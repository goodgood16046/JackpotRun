using UnityEditor;

namespace JackpotRun.EditorTools
{
    /// <summary>
    /// MCP for Unity stdio 브리지 자동 시작 — 에디터를 열 때마다 Window > MCP For Unity 를
    /// 수동으로 열지 않아도 Claude(MCP 클라이언트)가 접속할 수 있게 한다.
    /// 도메인 리로드마다 실행되며, 이미 돌고 있으면 아무것도 하지 않는다.
    /// </summary>
    [InitializeOnLoad]
    internal static class McpBridgeAutoStart
    {
        static McpBridgeAutoStart()
        {
            EditorApplication.delayCall += () =>
            {
                try
                {
                    if (MCPForUnity.Editor.Services.Transport.Transports.StdioBridgeHost.IsRunning) return;
                    // UseHttpTransport=false 로 강제 + StdioBridgeHost.StartAutoConnect()
                    MCPForUnity.Editor.McpCiBoot.StartStdioForCi();
                    UnityEngine.Debug.Log("[JackpotRun] MCP stdio 브리지 자동 시작");
                }
                catch (System.Exception e)
                {
                    UnityEngine.Debug.LogWarning("[JackpotRun] MCP 브리지 자동시작 실패: " + e.Message);
                }
            };
        }
    }
}
