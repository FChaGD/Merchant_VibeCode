using System;

namespace Game.Core
{
    /// <summary>
    /// Hub에서 퀘스트 보상이 임시 보관으로 가면 무역품 인벤토리 팝업을 연다(Docs/설계/107번 §8.3 Hub 발생 행, 2026-10-27 검진 2) - 팝업에 임시 보관 영역이 있어
    /// 그 자리에서 정리할 수 있다. 시설 화면·모달이 열려 있으면 닫힌 뒤 연다 - 시설 화면 진입 때 인벤토리 팝업을 숨기는 규칙과 부딪히지 않게.
    /// 보조 납품 목표처럼 Hub의 구매로 충족되는 즉시 보상이 이 경로다. Hub 로드마다 새로 만들고 Hub 언로드 때 Dispose한다(Field에서 반응하지 않게).
    /// </summary>
    public sealed class HubQuestRewardCleanup : IDisposable
    {
        private readonly IQuestRewardCleanupSource source;
        private readonly IUIVisibilitySignal visibility;
        private readonly Action openTradeGoodsPopup;

        public HubQuestRewardCleanup(IQuestRewardCleanupSource source, IUIVisibilitySignal visibility, Action openTradeGoodsPopup)
        {
            this.source = source;
            this.visibility = visibility;
            this.openTradeGoodsPopup = openTradeGoodsPopup;

            if (source != null) source.RewardStaged += TryRun;
            if (visibility != null)
            {
                visibility.ScreenOpenChanged += HandleClosed;
                visibility.ModalPopupOpenChanged += HandleClosed;
            }
        }

        public void Dispose()
        {
            if (source != null) source.RewardStaged -= TryRun;
            if (visibility != null)
            {
                visibility.ScreenOpenChanged -= HandleClosed;
                visibility.ModalPopupOpenChanged -= HandleClosed;
            }
        }

        private void HandleClosed(bool open)
        {
            if (!open) TryRun();
        }

        private void TryRun()
        {
            if (source == null || !source.HasPendingCleanup) return;
            if (visibility != null && (visibility.IsScreenOpen || visibility.IsModalPopupOpen)) return;
            source.ClearPendingCleanup();
            openTradeGoodsPopup?.Invoke();
        }
    }
}
