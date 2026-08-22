using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Game.Core
{
    /// <summary>
    /// 배치 UI의 팔레트(보유 유닛 목록) 영역. 무제한 소스로 취급되므로 아이콘을 드래그해도
    /// 목록에서 제거하지 않는다. 배치 여부와 무관하게 항상 동일하게 표시한다.
    /// </summary>
    public class FormationPaletteView : MonoBehaviour
    {
        [SerializeField] private Transform iconContent;
        [SerializeField] private FormationUnitIconView iconPrefab;

        private readonly List<FormationUnitIconView> icons = new();

        public void SetRoster(
            IReadOnlyList<IFormationUnit> roster,
            Action<IFormationUnit> iconClicked,
            Action<IFormationUnit, FormationUnitIconView, PointerEventData> iconBeginDrag,
            Action<PointerEventData> iconDrag,
            Action<PointerEventData> iconEndDrag)
        {
            foreach (var icon in icons)
            {
                if (icon != null)
                {
                    Destroy(icon.gameObject);
                }
            }
            icons.Clear();

            if (iconPrefab == null || iconContent == null)
            {
                Debug.LogWarning($"{nameof(FormationPaletteView)}에 {nameof(iconPrefab)} 또는 {nameof(iconContent)}가 지정되어 있지 않다.");
                return;
            }

            foreach (var unit in roster)
            {
                var icon = Instantiate(iconPrefab, iconContent);
                icon.Bind(unit);
                icon.SetHandlers(
                    _ => iconClicked?.Invoke(unit),
                    (iconView, eventData) => iconBeginDrag?.Invoke(unit, iconView, eventData),
                    eventData => iconDrag?.Invoke(eventData),
                    eventData => iconEndDrag?.Invoke(eventData));
                icons.Add(icon);
            }
        }
    }
}
