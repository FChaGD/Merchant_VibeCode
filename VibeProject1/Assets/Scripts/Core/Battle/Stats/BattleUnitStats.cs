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

        public BattleUnitStats(
            float maxHp, float attack, float defense, float moveSpeed, float attackInterval, float range,
            float hpRegenPerSecond = 0f)
        {
            MaxHp = maxHp;
            Attack = attack;
            Defense = defense;
            MoveSpeed = moveSpeed;
            AttackInterval = attackInterval;
            Range = range;
            HpRegenPerSecond = hpRegenPerSecond;
        }
    }
}
