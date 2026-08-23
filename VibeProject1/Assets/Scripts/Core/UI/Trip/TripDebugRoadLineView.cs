using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.Core
{
    /// <summary>
    /// 지도 콘텐츠 좌표계에 그려지는 도시 간 연결선(또는 드래그 중 임시 미리보기 선). 얇은 Image를
    /// 시작점에 놓고 끝점 방향으로 회전·늘려서 표현한다 - 좌표를 전부 부모(content)의 로컬 공간에서만
    /// 다루므로 지도가 팬/줌돼도 부모 스케일을 그대로 물려받아 별도 변환 없이 함께 움직인다.
    /// 더블클릭(연속 클릭)으로 삭제를 요청한다. SetRaycastTarget은 드래그 중 임시 미리보기 선으로 쓸 때
    /// 필요하다 - 레이캐스트를 막아두지 않으면 커서를 따라가는 선 자신이 그 아래 도시 아이콘에 대한
    /// 드롭 판정(레이캐스트)을 가로채 연결이 생성되지 않는 문제가 있었다(FormationUnitIconView의
    /// 드래그 고스트가 raycastTarget을 끄는 것과 동일한 이유).
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class TripDebugRoadLineView : MonoBehaviour, IPointerClickHandler
    {
        private const float DoubleClickThresholdSeconds = 0.3f;

        [SerializeField] private Image lineImage;

        private RectTransform rectTransform;
        private Action onDoubleClicked;
        private float lastClickTime = -10f;

        private void Awake()
        {
            rectTransform = (RectTransform)transform;
        }

        public void SetRaycastTarget(bool raycastTarget)
        {
            if (lineImage != null)
            {
                lineImage.raycastTarget = raycastTarget;
            }
        }

        public void Initialize(Action doubleClicked)
        {
            onDoubleClicked = doubleClicked;
        }

        public void SetEndpoints(Vector2 start, Vector2 end)
        {
            var delta = end - start;
            rectTransform.anchoredPosition = start;

            var size = rectTransform.sizeDelta;
            size.x = delta.magnitude;
            rectTransform.sizeDelta = size;

            var angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
            rectTransform.localRotation = Quaternion.Euler(0f, 0f, angle);
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
