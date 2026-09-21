using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Game.Core
{
    /// <summary>
    /// 마우스 호버 진입/이탈을 이벤트로 중계하는 범용 컴포넌트. 특정 UI 로직을 모른다 -
    /// 툴팁을 띄우는 쪽(PlayerCurrencyHudController 등)이 이 이벤트를 구독해 자신의 표시 로직을 수행한다.
    /// </summary>
    public class PointerHoverRelay : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public event Action PointerEntered;
        public event Action PointerExited;

        public void OnPointerEnter(PointerEventData eventData) => PointerEntered?.Invoke();
        public void OnPointerExit(PointerEventData eventData) => PointerExited?.Invoke();
    }
}
