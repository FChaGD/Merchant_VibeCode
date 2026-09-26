using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Game.Core
{
    /// <summary>
    /// 포인터 클릭을 이벤트로 중계하는 범용 컴포넌트(PointerHoverRelay와 같은 성격). 특정 UI 로직을 모른다 -
    /// 인벤토리 팝업은 빈 영역 클릭 시 아이템 선택 해제에 쓴다. 자식이 클릭을 처리하면(아이템 뷰 등)
    /// EventSystem이 이 컴포넌트까지 올리지 않는다.
    /// </summary>
    public class PointerClickRelay : MonoBehaviour, IPointerClickHandler
    {
        public event Action Clicked;

        public void OnPointerClick(PointerEventData eventData) => Clicked?.Invoke();
    }
}
