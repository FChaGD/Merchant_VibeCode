using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.Core
{
    /// <summary>
    /// 상행 준비 UI의 지도 영역. 상하좌우 드래그 이동은 같은 GameObject의 ScrollRect(Clamped)가
    /// 담당하고, 이 컴포넌트는 마우스 휠 확대/축소만 처리한다 - 실제 줌 계산(커서 위치 고정 줌,
    /// 최소/최대 줌 경계, 경계 클램핑)은 ScrollRectZoomController에 위임한다. 전투 뷰 카메라
    /// (BattleFieldCameraView)와 같은 조작 스타일을 공유하기 위한 것으로, 원래 있던 화면 중앙 기준
    /// 줌·관성 있는 드래그·SmoothDamp 보간은 폐기했다(Docs/설계/09_전투뷰_카메라_아키텍처.md §6 -
    /// 09번 기획에 따라 전투 뷰와 동일한 스타일로 통일). 출발/도착 표시는 더 이상 고정 핀이 아니라
    /// 지도 위에 자유 배치되는 디버그 도시 아이콘(TripDebugCityMarkerView)이 대신한다
    /// (02번 기획 문서 개정 이력 참고).
    /// </summary>
    [RequireComponent(typeof(ScrollRect))]
    public class TripMapView : MonoBehaviour, IScrollHandler
    {
        private const float MaxZoomRatio = 2.5f; // 09번 기획 §3.2 확정값 - 전투 뷰 카메라와 동일

        private ScrollRect scrollRect;
        private RectTransform viewport;
        private RectTransform content;
        private ScrollRectZoomController zoomController;

        /// <summary>
        /// 지도 화면 영역 - 드롭이 지도 안인지 판정할 때 쓴다(디버그 도시 배치 등). Awake 이전에는
        /// null이다 - 이 상행 준비 UI 패널은 씬에 비활성 상태로 배치돼 있어 Awake가 최초 활성화
        /// (TripPanel.Open) 시점까지 지연되므로, 이 값을 참조하는 쪽은 매번 다시 읽어야 하고 등록
        /// 시점(RegisterTripUI/Bind)에 값을 캐시해서는 안 된다.
        /// </summary>
        public RectTransform Viewport => viewport;

        /// <summary>지도 콘텐츠 - 이 하위에 배치된 요소는 팬/줌에 자동으로 함께 움직인다. Viewport와 동일한 지연 초기화 주의사항이 적용된다.</summary>
        public RectTransform Content => content;

        private void Awake()
        {
            scrollRect = GetComponent<ScrollRect>();
            viewport = scrollRect.viewport;
            content = scrollRect.content;
            // 관성 없음 - 09번 기획에 따라 전투 뷰 카메라와 같은 스타일로 통일(기존엔 ScrollRect 기본 관성 있었음).
            scrollRect.inertia = false;

            zoomController = new ScrollRectZoomController(MaxZoomRatio);
            zoomController.Bind(viewport, content);
            zoomController.RecomputeBounds();
        }

        public void OnScroll(PointerEventData eventData)
        {
            zoomController?.ApplyScroll(eventData.position, eventData.scrollDelta.y, eventData.pressEventCamera);
        }
    }
}
