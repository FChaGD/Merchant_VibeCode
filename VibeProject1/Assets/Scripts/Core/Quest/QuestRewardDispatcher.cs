using System;
using System.Collections.Generic;
using System.Text;

namespace Game.Core
{
    /// <summary>
    /// 유닛 보상 지급(Docs/설계/107번 §7) - 계약만 둔다. 캐릭터 생성 규칙(기획 80 4-10)이 정해지면 구현체를 등록한다. 지금은 등록된 구현이 없어
    /// 유닛 보상은 경고 후 건너뛴다(사용자 결정 2026-10-27 - 로직 구조까지만).
    /// </summary>
    public interface IQuestUnitRewardGranter
    {
        bool Grant(string characterId);
    }

    /// <summary>
    /// 퀘스트 보상·실패 불이익 지급(Docs/설계/107번 §7). 의존성은 모두 선택 - 없는 종류만 경고 후 건너뛴다(보상 하나가 빠졌다고 완료 처리를 멈추지 않는다).
    /// 위약금은 개인 골드에서 가진 만큼만 뺀다(골드 상자는 건드리지 않음, 설계 §7) - 부족해도 실패 처리는 진행된다.
    /// </summary>
    public sealed class QuestRewardDispatcher
    {
        private readonly IGoldIncome gold;
        private readonly IReputationAdjuster reputation;
        private readonly QuestCargoService cargo;
        private readonly IQuestUnitRewardGranter units;
        private readonly IPlayerCurrencyWallet wallet;
        private readonly Action<string> warn;

        public QuestRewardDispatcher(IGoldIncome gold, IReputationAdjuster reputation, QuestCargoService cargo, IQuestUnitRewardGranter units, IPlayerCurrencyWallet wallet, Action<string> warn)
        {
            this.gold = gold;
            this.reputation = reputation;
            this.cargo = cargo;
            this.units = units;
            this.wallet = wallet;
            this.warn = warn ?? (_ => { });
        }

        /// <summary>removals = 지급 전에 빠질 물품(보고 납품) - 그 자리를 비운 채 판정한다.</summary>
        public bool CanReceiveItems(IEnumerable<QuestRewardDefinition> rewards, IReadOnlyList<(string, int)> removals = null)
        {
            var items = ItemList(rewards);
            if (items.Count == 0) return true;
            return cargo != null && cargo.CanFit(items, removals);
        }

        /// <summary>반환 = 임시 보관으로 간 아이템 수. 보고형은 호출자가 CanReceiveItems로 먼저 막으므로 0이다.</summary>
        public int Grant(IEnumerable<QuestRewardDefinition> rewards)
        {
            var staged = 0;
            foreach (var r in rewards)
            {
                switch (r.Type)
                {
                    case QuestRewardType.Gold:
                        if (gold != null) gold.Earn(r.Amount);
                        else warn("골드 수입 계약이 없어 골드 보상을 건너뛴다.");
                        break;
                    case QuestRewardType.Reputation:
                        if (reputation != null) reputation.Adjust(r.Amount);
                        else warn("평판 계약이 없어 평판 보상을 건너뛴다.");
                        break;
                    case QuestRewardType.Item:
                        if (cargo != null) staged += cargo.GiveOrStage(r.ItemId, r.Amount);
                        else warn("무역품 인벤토리가 없어 아이템 보상을 건너뛴다.");
                        break;
                    case QuestRewardType.Unit:
                        if (units == null || !units.Grant(r.ItemId)) warn($"유닛 보상 '{r.ItemId}'를 지급할 구현이 없어 건너뛴다(캐릭터 생성 규칙 이후).");
                        break;
                }
            }
            return staged;
        }

        public void ApplyFailurePenalty(QuestDefinition def)
        {
            if (def.FailReputation != 0)
            {
                if (reputation != null) reputation.Adjust(def.FailReputation);
                else warn("평판 계약이 없어 실패 평판 감소를 건너뛴다.");
            }

            if (def.FailGold > 0)
            {
                if (wallet == null)
                {
                    warn("지갑이 없어 위약금을 건너뛴다.");
                    return;
                }
                var amount = Math.Min(def.FailGold, wallet.CurrentAmount);
                if (amount > 0) wallet.TrySpend(amount);
            }
        }

        /// <summary>보상 문구 - "골드 300, 평판 +5, 약초 ×2". 없으면 "없음".</summary>
        public static string Describe(IEnumerable<QuestRewardDefinition> rewards, Func<string, string> itemName)
        {
            var builder = new StringBuilder();
            foreach (var r in rewards)
            {
                if (builder.Length > 0) builder.Append(", ");
                switch (r.Type)
                {
                    case QuestRewardType.Gold: builder.Append($"골드 {r.Amount:N0}"); break;
                    case QuestRewardType.Reputation: builder.Append(r.Amount >= 0 ? $"평판 +{r.Amount}" : $"평판 {r.Amount}"); break;
                    case QuestRewardType.Item: builder.Append(r.Amount > 1 ? $"{itemName(r.ItemId)} ×{r.Amount}" : itemName(r.ItemId)); break;
                    case QuestRewardType.Unit: builder.Append("유닛 1명"); break;
                }
            }
            return builder.Length > 0 ? builder.ToString() : "없음";
        }

        private static List<(string, int)> ItemList(IEnumerable<QuestRewardDefinition> rewards)
        {
            var list = new List<(string, int)>();
            foreach (var r in rewards)
            {
                if (r.Type == QuestRewardType.Item && r.Amount > 0) list.Add((r.ItemId, r.Amount));
            }
            return list;
        }
    }
}
