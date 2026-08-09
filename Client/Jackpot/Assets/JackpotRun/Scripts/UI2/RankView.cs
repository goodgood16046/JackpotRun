using System.Collections.Generic;
using JackpotRun.Core;
using JackpotRun.Game;
using UnityEngine;
using UnityEngine.UI;

namespace JackpotRun.UI2
{
    // 글로벌 랭킹 화면 — ENGINE_PORT_DESIGN.md S15 "RankView.cs" + 웹 파리티 P7-4(WEB_PARITY_DESIGN.md
    // §1-A #20 "랭킹 3노드"). slotrank/slotrank_asc/slotrank_deep 3개 RTDB 보드(앱·웹 공용 "개인 최고
    // 기록" 게시판, 카톡 봇의 jackpotdex/<token>.rank와는 별개)를 탭으로 전환해가며 각각 상위 100명을
    // score 내림차순(동점 ts 오름차순 — 먼저 세운 기록이 위)으로 나열한다. 데이터 조회는
    // RankingService.Fetch가 담당하고, 이 뷰는 결과를 행 템플릿에 채우기만 한다(런타임 코드생성
    // 없음, DexView.RenderGrid와 동일한 "템플릿 clone" 패턴). 탭 전환은 DexView.SetCategory와 동일한
    // "문자열 인자 UnityEvent persistent listener" 관례(SetBoard(string)).
    public sealed class RankView : MonoBehaviour
    {
        private const int MaxRows = 100;

        [SerializeField] private Text statusText;
        [SerializeField] private RectTransform listContent;
        // 자식 경로 계약: "Content/RankNo"·"Content/Nick"·"Content/Score" 각 Text, 루트 자신에 행 배경
        // Image(UiSceneBuilder.BuildRankScreen이 UiKit.Panel로 만든 것을 그대로 사용). UiKit.HGroup이
        // 만드는 중간 GameObject를 "Content"로 개명해 찾는다 — Transform.Find는 직계 자식만 찾으므로
        // (BuildDexCardTemplate의 "Content" 계약과 같은 이유, Opus S15 검수 치명-1 반영).
        [SerializeField] private RectTransform rowTemplate;

        // ── P7-4: 3보드 탭(일반/승천/심화) — DexView.tabImages/SetCategory와 동일 패턴. UiSceneBuilder가
        // 탭 버튼을 지을 때 이 순서/라벨을 그대로 참조한다(JackpotCatalog.CategoryOrder/CategoryTitle과
        // 동일한 "단일 진실 공급원" 취지 — 리터럴 중복 방지). RankingService.RankBoard 열거값 순서와
        // 1:1 대응(SetBoard(string)이 이 순서로 되돌려 파싱).
        [SerializeField] private Image[] tabImages = System.Array.Empty<Image>();
        public static readonly string[] BoardOrder = { "normal", "asc", "deep" };
        public static readonly string[] BoardLabel = { "일반", "심화 학기", "심화(심볼 덱)" };

        private RankingService.RankBoard _board = RankingService.RankBoard.Normal;

        // 1~3위 순위 숫자 강조색 — 메달 이모지(🥇🥈🥉)는 astral이라 레거시 Text가 렌더링하지 못한다
        // (S8 항목⑤ 실측, PerkOfferPanel과 동일 제약 — BMP/색상 대체가 프로젝트 규칙).
        private static readonly Color GoldRank = UiKit.Gold;
        private static readonly Color SilverRank = UiKit.Hex("#C7CFDE");
        private static readonly Color BronzeRank = UiKit.Hex("#D08A4E");

        private void Awake()
        {
            if (rowTemplate != null) rowTemplate.gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            _board = RankingService.RankBoard.Normal;
            UpdateTabHighlight();
            Refetch();
        }

        /// <summary>탭 버튼의 UnityEvent 퍼시스턴트 리스너 대상(UiSceneBuilder가 "normal"/"asc"/"deep"
        /// 문자열을 인자로 바로 연결 — DexView.SetCategory와 동일 관례).</summary>
        public void SetBoard(string boardKey)
        {
            _board = boardKey switch
            {
                "asc" => RankingService.RankBoard.Asc,
                "deep" => RankingService.RankBoard.Deep,
                _ => RankingService.RankBoard.Normal,
            };
            UpdateTabHighlight();
            Refetch();
        }

