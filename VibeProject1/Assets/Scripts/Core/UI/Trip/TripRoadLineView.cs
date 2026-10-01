using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.Core
{
    /// <summary>
    /// 지도 콘텐츠 좌표계에 그려지는 도로 선(또는 디버그 도로 그리기 중 미리보기 선). 얇은 Image를 시작점에 놓고 끝점 방향으로
    /// 회전·늘려 표현한다 - 좌표를 부모(content) 로컬 공간에서만 다뤄 지도 팬/줌을 그대로 따라간다. 더블클릭 삭제 요청은 디버그
    /// 편집이 핸들러를 줄 때만 동작한다. 예전 TripDebugRoadLineView를 정식 폴더로 옮긴 것이다(설계 69번 §9-2).
    /// SetRaycastTarget은 미리보기 선이 그 아래 끝점 드롭 판정을 가로채지 않게 할 때 쓴다.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class TripRoadLineView : MonoBehaviour, IPointerClickHandler
    {
        private const float DoubleClickThresholdSeconds = 0.3f;

        [SerializeField] private Image lineImage;

        private RectTransform rectTransform;
        private Action onDoubleClicked;
        private float lastClickTime = -10f;

        private RectTransform Rect => rectTransform != null ? rectTransform : rectTransform = (RectTransform)transform;

        public void SetRaycastTarget(bool raycastTarget)
        {
            if (lineImage != null) lineImage.raycastTarget = raycastTarget;
        }

        public void Initialize(Action doubleClicked) => onDoubleClicked = doubleClicked;

        public void SetEndpoints(Vector2 start, Vector2 end)
        {
            var delta = end - start;
            Rect.anchoredPosition = start;

            var size = Rect.sizeDelta;
            size.x = delta.magnitude;
            Rect.sizeDelta = size;

            var angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
            Rect.localRotation = Quaternion.Euler(0f, 0f, angle);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            var now = Time.unscaledTime;
            if (now - lastClickTime <= DoubleClickThresholdSeconds)
            {
                lastClickTime = -10f;
                onDoubleClicked?.Invoke();
                return;
            }

            lastClickTime = now;
        }
    }
}
