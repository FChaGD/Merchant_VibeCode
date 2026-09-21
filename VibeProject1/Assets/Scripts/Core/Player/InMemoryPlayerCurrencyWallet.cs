using System;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 기본 소지 재화만 관리한다(Docs/기획/26번 §4) - 적재 재화(마차 연동)는 마차/인벤토리
    /// 시스템 설계 후 Capacity 계산에 합산 지점을 추가한다(Docs/설계/29번 §5 확장 지점).
    /// </summary>
    public class InMemoryPlayerCurrencyWallet : MonoBehaviour, IPlayerCurrencyWallet, IManagedComponent
    {
        // 23번 §3.1 기본 수치(잠정) - 가격 체계 정의 후 재검토 대상.
        [SerializeField] private int basicCapacity = 1000;

        private int currentAmount;

        public int CurrentAmount => currentAmount;
        public int Capacity => basicCapacity;
        public event Action<int> OnAmountChanged;

        public void RegisterSelf(IDependencyRegistrar registrar)
        {
            registrar.Register<IPlayerCurrencyWallet>(this);
        }

        public void ResolveDependencies(IDependencyResolver registrar)
        {
            // 게임 시작 시 상한만큼 가득 채운 상태로 시작(23번 §3.1). 다른 매니저에 대한 의존성은 없다.
            currentAmount = basicCapacity;
        }

        public int Add(int amount)
        {
            var applied = Mathf.Clamp(amount, 0, Capacity - currentAmount);
            if (applied <= 0) return 0;

            currentAmount += applied;
            OnAmountChanged?.Invoke(currentAmount);
            return applied;
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
