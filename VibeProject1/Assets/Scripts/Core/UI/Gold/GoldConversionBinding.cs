using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 골드 변환 모달 바인딩(설계 83번 §6.2) - 모달 팝업 채널에 등록한다: 시설 화면 위에 열려도 시설 화면은 닫히지 않는다(공통 B-1).
    /// 의존성: 골드 보유·골드 상자 변환(필수). 없으면 HUD 클릭은 "등록되지 않은 패널" 경고만 낸다.
    /// </summary>
    internal sealed class GoldConversionBinding : HubScreenBinding<GoldConversionPanel>
    {
        protected override GoldConversionPanel Create(HubScreenContext context)
        {
            var holdings = context.Optional<IGoldHoldingsReader>();
            var converter = context.Optional<IGoldBoxConverter>();
            if (holdings == null || converter == null)
            {
                Debug.LogWarning($"골드 변환 모달에 필요한 {nameof(IGoldHoldingsReader)}/{nameof(IGoldBoxConverter)}가 연결되어 있지 않아 등록하지 못했다(Tools > Game > Build Bootstrap Scene).");
                return null;
            }

            if (!GoldConversionElements.TryBind(context.SceneUIRoot, out var elements)) return null;

            var panel = new GoldConversionPanel(elements, holdings, converter, context.UIManager);
            context.Panels.RegisterPopupPanel(panel);
            return panel;
        }
    }
}
