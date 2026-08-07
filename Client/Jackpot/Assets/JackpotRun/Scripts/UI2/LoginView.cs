using UnityEngine;
using UnityEngine.UI;

namespace JackpotRun.UI2
{
    // 로그인 화면(신규) — ENGINE_PORT_DESIGN.md S8 "LoginView(신규, 인증 백엔드는 후속)". 닉네임
    // 입력(레거시 InputField, 2~12자) + [시작하기]/[게스트로 시작]. 저장은 PlayerPrefs
    // ("jackpotrun_nick")뿐 — **엔진(PlayerProfile)은 건드리지 않는다**. Firebase 연동 시 이 화면이
    // 실제 로그인으로 교체된다.
    public sealed class LoginView : MonoBehaviour
    {
        public const string NickPrefKey = "jackpotrun_nick";
        private const int MinLen = 2;
        private const int MaxLen = 12;

        [SerializeField] private InputField nicknameInput;
        [SerializeField] private Button startButton;
        [SerializeField] private Button guestButton;

        private AppRoot appRoot => AppRoot.Instance;

        private void Awake()
        {
            if (startButton != null) startButton.onClick.AddListener(OnStartClicked);
            if (guestButton != null) guestButton.onClick.AddListener(OnGuestClicked);
        }

        private void OnEnable()
        {
            if (nicknameInput != null) nicknameInput.text = SavedNick();
        }

        private void OnStartClicked()
        {
            string nick = nicknameInput != null ? (nicknameInput.text ?? string.Empty).Trim() : string.Empty;
            if (nick.Length < MinLen || nick.Length > MaxLen)
            {
                appRoot?.Toast?.Show($"닉네임은 {MinLen}~{MaxLen}자로 입력하세요.");
                return;
            }
            SaveNick(nick);
            appRoot?.CompleteLogin();
        }

        private void OnGuestClicked()
        {
            string guest = "게스트" + Random.Range(1000, 10000);
            SaveNick(guest);
            appRoot?.CompleteLogin();
        }

        public static string SavedNick() => PlayerPrefs.GetString(NickPrefKey, string.Empty);

        private static void SaveNick(string nick)
        {
            PlayerPrefs.SetString(NickPrefKey, nick);
            PlayerPrefs.Save();
        }
    }
}
