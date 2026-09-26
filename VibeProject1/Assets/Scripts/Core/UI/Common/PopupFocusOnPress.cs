using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Core
{
    /// <summary>
    /// 같은 부모 아래의 창들 중 포인터로 누른 창을 맨 위(마지막 형제)로 올린다(Docs/설계/46번). UI 누르기 이벤트
    /// (IPointerDownHandler)는 레이캐스트에 맞은 요소부터 부모 방향으로 처음 처리하는 하나에만 전달돼, 버튼처럼
    /// 스스로 처리하는 하위 요소를 누르면 창 루트까지 오지 않는다 - 그래서 입력 시스템의 포인터 누르기 액션으로
    /// 누른 순간을 받고, 위치로 창을 판정한다. 겹친 창 중 하나만 반응해야 하므로 각 창이 스스로 판정하지 않고 이
    /// 객체가 창 목록 전체를 보고 결정한다. 인벤토리를 모르는 범용 동작이라 Common에 둔다.
    /// </summary>
    public sealed class PopupFocusOnPress : IDisposable
    {
        private const string PressBinding = "<Pointer>/press";

        private readonly InputAction pressAction;
        private readonly List<RectTransform> windows = new();
        private Camera eventCamera;

        public PopupFocusOnPress()
        {
            pressAction = new InputAction("FocusPopupOnPress", InputActionType.Button, PressBinding);
            pressAction.performed += _ => HandlePress();
            pressAction.Enable();
        }

        /// <summary>씬 로드마다 새로 만들어진 창 루트로 교체한다. 이전 씬의 창은 이미 파괴돼 판정에서 빠진다.</summary>
        public void Rebind(IEnumerable<RectTransform> windowRoots)
        {
            windows.Clear();
            windows.AddRange(windowRoots);
            eventCamera = null;

            // Screen Space - Overlay 캔버스는 화면 좌표를 그대로 쓰므로 카메라가 null이다.
            foreach (var window in windows)
            {
                var canvas = window != null ? window.GetComponentInParent<Canvas>()?.rootCanvas : null;
                if (canvas == null) continue;
                eventCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
                break;
            }
        }

        public void Dispose()
        {
            pressAction.Disable();
            pressAction.Dispose();
            windows.Clear();
        }

        /// <summary>
        /// 올릴 창을 고른다 - 활성 창을 현재 표시 순서의 위쪽(형제 순서가 뒤쪽)부터 확인해 포인터가 들어 있는 첫 창.
        /// 겹친 영역을 누르면 화면에 보이는 창이 선택되고, 어느 창에도 없으면 null. 입력과 분리해 테스트한다.
        /// </summary>
        public static RectTransform SelectWindowToFront(IEnumerable<RectTransform> windowRoots, Predicate<RectTransform> containsPointer)
        {
            RectTransform selected = null;
            var selectedOrder = int.MinValue;
            foreach (var window in windowRoots)
            {
                // 파괴된 창(Unity null)과 닫힌 창은 건너뛴다.
                if (window == null || !window.gameObject.activeInHierarchy) continue;

                var order = window.GetSiblingIndex();
                if (order > selectedOrder && containsPointer(window))
                {
                    selected = window;
                    selectedOrder = order;
                }
            }
            return selected;
        }

        private void HandlePress()
        {
            if (windows.Count == 0 || Pointer.current == null) return;

            var pointerPosition = Pointer.current.position.ReadValue();
            var target = SelectWindowToFront(windows, window => RectTransformUtility.RectangleContainsScreenPoint(window, pointerPosition, eventCamera));
            if (target != null && target.GetSiblingIndex() != target.parent.childCount - 1)
            {
                target.SetAsLastSibling();
            }
        }
    }
}
