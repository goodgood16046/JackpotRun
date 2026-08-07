using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace JackpotRun.EngineTests
{
    // 미니 어서션 러너 — 어셈블리 내 `Tests_*` 정적 클래스의 `public static void Run(TestCtx t)`를
    // 리플렉션으로 전부 찾아 호출한다. 실패 목록을 출력하고, 실패가 하나라도 있으면 exit code 1.
    // (ENGINE_PORT_DESIGN.md "검증(Tools/EngineTests)" 절 / S1 작업 지시서 Program.cs 규격.)
    public static class Program
    {
        public static int Main(string[] args)
        {
            string repoRoot;
            try
            {
                repoRoot = FindRepoRoot(AppContext.BaseDirectory);
            }
            catch (Exception ex)
            {
                Console.WriteLine("[FATAL] 저장소 루트를 찾지 못했습니다: " + ex.Message);
                return 1;
            }

            var ctx = new TestCtx(repoRoot);

            var testTypes = Assembly.GetExecutingAssembly()
                .GetTypes()
                .Where(t => t.IsClass && t.IsAbstract && t.IsSealed && t.Name.StartsWith("Tests_", StringComparison.Ordinal))
                .OrderBy(t => t.Name, StringComparer.Ordinal)
                .ToList();

            if (testTypes.Count == 0)
            {
                Console.WriteLine("[WARN] `Tests_*` 정적 클래스를 찾지 못했습니다.");
            }

            foreach (var type in testTypes)
            {
                var method = type.GetMethod("Run", BindingFlags.Public | BindingFlags.Static);
                if (method == null)
                {
                    Console.WriteLine($"[SKIP] {type.Name}: `public static void Run(TestCtx)` 메서드가 없습니다.");
                    continue;
                }

                ctx.Group(type.Name);
                Console.WriteLine($"[RUN ] {type.Name}");
                try
                {
                    method.Invoke(null, new object[] { ctx });
                }
                catch (TargetInvocationException tie)
                {
                    var inner = tie.InnerException ?? tie;
                    ctx.Fail(type.Name, "테스트 실행 중 예외 발생: " + inner);
                }
                catch (Exception ex)
                {
                    ctx.Fail(type.Name, "테스트 실행 중 예외 발생: " + ex);
                }
            }

            Console.WriteLine();
            Console.WriteLine($"==== RESULT: {ctx.Passed} passed, {ctx.Failed} failed (total {ctx.Passed + ctx.Failed}) ====");

            if (ctx.Failed > 0)
            {
                Console.WriteLine("Failures:");
                foreach (var f in ctx.Failures) Console.WriteLine(" - " + f);
                return 1;
            }

            return 0;
        }

        // AppContext.BaseDirectory(보통 .../Client/Jackpot/Tools/EngineTests/bin/Debug/net8.0/)에서
        // 상위 디렉터리로 걸어 올라가며 "Client" 폴더를 포함한 첫 조상 디렉터리를 저장소 루트로 판단한다.
        private static string FindRepoRoot(string startDir)
        {
            var dir = new DirectoryInfo(startDir);
            while (dir != null)
            {
                if (Directory.Exists(Path.Combine(dir.FullName, "Client")))
                    return dir.FullName;
                dir = dir.Parent;
            }
            throw new DirectoryNotFoundException("'Client' 폴더를 가진 조상 디렉터리를 찾지 못했습니다 (시작: " + startDir + ")");
        }
    }

    // 골든 테스트용 미니 어서션 컨텍스트. Eq/True는 결과를 Passed/Failed에 반영하고,
    // Report는 통과/실패에 영향을 주지 않는 참고 출력(예: 가중치표 diff 등)이다.
    public sealed class TestCtx
    {
        public string RepoRoot { get; }

        public int Passed { get; private set; }
        public int Failed { get; private set; }

        private string _group = "";
        private readonly List<string> _failures = new List<string>();
        public IReadOnlyList<string> Failures => _failures;

        public TestCtx(string repoRoot)
        {
            RepoRoot = repoRoot;
        }

        public void Group(string name) => _group = name;

        public void Eq<T>(T expected, T actual, string label)
        {
            bool ok = EqualityComparer<T>.Default.Equals(expected, actual);
            if (ok) Passed++;
            else Fail(label, $"expected <{Fmt(expected)}> but was <{Fmt(actual)}>");
        }

        // 부동소수 비교용 — 절대오차 tolerance 이내면 통과(§ 머신 가중치표 등 double 계산 검증에 사용).
        public void EqTol(double expected, double actual, string label, double tolerance = 1e-9)
        {
            bool ok = Math.Abs(expected - actual) <= tolerance;
            if (ok) Passed++;
            else Fail(label, $"expected <{expected}> but was <{actual}> (|diff|={Math.Abs(expected - actual)} > {tolerance})");
        }

        public void True(bool condition, string label, string detail = null)
        {
            if (condition) Passed++;
            else Fail(label, detail ?? "condition was false");
        }

        // S2 테스트 파일(Tests_Perks.cs/Tests_Content2.cs)이 이 하네스보다 먼저 작성되면서 TestCtx.Check
        // (bool,string) 시그니처를 임시로 가정해 두었다(각 파일 헤더 주석 참고). 이 프로젝트의 정식
        // 계약은 Eq/True/Report(S1 작업 지시서)이지만, 내 작성 범위 밖인 S2 파일을 고치는 대신 여기에
        // 얇은 호환 별칭만 추가한다.
        public void Check(bool condition, string message) => True(condition, message);

        public void Report(string label, string message) => Console.WriteLine($"  [info] {label}: {message}");

        public void Fail(string label, string detail)
        {
            Failed++;
            string line = string.IsNullOrEmpty(_group) ? $"{label} — {detail}" : $"[{_group}] {label} — {detail}";
            _failures.Add(line);
        }

        private static string Fmt<T>(T v) => v == null ? "null" : v.ToString();
    }
}
