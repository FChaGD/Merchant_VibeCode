#if UNITY_EDITOR
using System;
using Game.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.Core.DebugTools
{
    /// <summary>
    /// 지도 위에 배치된 디버그 도시 1개. 클릭/드래그 이벤트를 직접 해석하지 않고 그대로 위임한다 -
    /// 실제 동작(이동/선 긋기/향후 출발·도착 지정) 판단은 TripMapInteractionCoordinator가 담당한다
    /// (FormationUnitIconView와 동일한 패턴).
    /// </summary>
    public class TripDebugCityMarkerView : MonoBehaviour,
        IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [SerializeField] private Image iconImage;

        public string CityId { get; private set; }
        public RectTransform RectTransform { get; private set; }

        private Action<string> onClicked;
        private Action<TripDebugCityMarkerView, PointerEventData> onBeginDrag;
        private Action<PointerEventData> onDrag;
        private Action<PointerEventData> onEndDrag;

        private void Awake()
        {
            RectTransform = (RectTransform)transform;
        }

        public void Bind(string cityId)
        {
            CityId = cityId;
        }

        public void SetAnchoredPosition(Vector2 position)
        {
            RectTransform.anchoredPosition = position;
        }

        /// <summary>
        /// 출발/도착 배정 상태에 따른 시각적 구분(02번 기획 3.1절 "시각적 구분"). role이 null이면
        /// 미배정 색(프리팹 기본 흰색 - 원본 스프라이트 색 그대로)으로 되돌린다.
        /// </summary>
        public void SetRoleVisual(TripRole? role)
        {
            if (iconImage == null)
            {
                return;
            }

            iconImage.color = role switch
            {
                TripRole.Origin => new Color(0.85f, 0.35f, 0.15f),
                TripRole.Destination => new Color(0.2f, 0.35f, 0.75f),
                _ => Color.white,
            };
        }

        public void SetHandlers(
            Action<string> clicked,
            Action<TripDebugCityMarkerView, PointerEventData> beginDrag,
            Action<PointerEventData> drag,
            Action<PointerEventData> endDrag)
        {
            onClicked = clicked;
            onBeginDrag = beginDrag;
            onDrag = drag;
            onEndDrag = endDrag;
        }

        public void OnPointerClick(PointerEventData eventData) => onClicked?.Invoke(CityId);

        public void OnBeginDrag(PointerEventData eventData) => onBeginDrag?.Invoke(this, eventData);

        public void OnDrag(PointerEventData eventData) => onDrag?.Invoke(eventData);

        public void OnEndDrag(PointerEventData eventData) => onEndDrag?.Invoke(eventData);
    }
}
#endif
