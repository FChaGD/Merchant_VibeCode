using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.Core
{
    /// <summary>
    /// 상행 준비 UI의 지도 영역. 상하좌우 드래그 이동은 같은 GameObject의 ScrollRect(Clamped)가
    /// 담당하고, 이 컴포넌트는 마우스 휠 확대/축소만 처리한다. 출발/도착 표시는 더 이상 고정 핀이
    /// 아니라 지도 위에 자유 배치되는 디버그 도시 아이콘(TripDebugCityMarkerView)이 대신한다
    /// (02번 기획 문서 개정 이력 참고). 확대/축소는 content의 localScale을 조절하는 방식이라 축소 시
    /// 콘텐츠가 뷰포트 경계 밖으로 나갈 수 있어 직접 재클램프한다(ScrollRect의 Clamped 이동은 드래그
    /// 중에만 적용되고, 스케일 변경 자체는 보정하지 않는다).
    /// </summary>
    [RequireComponent(typeof(ScrollRect))]
    public class TripMapView : MonoBehaviour, IScrollHandler
    {
        [SerializeField] private float minZoom = 1f;
        [SerializeField] private float maxZoom = 2.5f;
        [SerializeField] private float zoomStep = 0.1f;
        [SerializeField, Min(0.01f)] private float zoomSmoothTime = 0.12f;

        private ScrollRect scrollRect;
        private RectTransform viewport;
        private RectTransform content;
        private float currentZoom;
        private float targetZoom;
        private float zoomVelocity;

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

            if (viewport != null && content != null && content.rect.width > 0f && content.rect.height > 0f)
            {
                // 줌아웃 최소 배율(가장 멀리 축소했을 때)은 지도가 상하 또는 좌우 경계 중 먼저 닿는
                // 쪽에서 멈추도록 계산한다 - 두 축 각각 "뷰포트를 꽉 채우는 배율" 중 더 큰 쪽을 써야
                // 반대편 축도 화면을 벗어나지 않는다(더 작은 쪽을 쓰면 그 축에 빈 여백이 생긴다).
                minZoom = Mathf.Max(viewport.rect.width / content.rect.width, viewport.rect.height / content.rect.height);
            }

            currentZoom = content != null ? Mathf.Clamp(content.localScale.x, minZoom, maxZoom) : minZoom;
            targetZoom = currentZoom;
        }

        public void OnScroll(PointerEventData eventData)
        {
            if (content == null)
            {
                return;
            }

            // 스케일을 즉시 바꾸지 않고 목표값만 갱신한다 - 휠 한 칸(스크롤 이벤트 1회)마다 순간적으로
            // 점프하면 끊겨 보이므로, 실제 스케일 적용은 Update()에서 목표값을 향해 매끄럽게 보간한다.
            targetZoom = Mathf.Clamp(targetZoom + eventData.scrollDelta.y * zoomStep, minZoom, maxZoom);
        }

        private void Update()
        {
            if (content == null || Mathf.Approximately(currentZoom, targetZoom))
            {
                return;
            }

            currentZoom = Mathf.SmoothDamp(currentZoom, targetZoom, ref zoomVelocity, zoomSmoothTime);
            if (Mathf.Abs(currentZoom - targetZoom) < 0.0005f)
            {
                currentZoom = targetZoom;
            }

            content.localScale = new Vector3(currentZoom, currentZoom, 1f);
            ClampContentPosition();
        }

        private void ClampContentPosition()
        {
            if (viewport == null)
            {
                return;
            }

            var scaledContentSize = content.rect.size * currentZoom;
            var maxOffset = Vector2.Max((scaledContentSize - viewport.rect.size) * 0.5f, Vector2.zero);

            var position = content.anchoredPosition;
            position.x = Mathf.Clamp(position.x, -maxOffset.x, maxOffset.x);
            position.y = Mathf.Clamp(position.y, -maxOffset.y, maxOffset.y);
            content.anchoredPosition = position;
        }
    }
}
