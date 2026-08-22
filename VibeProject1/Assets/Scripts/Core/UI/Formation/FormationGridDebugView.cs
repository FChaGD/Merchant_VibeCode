using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Core
{
    /// <summary>
    /// 배치 UI 그리드의 열(X)/행(Y) 수와 타일 크기를 Play 모드에서 즉시 조절하기 위한 온스크린 디버그 패널.
    /// 값 적용 여부 판단(FormationLayout 재정렬 등)은 FormationPanel이 담당하며, 이 클래스는 입력값 파싱과
    /// 적용 버튼 클릭 중계만 담당한다.
    /// </summary>
    public class FormationGridDebugView : MonoBehaviour
    {
        [SerializeField] private TMP_InputField columnsInput;
        [SerializeField] private TMP_InputField rowsInput;
        [SerializeField] private TMP_InputField slotWidthInput;
        [SerializeField] private TMP_InputField slotHeightInput;
        [SerializeField] private Button applyButton;

        private Action<int, int, Vector2> onApply;

        public void Initialize(int columns, int rows, Vector2 slotSize, Action<int, int, Vector2> apply)
        {
            onApply = apply;

            if (columnsInput != null)
            {
                columnsInput.text = columns.ToString();
            }

            if (rowsInput != null)
            {
                rowsInput.text = rows.ToString();
            }

            if (slotWidthInput != null)
            {
                slotWidthInput.text = slotSize.x.ToString("0");
            }

            if (slotHeightInput != null)
            {
                slotHeightInput.text = slotSize.y.ToString("0");
            }

            if (applyButton != null)
            {
                applyButton.onClick.RemoveAllListeners();
                applyButton.onClick.AddListener(Apply);
            }
        }

        private void Apply()
        {
            var columns = ParseInt(columnsInput, 1);
            var rows = ParseInt(rowsInput, 1);
            var width = ParseFloat(slotWidthInput, 120f);
            var height = ParseFloat(slotHeightInput, 120f);
            onApply?.Invoke(columns, rows, new Vector2(width, height));
        }

        private static int ParseInt(TMP_InputField field, int fallback)
        {
            return field != null && int.TryParse(field.text, out var value) ? value : fallback;
        }

        private static float ParseFloat(TMP_InputField field, float fallback)
        {
            return field != null && float.TryParse(field.text, out var value) ? value : fallback;
        }
    }
}
