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

        public BattleUnitStats(float maxHp, float attack, float defense, float moveSpeed, float attackInterval, float range)
        {
            MaxHp = maxHp;
            Attack = attack;
            Defense = defense;
            MoveSpeed = moveSpeed;
            AttackInterval = attackInterval;
            Range = range;
        }
    }
}
