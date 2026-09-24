using System;

namespace Game.Core
{
    /// <summary>
    /// UI 관리 두 축의 상태 신호(Docs/설계/38번 §4.3). 화면 요소를 숨기고 보이는 쪽(HubUIController,
    /// HubPopupLayerGate)은 조회만 필요하므로 패널 열기/닫기가 포함된 IUIManager와 분리했다(ISP).
    /// </summary>
    public interface IUIVisibilitySignal
    {
        /// <summary>depth 패널이 하나도 열려 있지 않음(루트 depth).</summary>
        bool IsAtRootDepth { get; }
        event Action<bool> RootDepthChanged;

        /// <summary>모달 팝업(상행 준비/상단 배치/방향성 지시 등)이 열려 있음. 비모달(인벤토리) 팝업은 포함하지 않는다.</summary>
        bool IsModalPopupOpen { get; }
        event Action<bool> ModalPopupOpenChanged;
    }
}
