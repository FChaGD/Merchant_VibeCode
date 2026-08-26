using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.Core
{
    /// <summary>
    /// 전투 뷰의 드래그팬+휠줌 카메라(Docs/기획/09_전투뷰_카메라_기획.md). ScrollRect(Clamped)로
    /// 드래그 팬을, ScrollRectZoomController(TripMapView와 공유)로 커서 고정 줌을 처리한다. 콘텐츠
    /// 크기(=전장 바운딩 박스)는 전투마다 대형 크기에 따라 달라지므로 고정값이 아니라
    /// ConfigureFieldBounds로 매 전투 다시 잡는다 - TripMapView(콘텐츠 크기 고정)와 다른 점이다.
    /// </summary>
    // FieldUIInstaller(Editor 어셈블리)가 EditorUIBuilder.GetOrAddComponent<BattleFieldCameraView>로
    // 직접 부착해야 해서 internal이 아니라 public이어야 한다(TripMapView와 같은 이유).
    [RequireComponent(typeof(ScrollRect))]
    public class BattleFieldCameraView : MonoBehaviour, IScrollHandler
    {
        private const float MaxZoomRatio = 2.5f; // 기획 §3.2 확정값 - 최소 줌(전장 전체 보기) 대비 배율

        private ScrollRect scrollRect;
        private RectTransform viewport;
        private RectTransform content;
        private ScrollRectZoomController zoomController;

        private void Awake()
        {
            scrollRect = GetComponent<ScrollRect>();
            viewport = scrollRect.viewport;
            content = scrollRect.content;
            scrollRect.inertia = false; // 기획 §4 확정 - 드래그 관성 없음

            zoomController = new ScrollRectZoomController(MaxZoomRatio);
            zoomController.Bind(viewport, content);
        }

        /// <summary>
        /// 전투 시작마다 BattleViewPresenter가 호출한다. 전장 반지름이 대형 크기마다 달라지므로
        /// 콘텐츠 크기를 다시 잡고, 이전 전투의 팬/줌 상태를 이어받지 않도록 최소 줌+중앙으로
        /// 강제 리셋한다(기획 §5 - 매 전투 반지름이 달라 이전 상태를 이어받으면 새 경계와 어긋난다).
        /// </summary>
        public void ConfigureFieldBounds(float fieldRadius)
        {
            var diameterPixels = fieldRadius * 2f * BattleFieldGeometry.CoordinateToPixelScale;
            content.sizeDelta = new Vector2(diameterPixels, diameterPixels);
            zoomController.RecomputeBounds();
            zoomController.ResetToMinZoom();
        }

        public void OnScroll(PointerEventData eventData)
        {
            zoomController.ApplyScroll(eventData.position, eventData.scrollDelta.y, eventData.pressEventCamera);
        }
    }
}
