using UnityEngine;
using UnityEngine.UI;

namespace JackpotRun.UI2
{
    // 순수 내비게이션 버튼(메뉴로/도감으로 등) — S8에서 AppRoot가 DontDestroyOnLoad 싱글턴이 되며
    // 씬에 존재하지 않게 되어, SceneBuilder가 예전처럼 UnityEventTools.AddPersistentListener로
    // "AppRoot 인스턴스"를 직접 가리키는 퍼시스턴트 리스너를 구울 수 없게 됐다(에디터 시점엔 그
    // 인스턴스가 아직 없다). 대신 이 컴포넌트를 버튼에 붙이고 target(단순 enum — 정상적으로 직렬화
    // 가능)만 SceneBuilder가 구우면, 런타임 Awake에서 AddListener로 AppRoot.Instance를 그때그때
    // 안전하게 조회해 호출한다.
    [RequireComponent(typeof(Button))]
    public sealed class NavButton : MonoBehaviour
    {
        public enum Target
        {
            Menu,
            Pick,
            Dex,
            Login,
        }

        [SerializeField] private Target target;

        private void Awake()
        {
            var btn = GetComponent<Button>();
            if (btn == null) return;
            btn.onClick.AddListener(OnClick);
        }

        private void OnClick()
        {
            var app = AppRoot.Instance;
            if (app == null) return;
            switch (target)
            {
                case Target.Menu: app.ShowMenu(); break;
                case Target.Pick: app.ShowPick(); break;
                case Target.Dex: app.ShowDex(); break;
                case Target.Login: app.ShowLogin(); break;
            }
        }
    }
}
