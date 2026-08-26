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

        // 아이콘 인스턴스를 매번 파괴 후 재생성하지 않고, 로스터 개수 변화분만큼만 생성/파괴하고
        // 나머지는 Bind/SetHandlers로 내용만 덮어써 재사용한다(get-or-create, CLAUDE.md 최적화 규칙).
        // 매번 전체를 다시 바인딩하므로 이전에 어떤 로스터였든 결과는 동일하다.
        public void SetRoster(
            IReadOnlyList<IFormationUnit> roster,
            Action<IFormationUnit> iconClicked,
            Action<IFormationUnit, FormationUnitIconView, PointerEventData> iconBeginDrag,
            Action<PointerEventData> iconDrag,
            Action<PointerEventData> iconEndDrag)
        {
            if (iconPrefab == null || iconContent == null)
            {
                foreach (var icon in icons)
                {
                    if (icon != null)
                    {
                        Destroy(icon.gameObject);
                    }
                }
                icons.Clear();

                Debug.LogWarning($"{nameof(FormationPaletteView)}에 {nameof(iconPrefab)} 또는 {nameof(iconContent)}가 지정되어 있지 않다.");
                return;
            }

            while (icons.Count > roster.Count)
            {
                var last = icons[^1];
                if (last != null)
                {
                    Destroy(last.gameObject);
                }
                icons.RemoveAt(icons.Count - 1);
            }

            while (icons.Count < roster.Count)
            {
                icons.Add(Instantiate(iconPrefab, iconContent));
            }

            for (var i = 0; i < roster.Count; i++)
            {
                var unit = roster[i];
                var icon = icons[i];
                icon.Bind(unit);
                icon.SetHandlers(
                    _ => iconClicked?.Invoke(unit),
                    (iconView, eventData) => iconBeginDrag?.Invoke(unit, iconView, eventData),
                    eventData => iconDrag?.Invoke(eventData),
                    eventData => iconEndDrag?.Invoke(eventData));
            }
        }
    }
}
