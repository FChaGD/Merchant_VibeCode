using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Core.Editor
{
    /// <summary>
    /// 대화 화면 공용 조립(Docs/Refactor/2026-10-26_씬전용.md G-3, SE-5) - 상행 대화·이벤트 대화 조립이 스탠딩(그림 + 이름) 조립을 각자 복사하고 있었다.
    /// 화면마다 다른 것(그림 아랫변·이름 칸 윗변·글자 크기)만 인자로 받는다.
    /// </summary>
    internal static class EditorDialogueBuilder
    {
        /// <summary>
        /// 스탠딩 자리(get-or-create). 원본 일러스트는 오른쪽 자리 기준 방향이라 왼쪽만 그림을 좌우반전한다(설계 88 §5.2) - 이름은 별도 요소라 반전되지 않는다.
        /// 저장 상태는 숨김(대화가 시작될 때 StagedDialogueView가 켠다).
        /// </summary>
        public static void BuildStanding(Transform parent, string name, Vector2 min, Vector2 max, bool flip, float figureMinY, float nameMaxY, float nameFontSize,
            out GameObject root, out Image figure, out TMP_Text nameText)
        {
            root = EditorUIBuilder.GetOrCreateUIObject(parent, name);
            EditorUIBuilder.SetAnchors(root.GetComponent<RectTransform>(), min, max);

            var figureGo = EditorUIBuilder.GetOrCreateUIObject(root.transform, "Figure");
            var figureRect = figureGo.GetComponent<RectTransform>();
            EditorUIBuilder.SetAnchors(figureRect, new Vector2(0f, figureMinY), new Vector2(1f, 1f));
            figureRect.localScale = new Vector3(flip ? -1f : 1f, 1f, 1f);
            figure = EditorUIBuilder.EnsureImage(figureGo, Color.white);
            figure.raycastTarget = false;

            nameText = EditorUIBuilder.EnsureTextBlock(root.transform, "Name", new Vector2(0f, 0f), new Vector2(1f, nameMaxY), nameFontSize, TextAlignmentOptions.Center, Color.white);
            root.SetActive(false);
        }
    }
}
