using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Game.Core
{
    /// <summary>
    /// 대화 중 빈 곳 클릭(설계 88 §5.2). 이동 뷰 배경 바로 위·버튼들보다 뒤 형제라 버튼은 그대로 눌리고 빈 곳만 여기로 온다.
    /// 모달 화면이 열리면 그 화면이 위라 닿지 않는다. 대화 중에만 켜진다(FieldDialogueView가 켜고 끈다).
    /// </summary>
    public class FieldDialogueClickCatcher : MonoBehaviour, IPointerClickHandler
    {
        public event Action Clicked;
        public void OnPointerClick(PointerEventData eventData) => Clicked?.Invoke();
    }
}
