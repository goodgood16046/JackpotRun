using System.Collections.Generic;
using JackpotRun.Core;
using JackpotRun.Game;
using UnityEngine;
using UnityEngine.UI;

namespace JackpotRun.UI2
{
    // 글로벌 랭킹 화면 — ENGINE_PORT_DESIGN.md S15 "RankView.cs": jackpotrank/$pid RTDB 보드(앱·웹
    // 공용 "개인 최고 기록" 게시판, 카톡 봇의 jackpotdex/<token>.rank와는 별개)를 읽어 상위 100명을
    // score 내림차순(동점 ts 오름차순 — 먼저 세운 기록이 위)으로 나열한다. 데이터 조회는
    // RankingService.Fetch가 담당하고, 이 뷰는 결과를 행 템플릿에 채우기만 한다(런타임 코드생성
    // 없음, DexView.RenderGrid와 동일한 "템플릿 clone" 패턴).
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
            ClearRows();
            ShowStatus("랭킹 불러오는 중...");
            RankingService.Fetch(this, OnFetchOk, OnFetchError);
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
            if (scoreText != null) scoreText.text = NumberFormat.Comma(e.score) + " · S" + e.stage;
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
