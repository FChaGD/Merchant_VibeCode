using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.Core
{
    /// <summary>
    /// 지도 위 끝점 1개(도시 또는 관문=지역 아이콘, Docs/설계/69번 §5). 클릭·드래그를 직접 해석하지 않고 위임한다 - 도시 클릭은
    /// 도착지 지정, 관문 클릭은 지역 전환(TripMapPresenter), 드래그는 디버그 편집(TripMapDebugEditor)이 판단한다.
    /// 예전 TripDebugCityMarkerView를 정식 폴더로 옮긴 것이다(파일·.meta를 함께 옮겨 기존 프리팹 참조 유지, 설계 69번 §9-2).
    /// 도시와 관문은 같은 컴포넌트를 쓰고 프리팹(모양·라벨)만 다르다.
    /// </summary>
    public class TripMapMarkerView : MonoBehaviour,
        IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [SerializeField] private Image iconImage;
        [SerializeField] private TMP_Text label; // 관문 프리팹만 - 가리키는 지역 이름

        private RectTransform rectTransform;
        private Action<MapNodeId> onClicked;
        private Action<TripMapMarkerView, PointerEventData> onBeginDrag;
        private Action<PointerEventData> onDrag;
        private Action<PointerEventData> onEndDrag;

        public MapNodeId Node { get; private set; }
        public RectTransform RectTransform => rectTransform != null ? rectTransform : rectTransform = (RectTransform)transform;
        public Sprite Icon => iconImage != null ? iconImage.sprite : null;

        public void Bind(MapNodeId node, string labelText = null)
        {
            Node = node;
            if (label != null) label.text = labelText ?? string.Empty;
        }

        public void SetAnchoredPosition(Vector2 position) => RectTransform.anchoredPosition = position;

        /// <summary>출발(현재 위치)/도착 강조(기획 02번 §3.1). role이 null이면 원래 색.</summary>
        public void SetRoleVisual(TripRole? role)
        {
            if (iconImage == null) return;

            iconImage.color = role switch
            {
                TripRole.Origin => new Color(0.85f, 0.35f, 0.15f),
                TripRole.Destination => new Color(0.2f, 0.35f, 0.75f),
                _ => Color.white,
            };
        }

        /// <summary>드래그 핸들러는 디버그 편집이 있을 때만 주입한다 - 없으면 드래그는 아무 일도 하지 않는다.</summary>
        public void SetHandlers(
            Action<MapNodeId> clicked,
            Action<TripMapMarkerView, PointerEventData> beginDrag,
            Action<PointerEventData> drag,
            Action<PointerEventData> endDrag)
        {
            onClicked = clicked;
            onBeginDrag = beginDrag;
            onDrag = drag;
            onEndDrag = endDrag;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            // 드래그로 끝난 입력은 클릭으로 치지 않는다.
            if (eventData.dragging) return;
            onClicked?.Invoke(Node);
        }

        public void OnBeginDrag(PointerEventData eventData) => onBeginDrag?.Invoke(this, eventData);

        public void OnDrag(PointerEventData eventData) => onDrag?.Invoke(eventData);

        public void OnEndDrag(PointerEventData eventData) => onEndDrag?.Invoke(eventData);
    }
}
