namespace Game.Core
{
    /// <summary>
    /// IUIPanel을 UIManager에 등록하는 권한만 노출한다(ISP) - Open/Close만 필요한 일반 소비자는 IUIManager만
    /// 의존하고, 씬별 UI 배선 담당(IContentSceneUIWiring 구현체)만 이 인터페이스로 패널을 등록한다.
    /// 종류를 인자로 받지 않고 메서드를 나눈 이유: 호출부에서 종류가 이름으로 드러나 잘못된 지정을
    /// 리뷰로 바로 볼 수 있다(Docs/설계/38번 §4.1).
    /// </summary>
    public interface IPanelRegistrar
    {
        /// <summary>화면 depth 전환 패널(마을 카테고리 depth, 후속 시설 화면).</summary>
        void RegisterDepthPanel(IUIPanel panel);

        /// <summary>모달 팝업(상행 준비/상단 배치/방향성 지시, 후속 범용 팝업) - 열리면 유지 대상 외 UI가 숨는다.</summary>
        void RegisterPopupPanel(IUIPanel panel);
    }
}
