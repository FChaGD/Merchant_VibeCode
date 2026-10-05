using System;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 기획 §4/§9 - Wagon/Facility. 이동/공격하지 않고 피해만 받는다. IBattleCombatant가 아니라
    /// IDamageable만 구현한다 - Tick(이동/공격) 계약을 억지로 채울 필요가 없다(LSP).
    /// PartyMorale을 받지 않는다 - 기획 §7.2는 Character 유닛의 손실만 사기를 깎는다고 정의했다.
    /// 파괴가 곧 패배는 아니다 - 마차 전부 파괴, 또는 캐릭터·시설 전부 상실일 때만 패배다(Docs/기획/73번, BattleDefeatRule).
    /// </summary>
    public class BattleProtectedUnit : IDamageable
    {
        public Vector2 Position { get; }
        public bool IsAlive => currentHp > 0f;
        public float Defense => 0f; // 기획 §4: 방어력 해당 없음(N/A)
        public float Attack => 0f; // 공격력 해당 없음(N/A) - 이동/공격하지 않는 대상.
        public float Range => 0f; // 사거리 해당 없음(N/A) - Attack과 같은 이유.
        public float MaxHp { get; }
        public float CurrentHp => currentHp;
        // 정비창 팔레트에서 이미 쓰던 아이콘(마차=삼각형/시설=원형)을 그대로 - 뷰가 별도 도형을
        // 새로 만들지 않고 이 아이콘을 재사용한다.
        public Sprite Icon { get; }
        // 크기의 절반(월드 유닛). 뷰 표시 크기와 장애물 회피 반경의 공통 근거(Docs/설계/72번 §3.1) - 크기는 마차·시설만
        // 가지므로 IDamageable이 아니라 여기 둔다.
        public float HalfSize { get; }
        public ProtectedUnitKind Kind { get; }
        // 로스터 Id(= 상단 물류품 섹션 Id, 설계 64번 §3.2). 화물 원장이 "어느 마차의 화물인지" 찾는 키(설계 79번 §3.1).
        // 화물과 무관한 생성 경로(테스트 등)는 null.
        public string UnitId { get; }
        // 마차는 파괴돼도 잔해로 남아 길을 막는다(기획 77번 §4-9). 시설은 지금처럼 파괴되면 장애물에서 빠진다.
        public bool BlocksMovement => Kind == ProtectedUnitKind.Wagon || IsAlive;
        public event Action OnDied;
        public event Action<float> OnDamaged;
        // 체력 감소 직후·파괴 판정 전에 발생 - 마지막 일격도 피격 판정(화물 손실)을 먼저 받고 그 뒤 파괴 판정이 남은
        // 물품에 적용돼야 한다(기획 77번 §4-1·§4-6, 설계 79번 §3.1). OnDamaged와 따로 두는 이유는 공격자(도난 주체)를 넘겨야 해서다.
        public event Action<BattleProtectedUnit, IBattleCombatant> OnHitBy;

        private float currentHp;

        public BattleProtectedUnit(Vector2 position, float maxHp, Sprite icon, float halfSize, ProtectedUnitKind kind, string unitId = null)
        {
            Position = position;
            UnitId = unitId;
            HalfSize = halfSize;
            Kind = kind;
            MaxHp = maxHp;
            Icon = icon;
            currentHp = maxHp;
        }

        // attacker는 반격에 쓰지 않는다(이동/공격하지 않는 대상) - 화물 원장이 도난 주체로 쓰도록 OnHitBy로 넘기기만 한다.
        public void TakeDamage(float amount, IBattleCombatant attacker)
        {
            if (!IsAlive) return;
            currentHp = Mathf.Max(0f, currentHp - amount);
            OnDamaged?.Invoke(amount);
            OnHitBy?.Invoke(this, attacker);
            if (!IsAlive)
            {
                OnDied?.Invoke();
            }
        }
    }
}
