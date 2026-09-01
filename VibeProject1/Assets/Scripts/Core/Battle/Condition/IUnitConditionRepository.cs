namespace Game.Core
{
    /// <summary>
    /// 상행 동안 보유 유닛(용병) 개체별 HP/사망 상태를 저장한다(설계 15번). IFormationUnit/IMercenaryUnit을
    /// 확장하지 않는 이유(ISP) - Formation UI는 전투 전용 가변 상태를 몰라도 되게 좁혀져 있다
    /// (IMercenaryUnit 요약 주석과 같은 원칙). unitId로 조회하는 별도 저장소로 분리해, 필요한 쪽만
    /// (전투 시작/종료 동기화, 정비창 팔레트 잔여수 계산) 참조한다.
    /// </summary>
    public interface IUnitConditionRepository
    {
        /// <summary>저장된 값이 없으면(=아직 이 상행에서 초기화되지 않음) false.</summary>
        bool TryGetCurrentHp(string unitId, out float currentHp);

        bool IsDead(string unitId);

        /// <summary>전투 종료 시 호출한다. died=true면 currentHp는 무시하고 사망 처리한다.</summary>
        void ApplyBattleResult(string unitId, float currentHp, bool died);

        /// <summary>상행 시작/종료 시 호출한다 - 로스터 전원을 각자 직업 기준 만피로 리셋하고 사망 상태도 해제한다.</summary>
        void ResetAllToFull();
    }
}
