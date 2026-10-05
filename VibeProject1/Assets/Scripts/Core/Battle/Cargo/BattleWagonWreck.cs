using System;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 파괴된 마차 자리에 놓이는 잔해 표적(Docs/기획/77번 §4-10~13, 설계 79번 §3.3). 파괴된 마차 객체와 별도로 두는 이유 -
    /// 마차의 IsAlive는 패배 판정·방진 보호 후보·표적 선정이 모두 "파괴됨"으로 읽어야 하는데, 잔해는 "파괴됐지만 표적 가능"이라
    /// 한 값으로 표현할 수 없다(§15-1). IsAlive = 무방비 물품이 남아 있음이라 기존 표적 선택 규칙이 손대지 않고 잔해를
    /// 후보로 보거나 빼낸다. 체력이 없어 OnDamaged는 발생시키지 않는다. 생성은 원장만 한다.
    /// </summary>
    public sealed class BattleWagonWreck : IDamageable
    {
        private readonly BattleCargoLedger ledger;

        // 잔해가 놓인 원래 마차 - 장애물 회피의 "공격 대상은 피하지 않는다"를 잔해에도 적용하려면 같은 자리의 마차를 알아야 한다.
        public BattleProtectedUnit Wagon { get; }
        public Vector2 Position => Wagon.Position;
        public bool IsAlive => ledger.HasUnprotected(Wagon.UnitId);
        public float Defense => 0f;
        public float MaxHp => 0f;
        public float CurrentHp => 0f;
        public float Attack => 0f;
        public float Range => 0f;
        // 잔해 전용 뷰는 없다 - 연두색 잔해는 파괴된 마차 뷰가 그대로 보여준다.
        public Sprite Icon => null;
        public event Action OnDied;
        // 체력이 없어 발생시키지 않는다(IDamageable 계약상 선언만).
        public event Action<float> OnDamaged { add { } remove { } }

        internal BattleWagonWreck(BattleProtectedUnit wagon, BattleCargoLedger ledger)
        {
            Wagon = wagon;
            this.ledger = ledger;
        }

        // 피해량은 쓰지 않는다 - 잔해 공격은 확률로 무방비 물품 1개를 가져가는 것뿐이다(기획 77번 §4-11).
        // 마지막 물품이 빠지면 OnDied로 표적 해제를 알린다 - 적이 빈 잔해에 묶이지 않게.
        public void TakeDamage(float amount, IBattleCombatant attacker)
        {
            if (!IsAlive) return;
            if (ledger.TryStealFromWreck(Wagon.UnitId, attacker) && !IsAlive)
            {
                OnDied?.Invoke();
            }
        }
    }
}
