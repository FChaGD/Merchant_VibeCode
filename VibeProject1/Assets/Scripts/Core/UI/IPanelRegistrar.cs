namespace Game.Core
{
    /// <summary>
    /// IUIPanel을 패널 호스트(UIManager·배틀 테스트 호스트)에 등록하는 권한만 노출한다(ISP) - Open/Close만 필요한 일반 소비자는
    /// IPanelNavigator만 의존하고, 씬별 UI 배선 담당(IContentSceneUIWiring 구현체)만 이 인터페이스로 패널을 등록한다.
    /// 종류를 인자로 받지 않고 메서드를 나눈 이유: 호출부에서 종류가 이름으로 드러나 잘못된 지정을
    /// 리뷰로 바로 볼 수 있다(Docs/설계/38번 §4.1).
    /// </summary>
    public interface IPanelRegistrar
    {
        /// <summary>화면 depth 전환 패널(마을 카테고리 depth).</summary>
        void RegisterDepthPanel(IUIPanel panel);

        /// <summary>
        /// 시설 화면(무역품 구매·용병·마구간·숙소·휴식 진행·상인조합, Docs/Refactor/2026-10-26_공통.md B-1). 화면을 통째로 바꾸는 개별 화면이라
        /// 모달과 채널을 나눴다 - 같은 채널이면 골드 변환 같은 모달이 열릴 때 시설 화면이 닫혔다 다시 열린다.
        /// </summary>
        void RegisterScreenPanel(IUIPanel panel);

        /// <summary>모달 팝업(상행 준비/상단 배치/방향성 지시/골드 변환 등) - 열리면 유지 대상 외 UI가 숨는다.</summary>
        void RegisterPopupPanel(IUIPanel panel);
    }
}
