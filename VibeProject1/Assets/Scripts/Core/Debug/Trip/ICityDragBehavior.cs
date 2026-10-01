#if UNITY_EDITOR
using UnityEngine.EventSystems;

namespace Game.Core.DebugTools
{
    /// <summary>
    /// 지도 위 끝점(도시·관문) 마커 드래그의 의미(이동/도로 긋기)를 갈아끼울 수 있는 전략(OCP). 도로 모드 여부에 따른 분기를 마커나
    /// 편집기의 if문에 박아넣지 않고, 드래그 의미가 늘어나도 새 구현체 추가만으로 확장할 수 있게 한다.
    /// </summary>
    internal interface ICityDragBehavior
    {
        void OnDragBegin(TripMapMarkerView marker, PointerEventData eventData);
        void OnDragUpdate(PointerEventData eventData);
        void OnDragEnd(PointerEventData eventData);
    }
}
#endif
