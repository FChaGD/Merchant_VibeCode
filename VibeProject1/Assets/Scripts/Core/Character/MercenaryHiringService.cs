namespace Game.Core
{
    /// <summary>
    /// 캐릭터 고용 1건의 판정과 처리(Docs/설계/54번 §7.3). 재화 차감과 로스터 추가는 한쪽만 성공하면 재화가 사라지므로 한 연산으로
    /// 묶는다 - 차감 후 추가가 실패하면 환급한다(ShopPurchaseService와 같은 방식). 직업당 고용 상한은 없다(기획 80번 §3-10).
    /// 지출은 소유 골드 기준이고 개인 골드가 부족하면 골드 상자를 인출한다(기획 82번 C1·C3).
    /// </summary>
    public class MercenaryHiringService
    {
        private readonly IGoldSpender gold;
        private readonly IHiredCharacterRoster roster;

        public MercenaryHiringService(IGoldSpender gold, IHiredCharacterRoster roster)
        {
            this.gold = gold;
            this.roster = roster;
        }

        // 재화 부족을 먼저 알린다(무역품 구매와 같은 순서, 설계 50번 §11-2).
        public MercenaryHireCheck Evaluate(CharacterProfile candidate)
        {
            if (roster.IsHired(candidate.CharacterId)) return MercenaryHireCheck.AlreadyHired;
            if (!gold.CanAfford(candidate.HireCost)) return MercenaryHireCheck.InsufficientFunds;
            return MercenaryHireCheck.Available;
        }

        public bool TryHire(CharacterProfile candidate)
        {
            if (Evaluate(candidate) != MercenaryHireCheck.Available) return false;
            if (!gold.TrySpend(candidate.HireCost)) return false;

            if (!roster.TryAddHired(candidate.CharacterId))
            {
                gold.Refund(candidate.HireCost); // 로스터 추가 실패 롤백
                return false;
            }

            return true;
        }
    }
}
