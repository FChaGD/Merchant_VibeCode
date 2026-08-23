using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.Core
{
    /// <summary>
    /// 상행 준비 UI 지도 위에 디버그 도시를 배치하기 위한 팔레트 아이콘. 배치 UI 팔레트와 달리 목록이
    /// 아니라 아이콘 1개만 필요해, FormationPaletteView처럼 별도 아이콘 컴포넌트를 두지 않고 이
    /// 컴포넌트 자체가 드래그 이벤트를 낸다. 드래그해도 아이콘은 소모되지 않는다 - 실제 도시 인스턴스
    /// 생성/취소 판단은 TripMapInteractionCoordinator가 담당한다.
    /// </summary>
    public class TripDebugCityPaletteView : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [SerializeField] private Image iconImage;

        public Sprite Icon => iconImage != null ? iconImage.sprite : null;

        private Action<PointerEventData> onBeginDrag;
        private Action<PointerEventData> onDrag;
        private Action<PointerEventData> onEndDrag;

        public void Initialize(Action<PointerEventData> beginDrag, Action<PointerEventData> drag, Action<PointerEventData> endDrag)
        {
            onBeginDrag = beginDrag;
            onDrag = drag;
            onEndDrag = endDrag;
        }

        public void OnBeginDrag(PointerEventData eventData) => onBeginDrag?.Invoke(eventData);

        public void OnDrag(PointerEventData eventData) => onDrag?.Invoke(eventData);

        public void OnEndDrag(PointerEventData eventData) => onEndDrag?.Invoke(eventData);
    }
}
