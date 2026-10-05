using System;
using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>
    /// 전투 한 번의 화물 결과(불변, 설계 79번 §4.2). 전투 중에는 저장소를 건드리지 않고 이 보고서만 결과에 실어 보낸다 -
    /// 패배 손실처럼 결과 판정 뒤에야 확정되는 처리와 골드 상자 환급 규칙이 전투 중에 섞이지 않게 하기 위해서다(§15-2).
    /// 배틀 테스트 씬·마차 없는 전투는 Empty.
    /// </summary>
    public sealed class BattleCargoReport
    {
        public static readonly BattleCargoReport Empty = new(Array.Empty<CargoItemRecord>(), Array.Empty<string>());

        public IReadOnlyList<CargoItemRecord> Items { get; }
        public IReadOnlyList<string> DestroyedWagonIds { get; }

        public BattleCargoReport(IReadOnlyList<CargoItemRecord> items, IReadOnlyList<string> destroyedWagonIds)
        {
            Items = items ?? Array.Empty<CargoItemRecord>();
            DestroyedWagonIds = destroyedWagonIds ?? Array.Empty<string>();
        }
    }
}
