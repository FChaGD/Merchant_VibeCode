using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace Game.Core
{
    /// <summary>
    /// 개인 골드만 관리하는 저수준 저장소(Docs/설계/83번 §3.1). 적재 골드·소지 가능량은 이 클래스가 모른다 - 골드 보유 서비스가
    /// 이 지갑과 교역품 저장소를 함께 읽어 계산한다. 획득 상한은 폐기됐고(기획 82번 C4), 출발 시 버림이 사실상의 상한 역할을 한다.
    /// </summary>
    public class InMemoryPlayerCurrencyWallet : MonoBehaviour, IPlayerCurrencyWallet, IManagedComponent
    {
        // 개인 소유 가능량(기획 82번 A1).
        [SerializeField] private int personalLimit = 500;
        // 시작 금액(기획 82번 A4) - 검증용 10,000. 예전 상한 겸 시작 금액(basicCapacity)의 저장값을 이어받는다.
        [FormerlySerializedAs("basicCapacity")]
        [SerializeField] private int startingAmount = 10000;

        private int currentAmount;

        public int CurrentAmount => currentAmount;
        public int PersonalLimit => personalLimit;
        public event Action<int> OnAmountChanged;

        public void RegisterSelf(IDependencyRegistrar registrar)
        {
            registrar.Register<IPlayerCurrencyWallet>(this);
        }

        public void ResolveDependencies(IDependencyResolver registrar)
        {
            currentAmount = startingAmount;
        }

        public int Add(int amount)
        {
            if (amount <= 0) return 0;

            currentAmount += amount;
            OnAmountChanged?.Invoke(currentAmount);
            return amount;
        }

        public bool TrySpend(int amount)
        {
            if (amount > currentAmount) return false;

            currentAmount -= amount;
            OnAmountChanged?.Invoke(currentAmount);
            return true;
        }
    }
}
