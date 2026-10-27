using System;
using System.Collections.Generic;

namespace Game.Core
{
    public enum BattleOutcome
    {
        Victory,
        Defeat
    }

    /// <summary>
    /// 전투 결과. 패배 원인·화물 보고서는 전투 뒤 정산이 쓴다(설계 79번 §4.2). 1인자 생성자는 화물·마차가 없는 규칙
    /// (배틀 테스트 씬·Placeholder)용으로 남긴다 - 원인 None, 보고서 Empty. 마차·시설 기록은 내구도 정산이 쓴다(설계 91번 §4.2).
    /// </summary>
    public readonly struct BattleResult
    {
        private readonly BattleCargoReport cargoReport;
        private readonly IReadOnlyList<ProtectedUnitRecord> protectedUnits;

        public BattleOutcome Outcome { get; }
        // 승리면 None.
        public BattleDefeatCause DefeatCause { get; }
        // default(BattleResult)처럼 보고서가 비어 있어도 소비자가 null을 다루지 않게 Empty를 돌려준다.
        public BattleCargoReport CargoReport => cargoReport ?? BattleCargoReport.Empty;
        // 마차·시설 기록(설계 91번 §4.2). 배틀 테스트 씬·Placeholder는 비어 있다.
        public IReadOnlyList<ProtectedUnitRecord> ProtectedUnits => protectedUnits ?? Array.Empty<ProtectedUnitRecord>();

        public BattleResult(BattleOutcome outcome)
            : this(outcome, BattleDefeatCause.None, BattleCargoReport.Empty)
        {
        }

        public BattleResult(BattleOutcome outcome, BattleDefeatCause defeatCause, BattleCargoReport cargoReport)
            : this(outcome, defeatCause, cargoReport, null)
        {
        }

        public BattleResult(BattleOutcome outcome, BattleDefeatCause defeatCause, BattleCargoReport cargoReport, IReadOnlyList<ProtectedUnitRecord> protectedUnits)
            : this(outcome, defeatCause, cargoReport, protectedUnits, null)
        {
        }

        public BattleResult(BattleOutcome outcome, BattleDefeatCause defeatCause, BattleCargoReport cargoReport, IReadOnlyList<ProtectedUnitRecord> protectedUnits, string enemyType)
        {
            Outcome = outcome;
            DefeatCause = defeatCause;
            this.cargoReport = cargoReport;
            this.protectedUnits = protectedUnits;
            EnemyType = enemyType;
        }

        // 이번 전투의 적 종류(적 스탯 테이블 Id, 전투마다 한 종류 - 기획 08번 §13.1). 퀘스트 전투 승리 목표의 필터에 쓴다(설계 107번 §6.1).
        // 배틀 테스트 씬·Placeholder는 null.
        public string EnemyType { get; }
    }
}
