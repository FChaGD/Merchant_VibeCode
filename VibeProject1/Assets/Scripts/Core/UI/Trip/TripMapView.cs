using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.Core
{
    /// <summary>
    /// 상행 준비 UI의 지도 영역. 상하좌우 드래그 이동은 같은 GameObject의 ScrollRect(Clamped)가
    /// 담당하고, 이 컴포넌트는 마우스 휠 확대/축소와 출발/도착 핀 클릭 중계를 처리한다. 확대/축소는
    /// content의 localScale을 조절하는 방식이라 축소 시 콘텐츠가 뷰포트 경계 밖으로 나갈 수 있어
    /// 직접 재클램프한다(ScrollRect의 Clamped 이동은 드래그 중에만 적용되고, 스케일 변경 자체는
    /// 보정하지 않는다).
    /// </summary>
    [RequireComponent(typeof(ScrollRect))]
    public class TripMapView : MonoBehaviour, IScrollHandler
    {
        [SerializeField] private Button originPinButton;
        [SerializeField] private Button destinationPinButton;
        [SerializeField, Min(0.1f)] private float minZoom = 1f;
        [SerializeField] private float maxZoom = 2.5f;
        [SerializeField] private float zoomStep = 0.1f;

        private ScrollRect scrollRect;
        private RectTransform viewport;
        private RectTransform content;
        private float currentZoom;

        private void Awake()
        {
            scrollRect = GetComponent<ScrollRect>();
            viewport = scrollRect.viewport;
            content = scrollRect.content;
            currentZoom = content != null ? Mathf.Clamp(content.localScale.x, minZoom, maxZoom) : minZoom;
        }

        public void Initialize(Action onOriginClicked, Action onDestinationClicked)
        {
            BindPin(originPinButton, onOriginClicked);
            BindPin(destinationPinButton, onDestinationClicked);
        }

        private static void BindPin(Button button, Action callback)
        {
            if (button == null)
            {
                return;
            }

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => callback?.Invoke());
        }

        public void OnScroll(PointerEventData eventData)
        {
            if (content == null)
            {
                return;
            }

            var previousZoom = currentZoom;
            currentZoom = Mathf.Clamp(currentZoom + eventData.scrollDelta.y * zoomStep, minZoom, maxZoom);
            if (Mathf.Approximately(previousZoom, currentZoom))
            {
                return;
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
