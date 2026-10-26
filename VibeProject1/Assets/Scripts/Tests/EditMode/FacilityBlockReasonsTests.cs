using NUnit.Framework;

namespace Game.Core.Tests
{
    /// <summary>
    /// 시설 화면 버튼 비활성 사유 문구(Docs/Refactor/2026-10-26_공통.md D-1). 사유 문구 줄을 툴팁으로 옮기면서 판정 → 문구를 순수 로직으로 뺐다 -
    /// 문구는 현행 그대로, 버튼 활성 = 사유 없음(null).
    /// </summary>
    public class FacilityBlockReasonsTests
    {
        [Test]
        public void ShopPurchase()
        {
            Assert.IsNull(FacilityBlockReasons.For(ShopPurchaseCheck.Available));
            Assert.AreEqual("품절", FacilityBlockReasons.For(ShopPurchaseCheck.SoldOut));
            Assert.AreEqual("재화가 부족합니다.", FacilityBlockReasons.For(ShopPurchaseCheck.InsufficientFunds));
            Assert.AreEqual("상단 물류품에 놓을 공간이 없습니다. 마차 공간을 정리해주세요.", FacilityBlockReasons.For(ShopPurchaseCheck.NoSpace));
        }

        [Test]
        public void MercenaryHire()
        {
            Assert.IsNull(FacilityBlockReasons.For(MercenaryHireCheck.Available));
            Assert.AreEqual("재화가 부족합니다.", FacilityBlockReasons.For(MercenaryHireCheck.InsufficientFunds));
            Assert.AreEqual("이미 고용한 용병입니다.", FacilityBlockReasons.For(MercenaryHireCheck.AlreadyHired));
        }

        [Test]
        public void CaravanAssetPurchase()
        {
            Assert.IsNull(FacilityBlockReasons.For(CaravanAssetPurchaseCheck.Available));
            Assert.AreEqual("품절", FacilityBlockReasons.For(CaravanAssetPurchaseCheck.SoldOut));
            Assert.AreEqual("재화가 부족합니다.", FacilityBlockReasons.For(CaravanAssetPurchaseCheck.InsufficientFunds));
        }

        [Test]
        public void CaravanAssetSale()
        {
            Assert.IsNull(FacilityBlockReasons.For(CaravanAssetSaleCheck.Available));
            Assert.AreEqual("화물이 실린 마차는 매각할 수 없습니다.", FacilityBlockReasons.For(CaravanAssetSaleCheck.HasCargo));
            Assert.AreEqual("마지막 마차는 매각할 수 없습니다.", FacilityBlockReasons.For(CaravanAssetSaleCheck.LastWagon));
            Assert.IsNull(FacilityBlockReasons.For(CaravanAssetSaleCheck.NotOwned));
        }

        [Test]
        public void Repair()
        {
            Assert.IsNull(FacilityBlockReasons.ForRepair(CaravanAssetRepairCheck.Available));
            Assert.AreEqual("수리할 필요 없음", FacilityBlockReasons.ForRepair(CaravanAssetRepairCheck.NotDamaged));
            Assert.AreEqual("골드 부족", FacilityBlockReasons.ForRepair(CaravanAssetRepairCheck.InsufficientFunds));
            Assert.AreEqual("수리 불가능 개체", FacilityBlockReasons.ForRepair(null), "수리 서비스 없음(사용자 결정 2026-10-26)");
        }

        [Test]
        public void RepairAll_NotDamagedHasNoReason()
        {
            Assert.IsNull(FacilityBlockReasons.ForRepairAll(CaravanAssetRepairCheck.NotDamaged), "손상 없음은 사유 없이 비활성(기획 90번 §4-16)");
            Assert.AreEqual("골드 부족", FacilityBlockReasons.ForRepairAll(CaravanAssetRepairCheck.InsufficientFunds));
        }
    }
}
