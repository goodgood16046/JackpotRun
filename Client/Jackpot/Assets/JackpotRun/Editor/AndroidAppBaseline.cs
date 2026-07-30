using System.IO;
using UnityEditor;
using UnityEngine;

namespace JackpotRun.Editor
{
    /// <summary>
    /// Android 앱 출시를 위한 PlayerSettings 베이스라인을 1회만 자동 적용한다.
    /// Library는 머신 로컬이므로, 적용 후 사용자가 값을 바꿔도 되돌리지 않는다.
    /// </summary>
    [InitializeOnLoad]
    public static class AndroidAppBaseline
    {
        // CWD 의존 금지 — 프로젝트 루트 기준 절대 경로.
        private static string MarkerPath => Path.Combine(
            Path.GetDirectoryName(Application.dataPath), "Library", "JackpotRunAndroidBaseline.marker");

        static AndroidAppBaseline()
        {
            EditorApplication.delayCall += Apply;
        }

        private static void Apply()
        {
            if (File.Exists(MarkerPath)) return;

            if (PlayerSettings.companyName != "Phigolf")
                PlayerSettings.companyName = "Phigolf";

            if (PlayerSettings.productName != "JackpotRun")
                PlayerSettings.productName = "JackpotRun";

            // Unity 기본값("0.1")일 때만 초기화 — 클린 클론에서 실제 버전을 되돌리지 않도록.
            if (PlayerSettings.bundleVersion == "0.1")
                PlayerSettings.bundleVersion = "0.1.0";

            if (PlayerSettings.GetApplicationIdentifier(BuildTargetGroup.Android) != "com.phigolf.jackpotrun")
                PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, "com.phigolf.jackpotrun");

            if (PlayerSettings.defaultInterfaceOrientation != UIOrientation.Portrait)
                PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;

            if (PlayerSettings.Android.minSdkVersion != AndroidSdkVersions.AndroidApiLevel24)
                PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel24;

            if (PlayerSettings.Android.targetSdkVersion != AndroidSdkVersions.AndroidApiLevelAuto)
                PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;

            if (PlayerSettings.Android.bundleVersionCode != 1)
                PlayerSettings.Android.bundleVersionCode = 1;

            if (PlayerSettings.GetScriptingBackend(BuildTargetGroup.Android) != ScriptingImplementation.IL2CPP)
                PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);

            const AndroidArchitecture targetArch = AndroidArchitecture.ARM64 | AndroidArchitecture.ARMv7;
            if (PlayerSettings.Android.targetArchitectures != targetArch)
                PlayerSettings.Android.targetArchitectures = targetArch;

            AssetDatabase.SaveAssets();

            // 타깃 스위치가 실패하면 마커를 쓰지 않는다 — 다음 도메인 리로드에서 재시도.
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            {
                if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android))
                {
                    Debug.LogWarning("[JackpotRun] Android Build Support 미설치 — 빌드타깃 스위치 보류 (설정만 적용됨)");
                    return;
                }
                if (!EditorUserBuildSettings.SwitchActiveBuildTargetAsync(BuildTargetGroup.Android, BuildTarget.Android))
                {
                    Debug.LogWarning("[JackpotRun] Android 빌드타깃 스위치 시작 실패 — 다음 리로드에서 재시도");
                    return;
                }
            }

            Debug.Log("[JackpotRun] Android app baseline applied");
            try
            {
                File.WriteAllText(MarkerPath, System.DateTime.UtcNow.ToString("o"));
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("[JackpotRun] 베이스라인 마커 기록 실패: " + ex.Message);
            }
        }
    }
}
