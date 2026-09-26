using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Game.Core
{
    /// <summary>
    /// 창의 제목 줄에 부착해 창 전체(window)를 드래그로 옮긴다. 창이 부모 영역(화면) 밖으로 나가지 않게
    /// 클램프한다 - 닫기 버튼이 화면 밖으로 사라지는 상황을 막는다(Docs/기획/39번 §3.8). 위치 보관은
    /// 하지 않고 이동이 끝날 때 Moved로 알리기만 한다 - 보관 주체(PopupWindowPositionStore)는 소비자가 정한다.
    /// 인벤토리 외 팝업에도 쓸 수 있는 범용 컴포넌트라 Common에 둔다.
    /// </summary>
    public class DraggableWindow : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [SerializeField] private RectTransform window;

        private Canvas canvas;

        public event Action<Vector2> Moved;

        private RectTransform Window => window != null ? window : transform.parent as RectTransform;

        public Vector2 Position => Window.anchoredPosition;

        public void SetPosition(Vector2 anchoredPosition)
        {
            Window.anchoredPosition = anchoredPosition;
            ClampToParent();
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            canvas = GetComponentInParent<Canvas>()?.rootCanvas;
        }

        public void OnDrag(PointerEventData eventData)
        {
            var scale = canvas != null && canvas.scaleFactor > 0f ? canvas.scaleFactor : 1f;
            Window.anchoredPosition += eventData.delta / scale;
            ClampToParent();
        }

        public void OnEndDrag(PointerEventData eventData) => Moved?.Invoke(Window.anchoredPosition);

        // 창의 네 모서리를 부모 좌표로 옮겨 부모 사각형을 벗어난 만큼 되돌린다 - 앵커 설정과 무관하게 동작한다.
        private void ClampToParent()
        {
            var target = Window;
            if (target.parent is not RectTransform parent) return;

            var corners = new Vector3[4];
            target.GetWorldCorners(corners);
            var min = (Vector2)parent.InverseTransformPoint(corners[0]);
            var max = (Vector2)parent.InverseTransformPoint(corners[2]);
            var bounds = parent.rect;

            var offset = Vector2.zero;
            if (min.x < bounds.xMin) offset.x = bounds.xMin - min.x;
            else if (max.x > bounds.xMax) offset.x = bounds.xMax - max.x;
            if (min.y < bounds.yMin) offset.y = bounds.yMin - min.y;
            else if (max.y > bounds.yMax) offset.y = bounds.yMax - max.y;

            target.anchoredPosition += offset;
        }
    }
}
