using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Game.Core
{
    /// <summary>
    /// 드래그 이벤트를 델리게이트로 그대로 위임하는 최소 컴포넌트(FormationUnitIconView와 같은
    /// "이벤트 위임" 패턴) - 대열 범위 기즈모의 모서리 핸들 4개가 공용으로 쓴다. 각 핸들이 어느
    /// 모서리인지는 이 컴포넌트가 몰라도 된다(소비자가 SetHandler로 주입한 콜백이 판단).
    /// </summary>
    public class BattleTestDragHandle : MonoBehaviour, IBeginDragHandler, IDragHandler
    {
        private Action<PointerEventData> onDrag;

        public void SetHandler(Action<PointerEventData> handler) => onDrag = handler;

        // EventSystem이 pointerDrag를 잡으려면 IBeginDragHandler 구현체가 있어야 한다(FormationUnitIconView와
        // 같은 이유) - 이 핸들은 시작 시점에 할 일이 없어 실제 로직은 OnDrag에만 있다.
        public void OnBeginDrag(PointerEventData eventData) { }

        public void OnDrag(PointerEventData eventData) => onDrag?.Invoke(eventData);
    }
}
