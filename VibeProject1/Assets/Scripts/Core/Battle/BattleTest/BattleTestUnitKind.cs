using System;

namespace Game.Core
{
    /// <summary>
    /// 배틀 테스트 씬에서 "어느 진영의 어떤 종류 유닛인가"를 하나로 묶은 식별자. 아군 종류(MercenaryClass)와
    /// 적 종류(EnemyType)가 서로 다른 enum이라 이를 받는 코드(로스터/소환 요청)가 진영마다 쌍으로
    /// 갈라졌던 것을 통합하기 위한 값이다. 배치된 개체 하나를 가리키는 로스터 항목 Id와는 별개다.
    /// enum↔정수 변환은 이 구조체의 팩토리/역변환에서만 일어나고, 진영이 맞지 않는 역변환은 예외로
    /// 조기 실패시켜 정수 캐스팅으로 잃은 타입 검사를 보완한다(Docs/설계/28번).
    /// </summary>
    public readonly struct BattleTestUnitKind
    {
        public bool IsAlly { get; }
        public int Value { get; }

        private BattleTestUnitKind(bool isAlly, int value)
        {
            IsAlly = isAlly;
            Value = value;
        }

        public static BattleTestUnitKind Ally(MercenaryClass unitClass) => new(true, (int)unitClass);

        public static BattleTestUnitKind Enemy(EnemyType type) => new(false, (int)type);

        public MercenaryClass ToMercenaryClass()
        {
            if (!IsAlly) throw new InvalidOperationException("적 종류를 MercenaryClass로 변환할 수 없다.");
            return (MercenaryClass)Value;
        }

        public EnemyType ToEnemyType()
        {
            if (IsAlly) throw new InvalidOperationException("아군 종류를 EnemyType으로 변환할 수 없다.");
            return (EnemyType)Value;
        }
    }
}
