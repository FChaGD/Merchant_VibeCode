namespace Game.Core
{
    public readonly struct BattleUnitStats
    {
        public float MaxHp { get; }
        public float Attack { get; }
        public float Defense { get; }
        public float MoveSpeed { get; }
        public float AttackInterval { get; }
        public float Range { get; }
        // 괴수 타입 전용 특성(기획 08번 §13.1) - 나머지 전부 기본값 0(재생 없음)이라 호출부 대부분은
        // 신경 쓸 필요가 없다.
        public float HpRegenPerSecond { get; }
        // 전투 뷰가 적 타입별로 다른 도형(약탈자=사각형/괴수=삼각형/적대자=원)을 보여주기 위한 태그 -
        // 아군 스탯 생성부는 이 값을 설정하지 않아 항상 null(N/A), HpRegenPerSecond와 같은 패턴.
        public EnemyType? EnemyType { get; }
        // 사기 동기화 속도(기획 08번 §7.4, 설계 14번 §7) - 보통 티어 기준값이며 저사기 시
        // MoraleTuning.LowTierSyncMultiplier가 곱해진다. HpRegenPerSecond/EnemyType과 달리 타입/직업별로
        // 반드시 의도적으로 지정해야 하는 값이라 기본값을 두지 않는다.
        public float MoraleSyncRate { get; }

        public BattleUnitStats(
            float maxHp, float attack, float defense, float moveSpeed, float attackInterval, float range,
            float moraleSyncRate, float hpRegenPerSecond = 0f, EnemyType? enemyType = null)
        {
            MaxHp = maxHp;
            Attack = attack;
            Defense = defense;
            MoveSpeed = moveSpeed;
            AttackInterval = attackInterval;
            Range = range;
            MoraleSyncRate = moraleSyncRate;
            HpRegenPerSecond = hpRegenPerSecond;
            EnemyType = enemyType;
        }
    }
}
