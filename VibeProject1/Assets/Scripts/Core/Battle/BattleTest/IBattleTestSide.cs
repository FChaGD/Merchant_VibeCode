namespace Game.Core
{
    /// <summary>
    /// 배틀 테스트 씬의 진영(아군/적) 하나가 갖는 차이를 모은 정의 - 로스터, 사기/파동 인스턴스, 기본 스탯
    /// 조회, 유닛 생성 방식. 규칙 클래스는 진영 bool로 구현체를 골라 공통 절차만 수행하고, 진영별 분기는 이
    /// 인터페이스 구현체에만 남긴다(Docs/설계/28번, 방향성 지시의 축별 조합과 같은 패턴).
    /// </summary>
    public interface IBattleTestSide
    {
        bool IsAlly { get; }
        BattleTestRoster Roster { get; }

        // BeginBattle() 전(첫 전투 시작 전)에는 null이다.
        PartyMorale Morale { get; }
        MoraleWaveCoordinator WaveCoordinator { get; }

        /// <summary>전투를 새로 구성할 때마다 호출한다 - 사기/파동 인스턴스를 새로 만든다. 전투 중 소환 유닛은 이 인스턴스를 그대로 재사용해야 이미 싸우던 유닛과 같은 사기 체계를 갖는다.</summary>
        void BeginBattle(float fieldRadius);

        BattleUnitStats GetDefaultStats(BattleTestUnitKind kind);

        /// <summary>세팅 단계 미리보기 전용 - 절대 Tick되지 않으므로 더미 사기/파동을 써도 무해하다.</summary>
        BattleCharacterUnit CreatePreviewUnit(BattleTestRoster.Entry entry);

        BattleCharacterUnit CreateBattleUnit(BattleTestRoster.Entry entry, BattleTestBattleContext context);
    }
}
