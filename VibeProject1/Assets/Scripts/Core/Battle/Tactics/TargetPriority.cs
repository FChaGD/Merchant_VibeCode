namespace Game.Core
{
    /// <summary>
    /// 역할군 축 - 인식된 적 중 실제 공격 대상을 고르는 기준. 값 하나를 모든 역할군이 공유하지만,
    /// 역할군마다 유효한 부분집합만 RoleGroupTacticsCatalogAsset에서 허용한다(Docs/기획/12번 §3.1).
    /// 명시적 정수값 = 데이터 테이블 Id(Docs/설계/18번 §3), 0은 비워둔다.
    /// </summary>
    public enum TargetPriority
    {
        Nearest = 1,
        // 전열 - 아군 대형 깊이 침투한 적 우선.
        DeepestPenetration = 2,
        // 전열 - HP 비율이 가장 높은 적 우선("아직 공격받지 않은 적"의 근사, Docs/설계/12번 §3.3-1).
        HighestHpRatio = 3,
        // 원거리딜러 - 공격력이 가장 높은 적 우선(저격).
        HighestAttack = 4,
        // 원거리딜러 - 체력이 가장 낮은 적 우선(마무리).
        LowestHp = 5,
    }
}
