using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.Core
{
    /// <summary>
    /// 정비창 팔레트의 카테고리 한 줄(설계 16번, 기획 11번 §4) - 아이콘 표시/클릭/드래그는 기존
    /// FormationUnitIconView를 그대로 합성해 재사용하고(그리드/드래그고스트와 같은 컴포넌트), 이
    /// 뷰는 "잔여/전체" 라벨과 소진 시 비활성화(CanvasGroup)만 추가로 얹는다 - 팔레트 전용 관심사를
    /// 그리드/드래그고스트에도 쓰이는 공용 아이콘 컴포넌트에 섞지 않기 위함.
    /// </summary>
    public class FormationPaletteRowView : MonoBehaviour
    {
        [SerializeField] private FormationUnitIconView iconView;
        [SerializeField] private TMP_Text countLabel;
        [SerializeField] private CanvasGroup canvasGroup;

        public FormationCategoryKey Key { get; private set; }

        public void Bind(
            FormationCategorySummary summary,
            Action<FormationCategoryKey> categoryClicked,
            Action<FormationCategoryKey, PointerEventData> categoryBeginDrag,
            Action<PointerEventData> drag,
            Action<PointerEventData> endDrag)
        {
            Key = summary.Key;

            iconView.BindIconOnly(summary.Icon);
            if (countLabel != null)
            {
                countLabel.text = $"{summary.AvailableCount}/{summary.TotalCount}";
            }

            // 잔여 0 = 드래그 시작 자체를 막는다(기획 11번 §4 확정 사항).
            var isEmpty = summary.AvailableCount <= 0;
            if (canvasGroup != null)
            {
                canvasGroup.alpha = isEmpty ? 0.4f : 1f;
                canvasGroup.interactable = !isEmpty;
                canvasGroup.blocksRaycasts = !isEmpty;
            }

            iconView.SetHandlers(
                _ => categoryClicked?.Invoke(summary.Key),
                (_, eventData) => categoryBeginDrag?.Invoke(summary.Key, eventData),
                drag,
                endDrag);
        }
    }
}
