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
    // 재구성 — 최신 줄이 맨 위(SetAsFirstSibling), 캡(6) 초과분은 가장 오래된(맨 아래) 줄부터 제거.
    public sealed class NotesFeed : MonoBehaviour
    {
        private const int Cap = 6;

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
            if (label != null) label.text = line;

            _rows.Insert(0, row);
            while (_rows.Count > Cap)
            {
                int last = _rows.Count - 1;
                if (_rows[last] != null) Destroy(_rows[last].gameObject);
                _rows.RemoveAt(last);
            }
        }
    }
}
