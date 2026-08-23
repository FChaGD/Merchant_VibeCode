using UnityEngine.EventSystems;

namespace Game.Core
{
    /// <summary>
    /// 지도 위 도시 아이콘 드래그의 의미(이동/선 긋기)를 갈아끼울 수 있는 전략(OCP). road-mode 여부에
    /// 따른 분기를 TripDebugCityMarkerView나 코디네이터의 if문에 박아넣지 않고, 드래그 의미가 늘어나도
    /// 새 구현체 추가만으로 확장할 수 있게 한다.
    /// </summary>
    internal interface ICityDragBehavior
    {
        void OnDragBegin(TripDebugCityMarkerView marker, PointerEventData eventData);
        void OnDragUpdate(PointerEventData eventData);
        void OnDragEnd(PointerEventData eventData);
    }
}