        private void UpdateTabHighlight()
        {
            int idx = (int)_board;
            for (int i = 0; i < tabImages.Length; i++)
                if (tabImages[i] != null) tabImages[i].color = i == idx ? UiKit.Panel3 : UiKit.PanelBg;
        }

        private void Refetch()
        {
            ClearRows();
            ShowStatus("랭킹 불러오는 중...");
            RankingService.Fetch(this, _board, OnFetchOk, OnFetchError);
        }

        // 기존 행 제거(rowTemplate 제외) — DexView.RenderGrid와 동일 패턴.
        private void ClearRows()
        {
            if (listContent == null) return;
            for (int i = listContent.childCount - 1; i >= 0; i--)
            {
                var child = listContent.GetChild(i);
                if (child == rowTemplate) continue;
                Destroy(child.gameObject);
            }
        }

        private void OnFetchOk(List<RankingService.Entry> entries)
        {
            // 화면이 꺼지면 host(this) 코루틴이 함께 멎으므로 콜백 누수 없음 — 그래도 방어적으로 가드.
            if (this == null || !isActiveAndEnabled) return;

            if (entries == null || entries.Count == 0)
            {
                ShowStatus("아직 등록된 기록이 없어요\n첫 기록의 주인공이 되어보세요!");
                return;
            }

            HideStatus();
            string myPid = RankingService.PlayerId();
            int count = Mathf.Min(entries.Count, MaxRows);
            for (int i = 0; i < count; i++) BuildRow(i, entries[i], entries[i].pid == myPid);
        }

        private void OnFetchError(string reason)
        {
            if (this == null || !isActiveAndEnabled) return;
            ShowStatus("랭킹을 불러오지 못했어요\n네트워크 확인 후 다시 열어주세요");
        }

        private void ShowStatus(string message)
        {
            if (statusText == null) return;
            statusText.text = message;
            statusText.gameObject.SetActive(true);
        }

        private void HideStatus()
        {
            if (statusText != null) statusText.gameObject.SetActive(false);
        }

        private void BuildRow(int index, RankingService.Entry e, bool isMine)
        {
            if (listContent == null || rowTemplate == null || e == null) return;

            var row = Instantiate(rowTemplate, listContent);
            row.gameObject.SetActive(true);
            row.name = "Row_" + (index + 1);

            var bg = row.GetComponent<Image>();
            if (bg != null) bg.color = isMine ? UiKit.CardTop : UiKit.PanelBg;

            int rank = index + 1;
            var rankNoText = row.Find("Content/RankNo")?.GetComponent<Text>();
            if (rankNoText != null)
            {
                rankNoText.text = rank.ToString();
                rankNoText.color = RankColor(rank);
            }

            var nickText = row.Find("Content/Nick")?.GetComponent<Text>();
            if (nickText != null)
            {
                nickText.text = e.nick;
                nickText.color = isMine ? UiKit.Gold : UiKit.TextPrimary;
            }

            var scoreText = row.Find("Content/Score")?.GetComponent<Text>();
            if (scoreText != null)
            {
                // 승천 보드만 "심화 N" 배지를 함께 보여준다(웹 topAscScores 행이 asc를 담는 것과 동일 —
                // 일반/심화(심볼 덱) 보드는 asc가 항상 0이라 배지를 넣지 않는다).
                string ascSuffix = (_board == RankingService.RankBoard.Asc && e.asc > 0) ? $" · 심화{e.asc}" : "";
                scoreText.text = NumberFormat.Comma(e.score) + " · S" + e.stage + ascSuffix;
            }
        }

        // 1~3위는 금/은/동 색으로 순위 숫자를 강조한다(메달 이모지 대체 — 필드 선언부 주석 참조).
        private static Color RankColor(int rank)
        {
            switch (rank)
            {
                case 1: return GoldRank;
                case 2: return SilverRank;
                case 3: return BronzeRank;
                default: return UiKit.TextPrimary;
            }
        }
    }
}
