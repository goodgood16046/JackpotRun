using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace JackpotRun.UI2
{
    // 스핀 노트 피드 — ENGINE_PORT_DESIGN.md S7 파일 구성 표의 Run/NotesFeed.cs. 최근 N줄만 유지하며
    // RunEvent를 사람이 읽는 한 줄 텍스트로 번역해 쌓는다(카톡 문자열 조립 금지 원칙은 엔진 쪽 얘기 —
    // 여기 UI 레이어가 구조화 이벤트를 문구로 "연출"하는 것은 원본 RunScreen.cs의 의도된 역할이다).
    // 이관 원본: Scripts/UI/RunScreen.cs의 _notesFeed/AppendNote/AppendSpinNotes.
    //
    // Fable 육안 검수 지시(2026-07-31): 단일 Text 블록 대신 줄마다 은은한 배경 카드를 가진 행 템플릿으로
    // 재구성 — 최신 줄이 맨 위(SetAsFirstSibling), 캡 초과분은 가장 오래된(맨 아래) 줄부터 제거.
    // S16 — 결과 패널(GainPanel) 도입으로 "왜 얻었는지"는 그쪽이 맡는다. 여기는 이제 보조 로그일
    // 뿐이라 캡을 6→3으로 낮춘다("로그는 축소 — 최근 3줄만", 설계 명시).
    public sealed class NotesFeed : MonoBehaviour
    {
        private const int Cap = 3;

        // S16 육안 검수 — 엔진 notes 문자열은 astral 이모지(서로게이트 쌍)를 그대로 담고 있어
        // 레거시 Text에서 아예 그려지지 않는다("🔥 EXP +50%" → " EXP +50%"처럼 앞이 빈 채로 보였다).
        // ReelView.TagTranslate와 같은 방침으로 표시 레이어에서만 한글/BMP 기호로 바꾼다(엔진 미수정).
        // 목록은 SpinResolver.Evaluate·RunController가 만드는 notes 전수 조사 결과.
        private static readonly Dictionary<string, string> EmojiWords = new Dictionary<string, string>
        {
            { "🍒", "체리" }, { "📖", "책" }, { "💎", "보석" }, { "👑", "왕관" },
            { "💣", "폭탄" }, { "🧲", "자석" }, { "🎲", "주사위" },
            { "🗝", "열쇠" }, { "🎰", "잭팟" }, { "🔥", "불꽃" }, { "🔗", "인접" },
            { "💫", "운명폭발" }, { "🧩", "퍼즐" }, { "💠", "완벽한모양" }, { "🧯", "총배율캡" },
            { "👯", "짝맞춤" }, { "👁️", "해골관찰" }, { "🌱", "씨앗" }, { "💥", "폭발" },
            { "📜", "보험증서" }, { "🔔", "운명의종" }, { "✅", "획득" },
        };

        private const string CoinEmoji = "🪙";

        [SerializeField] private RectTransform rowsContent;
        [SerializeField] private RectTransform rowTemplate; // 자식 경로 계약: "Label"(Text)

        private readonly List<RectTransform> _rows = new List<RectTransform>();

        private void Awake()
        {
            if (rowTemplate != null) rowTemplate.gameObject.SetActive(false);
        }

        public void Clear()
        {
            for (int i = _rows.Count - 1; i >= 0; i--)
                if (_rows[i] != null) Destroy(_rows[i].gameObject);
            _rows.Clear();
        }

        public void Append(string line)
        {
            if (string.IsNullOrEmpty(line) || rowsContent == null || rowTemplate == null) return;

            var row = Instantiate(rowTemplate, rowsContent);
            row.gameObject.SetActive(true);
            row.SetAsFirstSibling(); // 최신 줄이 위로

            var label = row.Find("Label")?.GetComponent<Text>();
            if (label != null) label.text = Readable(line);

            _rows.Insert(0, row);
            while (_rows.Count > Cap)
            {
                int last = _rows.Count - 1;
                if (_rows[last] != null) Destroy(_rows[last].gameObject);
                _rows.RemoveAt(last);
            }
        }

        // 알려진 이모지는 한글 낱말로 치환하고, 표에 없는 서로게이트 쌍은 통째로 제거한다(그려지지
        // 않는 자리가 공백으로 남아 문장 앞이 들여쓰기된 것처럼 보이는 것을 막는다). 이어진 공백은
        // 하나로 접고 양끝을 다듬는다.
        private static string Readable(string line)
        {
            // 코인 이모지는 한 줄에 두 번 나온다("🪙 +5🪙" — 머리표 + 단위). 둘 다 "코인"으로 바꾸면
            // "코인 +5코인"이 되므로 처음 것만 낱말로 남기고 뒤는 버린다.
            int coin = line.IndexOf(CoinEmoji, System.StringComparison.Ordinal);
            if (coin >= 0)
                line = line.Substring(0, coin) + "코인" +
                       line.Substring(coin + CoinEmoji.Length).Replace(CoinEmoji, "");

            foreach (var kv in EmojiWords)
                if (line.IndexOf(kv.Key, System.StringComparison.Ordinal) >= 0)
                    line = line.Replace(kv.Key, kv.Value);

            var sb = new System.Text.StringBuilder(line.Length);
            bool prevSpace = false;
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (char.IsHighSurrogate(c))
                {
                    i++; // 짝인 low surrogate까지 함께 버린다
                    continue;
                }
                if (char.IsLowSurrogate(c) || c == '️') continue; // 짝 잃은 조각·이모지 변형 선택자
                bool isSpace = c == ' ';
                if (isSpace && prevSpace) continue;
                prevSpace = isSpace;
                sb.Append(c);
            }
            return sb.ToString().Trim();
        }
    }
}
