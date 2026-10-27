using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 용병단 접촉 화면 바인딩(Docs/설계/54번 §8.4) - 무역품 구매 화면과 같은 등록 방식(시설 화면 채널).
    /// 의존성: 골드 지출·고용 후보·고용 명단·캐릭터 카탈로그(필수), 직업 아이콘(선택 - 없으면 아이콘 없이 표시).
    /// </summary>
    internal sealed class MercenaryContactScreenBinding : HubScreenBinding<MercenaryContactPanel>
    {
        protected override MercenaryContactPanel Create(HubScreenContext context)
        {
            var gold = context.Optional<IGoldSpender>();
            var candidateReader = context.Optional<IMercenaryCandidateReader>();
            var roster = context.Optional<IHiredCharacterRoster>();
            var catalog = context.Optional<ICharacterCatalogReader>();
            if (gold == null || candidateReader == null || roster == null || catalog == null)
            {
                Debug.LogWarning($"용병단 접촉 화면에 필요한 {nameof(IGoldSpender)}/{nameof(IMercenaryCandidateReader)}/{nameof(IHiredCharacterRoster)}/{nameof(ICharacterCatalogReader)}가 연결되어 있지 않아 등록하지 못했다(Tools > Game > Build Bootstrap Scene).");
                return null;
            }

            if (!RosterShopElements.TryBind(context.SceneUIRoot, RosterShopUIElementIds.MercenaryContactPrefix, out var elements)) return null;

            var hireTooltip = RosterShopTooltips.Register(context, elements, RosterShopUIElementIds.MercenaryContactPrefix);
            var panel = new MercenaryContactPanel(elements, gold, candidateReader, roster, catalog, context.Optional<IMercenaryClassIconReader>(), context.CurrentLocation, context.UIManager, hireTooltip);
            context.Panels.RegisterScreenPanel(panel);
            return panel;
        }
    }
}
