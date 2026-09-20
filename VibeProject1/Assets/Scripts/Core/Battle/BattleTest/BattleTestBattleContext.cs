namespace Game.Core
{
    /// <summary>
    /// 전투 유닛 생성에 필요한, BuildSimulation() 시점에 정해지는 값 묶음. 전투 도중 소환(Spawn)이 시작 시점과
    /// 같은 조율자 인스턴스를 재사용해야 해서 규칙 클래스가 한 번 만들어 들고 있는다 - 진영 정의 객체가
    /// 규칙 클래스를 역참조하지 않고 이 값만 읽도록 하려는 구조다(Docs/설계/28번).
    /// </summary>
    public class BattleTestBattleContext
    {
        public float FleeTravelDistance { get; }
        public float StandardActivityRadius { get; }
        public float FieldRadius { get; }
        public FrontlineFormationCoordinator FrontlineCoordinator { get; }
        public RangedSurroundCoordinator RangedSurroundCoordinator { get; }
        // 방향성 지시 리더가 없으면 null - 아군 전술 행동을 부여하지 않는다.
        public IUnitTacticsProfileResolver TacticsProfileResolver { get; }

        public BattleTestBattleContext(
            float fleeTravelDistance, float standardActivityRadius, float fieldRadius,
            FrontlineFormationCoordinator frontlineCoordinator, RangedSurroundCoordinator rangedSurroundCoordinator,
            IUnitTacticsProfileResolver tacticsProfileResolver)
        {
            FleeTravelDistance = fleeTravelDistance;
            StandardActivityRadius = standardActivityRadius;
            FieldRadius = fieldRadius;
            FrontlineCoordinator = frontlineCoordinator;
            RangedSurroundCoordinator = rangedSurroundCoordinator;
            TacticsProfileResolver = tacticsProfileResolver;
        }
    }
}
