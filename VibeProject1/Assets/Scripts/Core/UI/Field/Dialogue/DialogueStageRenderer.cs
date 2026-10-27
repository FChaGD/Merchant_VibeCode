using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Core
{
    /// <summary>
    /// 말풍선·좌우 스탠딩 그리기 공용 부품(Docs/Refactor/2026-10-26_씬전용.md G-2, SE-5). 상행 대화 화면과 이벤트 대화 화면이 같은 코드를 각자 갖고 있었다 -
    /// 밝기 규칙(듣는 쪽 어둡게)을 바꾸면 이 한 곳만 고친다. 자리·이름·색·밝기 결정은 StagedDialogueView가 하고 여기서는 받은 값을 그리기만 한다.
    /// </summary>
    public static class DialogueStageRenderer
    {
        // 듣는 쪽(밝게 표시되지 않은 자리) 그림 색 계수.
        public const float DimFactor = 0.45f;

        /// <summary>새 줄. 보이는 글자 수는 0부터 다시 시작한다(타자 효과).</summary>
        public static void SetBubble(TMP_Text speakerNameText, TMP_Text lineText, string speakerName, string text)
        {
            speakerNameText.text = speakerName;
            lineText.text = text;
            lineText.maxVisibleCharacters = 0;
        }

        public static void SetStanding(GameObject root, Image figure, TMP_Text nameText, StandingState state)
        {
            root.SetActive(state.Visible);
            if (!state.Visible) return;
            var color = state.Highlighted ? state.Color : state.Color * DimFactor;
            color.a = 1f;
            figure.color = color;
            nameText.text = state.Name;
        }
    }
}
