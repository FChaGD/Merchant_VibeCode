using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    public interface IPopupLayerGate
    {
        /// <summary>
        /// 모달 팝업이 열려 있는 동안 hiddenOnModalPopup 레이어들을 숨긴다(Docs/설계/38번 §6). 씬이 다시
        /// 로드될 때마다 새 레이어로 다시 호출된다 - 이전 등록은 대체된다.
        /// </summary>
        void Register(IUIVisibilitySignal signal, IReadOnlyList<CanvasGroup> hiddenOnModalPopup);
    }
}
