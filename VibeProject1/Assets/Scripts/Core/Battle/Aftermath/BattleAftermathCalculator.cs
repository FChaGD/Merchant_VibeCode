using System;
using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>전투 뒤 화물·시간 정산 계획(설계 79번 §5). 대열·보유 목록 반영은 계획에 담지 않는다 - 저장소 상태가 필요해 반영 컴포넌트가 한다.</summary>
    public sealed class BattleAftermathPlan
    {
        public static readonly BattleAftermathPlan Empty = new(Array.Empty<string>(), Array.Empty<IInventoryItemDefinition>(), Array.Empty<string>(), 0f, 0, 0, 0, 0);

        /// <summary>환급 없이 제거할 인스턴스 Id.</summary>
        public IReadOnlyList<string> RemoveInstanceIds { get; }
        /// <summary>승리 시 임시보관에 새 인스턴스로 넣을 회수 물품(설계 79번 §15-3).</summary>
        public IReadOnlyList<IInventoryItemDefinition> Recover { get; }
        public IReadOnlyList<string> DestroyedWagonIds { get; }
        public float ExtraSeconds { get; }
        public int Broken { get; }
        public int StolenLost { get; }
        public int Recoverable { get; }
        public int DefeatLost { get; }

        public BattleAftermathPlan(IReadOnlyList<string> removeInstanceIds, IReadOnlyList<IInventoryItemDefinition> recover, IReadOnlyList<string> destroyedWagonIds,
            float extraSeconds, int broken, int stolenLost, int recoverable, int defeatLost)
        {
            RemoveInstanceIds = removeInstanceIds ?? Array.Empty<string>();
            Recover = recover ?? Array.Empty<IInventoryItemDefinition>();
            DestroyedWagonIds = destroyedWagonIds ?? Array.Empty<string>();
            ExtraSeconds = extraSeconds;
            Broken = broken;
            StolenLost = stolenLost;
            Recoverable = recoverable;
            DefeatLost = defeatLost;
        }
    }

    /// <summary>
    /// 전투 결과 + 패배 결과 → 정산 계획(Docs/기획/77번 §4.4~4.6, 78번 §4.2·§4.7, 설계 79번 §5). 저장소 없이 계산만 하는 순수 함수로
    /// 둔 이유는 결과별 손실·회수 분기를 GameObject 없이(Unity 밖) 검증하기 위해서다. 난수는 Func로 받아 고정값으로 검증한다.
    /// - 원장의 Fate는 기록일 뿐이고 해석은 결과에 따른다: Unprotected·Recoverable은 승리면 회수, 패배면 도난 손실로 집계한다.
    ///   Stolen(소유 적이 끝까지 생존)은 승리면 생길 수 없지만(적 전멸) 방어적으로 도난 손실로 본다.
    /// - 포로·사망은 플레이가 끝나므로 아무것도 반영하지 않는다(기획 77번 §4-24).
    /// </summary>
    public static class BattleAftermathCalculator
    {
        public static BattleAftermathPlan Plan(BattleResult result, DefeatConsequence? consequence, IEnumerable<InventoryItemInstance> currentItems, Func<float> random)
        {
            if (consequence == DefeatConsequence.Captured || consequence == DefeatConsequence.Death) return BattleAftermathPlan.Empty;

            var report = result.CargoReport;
            var victory = result.Outcome == BattleOutcome.Victory;
            var remove = new List<string>();
            var removeSet = new HashSet<string>();
            var recover = new List<IInventoryItemDefinition>();
            int broken = 0, stolenLost = 0, recoverable = 0, defeatLost = 0;

            foreach (var record in report.Items)
            {
                if (record.Fate == CargoItemFate.Intact) continue;

                if (removeSet.Add(record.InstanceId)) remove.Add(record.InstanceId);
                switch (record.Fate)
                {
                    case CargoItemFate.Broken:
                        broken++;
                        break;
                    case CargoItemFate.Unprotected:
                    case CargoItemFate.Recoverable:
                        if (victory)
                        {
                            recover.Add(record.Definition);
                            recoverable++;
                        }
                        else
                        {
                            stolenLost++;
                        }
                        break;
                    default: // Stolen, StolenLost
                        stolenLost++;
                        break;
                }
            }

            var destroyed = new HashSet<string>(report.DestroyedWagonIds);

            // 유닛 전멸 패배(도주·궤주 공통 30%, 기획 78번 §4-29): 살아남은 마차에 그대로 실린 물품만 굴린다. 임시보관 물품은 마차에 없고,
            // 파괴된 마차의 물품은 이미 원장이 Fate로 처리했다.
            if (!victory && result.DefeatCause == BattleDefeatCause.NoCombatants
                && (consequence == DefeatConsequence.Flee || consequence == DefeatConsequence.Rout)
                && currentItems != null && random != null)
            {
                foreach (var item in currentItems)
                {
                    if (item.IsStaged || string.IsNullOrEmpty(item.SectionId)) continue;
                    if (destroyed.Contains(item.SectionId) || removeSet.Contains(item.InstanceId)) continue;
                    if (random() >= CargoLossTuning.UnitWipeLossChance) continue;

                    removeSet.Add(item.InstanceId);
                    remove.Add(item.InstanceId);
                    defeatLost++;
                }
            }

            // 상행이 이어지는 결과(승리·도주)에만 시간 추가 - 궤주는 마을로 돌아가 구간이 끝난다(기획 77번 §4-20).
            var continues = victory || consequence == DefeatConsequence.Flee;
            var extraSeconds = continues ? report.DestroyedWagonIds.Count * CargoLossTuning.SecondsPerDestroyedWagon : 0f;

            return new BattleAftermathPlan(remove, recover, new List<string>(report.DestroyedWagonIds), extraSeconds, broken, stolenLost, recoverable, defeatLost);
        }
    }
}
