using System.Text;

namespace JackpotRun.Core
{
    // 레거시 uGUI Text 표시 전용 새니타이저 — ReelView.TagTranslate 선례(S8 항목⑤)와 같은 문제를
    // 더 일반화해서 푼다: astral(서로게이트 페어, U+10000 이상 — 요즘 이모지 대부분)는 legacy Text에
    // 렌더링되지 않는다. TagTranslate는 알려진 짧은 태그 문자열 전체를 고정 치환하는 방식이었지만,
    // 업적 name/desc처럼 "웹 원문 문장 중간에 이모지가 박혀 있는" 텍스트(예: "🍒체리 누적 100개",
    // "체리 계열(🍒+🍑) 비중 50%↑")에는 전체 치환이 아니라 문자 단위 필터가 필요하다.
    //
    // WEB_PARITY_DESIGN.md P3-2(Opus 2차 검수 저④) — Achievements.cs의 34종 데이터는 웹 원문을 그대로
    // 보존한다(emoji 필드만 astral일 때 빈 문자열로 대체했을 뿐, name/desc 문장 안에 박힌 이모지는
    // 손대지 않았다 — "id·이모지·이름·설명·key·th를 데이터 그대로" 원칙). 대신 이 텍스트를 실제
    // 레거시 Text 컴포넌트에 넣기 *직전*(표시 레이어)에서만 걸러낸다 — GameOverPanel/DexView 등.
    public static class TextSanitize
    {
        // s에서 astral 코드포인트(UTF-16 서로게이트 페어)를 전부 제거한 새 문자열을 반환한다.
        // BMP 문자(한글·숫자·문장부호·화살표 등 U+FFFF 이하)는 그대로 통과 — 데이터 원문은 건드리지
        // 않고(입력 인자 불변) 호출측에 새 문자열만 돌려준다.
        public static string StripAstral(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;

            StringBuilder sb = null; // astral을 실제로 만난 경우에만 할당(흔한 "이미 깨끗한 문자열" 경로는 원본 그대로 반환)
            int copiedUpTo = 0;

            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                bool isHighSurrogateStart = char.IsHighSurrogate(c) && i + 1 < s.Length && char.IsLowSurrogate(s[i + 1]);
                bool isStraySurrogate = !isHighSurrogateStart && char.IsSurrogate(c);

                if (!isHighSurrogateStart && !isStraySurrogate) continue; // 평범한 BMP 문자 — 나중에 한 번에 복사

                if (sb == null) sb = new StringBuilder(s.Length);
                sb.Append(s, copiedUpTo, i - copiedUpTo); // 여기까지의 BMP 구간을 통째로 복사
                if (isHighSurrogateStart) i++; // 로우 서로게이트까지 함께 스킵
                copiedUpTo = i + 1;
            }

            if (sb == null) return s; // astral 없음 — 원본 그대로(할당 없이 빠른 경로)
            sb.Append(s, copiedUpTo, s.Length - copiedUpTo);
            return sb.ToString();
        }
    }
}
