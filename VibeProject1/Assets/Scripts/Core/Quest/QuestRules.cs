using System;
using System.Collections.Generic;

namespace Game.Core
{
    public enum QuestVerdict { InProgress, CoreMet, Failed }

    /// <summary>
    /// 목표 판정 규칙(Docs/설계/107번 §6.1). 순수 C# - 신호를 받은 QuestManager가 인스턴스 하나씩 넘긴다. 각 판정은 "바뀌었는가"를 돌려줘 변경 알림을 한 번으로 묶게 한다.
    /// - 기한·보존은 행동이 아니라 제약이라 Met을 쓰지 않는다: 어기기 전이면 충족, 어기면(Impossible) 미달. 핵심이면 실패, 보조면 추가 보상 없음.
    /// - 납품: 즉시형은 대상 도시 도착 순간의 개수로 판정한다. 보고형은 화물 변화마다 개수로 충족을 다시 매긴다(양방향) - 보고 가능 상태가
    ///   물품을 잃으면 다시 진행 중으로 돌아가고, 그동안 기한도 다시 흐른다. 지급 화물 납품은 개수가 모자라는 순간 불가(다시 얻을 수 없음).
    /// </summary>
    public static class QuestRules
    {
        public static QuestInstance Create(QuestDefinition def, int today, int sourceCityId, int givenCargoCount)
        {
            var instance = new QuestInstance
            {
                QuestId = def.Id,
                State = QuestState.Active,
                AcceptedDay = today,
                SourceCityId = sourceCityId,
                GivenCargoCount = givenCargoCount,
            };
            foreach (var o in def.Objectives)
            {
                var deadline = -1;
                if (o.Type == QuestObjectiveType.Deadline) deadline = o.HasDeadlineDays ? today + o.DeadlineDays : o.DeadlineTotalDays;
                instance.Objectives.Add(new QuestObjectiveProgress { ObjectiveId = o.Id, DeadlineDay = deadline });
            }
            return instance;
        }

        /// <summary>핵심 고정 날짜 기한이 이미 지났는지 - 수락 시점 판정(2026-10-27 검진 4). "수락 후 N일"은 수락일 기준이라 지날 수 없다.</summary>
        public static bool HasExpiredCoreDeadline(QuestDefinition def, int today)
        {
            foreach (var o in def.Objectives)
            {
                if (o.Type == QuestObjectiveType.Deadline && o.Role == QuestObjectiveRole.Core && !o.HasDeadlineDays && today > o.DeadlineTotalDays) return true;
            }
            return false;
        }

        public static bool OnArrive(QuestDefinition def, QuestInstance instance, int cityId, Func<string, int> countItem)
        {
            var changed = false;
            foreach (var o in def.Objectives)
            {
                var p = instance.Progress(o.Id);
                if (p == null || p.Met || p.Impossible || o.CityId != cityId) continue;
                if (o.Type == QuestObjectiveType.Arrive
                    || (o.Type == QuestObjectiveType.Deliver && def.Completion == QuestCompletion.Instant && countItem(o.ItemId) >= o.Count))
                {
                    p.Met = true;
                    changed = true;
                }
            }
            return changed;
        }

        public static bool OnDateChanged(QuestDefinition def, QuestInstance instance, int today)
        {
            var changed = false;
            foreach (var o in def.Objectives)
            {
                if (o.Type != QuestObjectiveType.Deadline) continue;
                var p = instance.Progress(o.Id);
                if (p == null || p.Impossible || p.DeadlineDay < 0 || today <= p.DeadlineDay) continue;
                p.Impossible = true;
                changed = true;
            }
            return changed;
        }

        public static bool OnBattleWon(QuestDefinition def, QuestInstance instance, string enemyType, bool isQuestEvent)
        {
            var changed = false;
            foreach (var o in def.Objectives)
            {
                if (o.Type != QuestObjectiveType.BattleWin) continue;
                var p = instance.Progress(o.Id);
                if (p == null || p.Met) continue;
                if (!string.IsNullOrEmpty(o.EnemyType) && o.EnemyType != enemyType) continue;
                if (o.QuestBattleOnly && !isQuestEvent) continue;
                p.Value++;
                if (p.Value >= o.Count) p.Met = true;
                changed = true;
            }
            return changed;
        }

        public static bool OnCargoCountChanged(QuestDefinition def, QuestInstance instance, Func<string, int> countItem)
        {
            var changed = false;
            foreach (var o in def.Objectives)
            {
                var p = instance.Progress(o.Id);
                if (p == null || p.Impossible) continue;
                if (o.Type == QuestObjectiveType.Deliver)
                {
                    var count = countItem(o.ItemId);
                    if (IsGivenCargo(def, o.ItemId) && count < o.Count && !p.Met)
                    {
                        p.Impossible = true;
                        changed = true;
                    }
                    else if (def.Completion == QuestCompletion.Report)
                    {
                        var met = count >= o.Count;
                        if (met != p.Met)
                        {
                            p.Met = met;
                            changed = true;
                        }
                    }
                }
                else if (o.Type == QuestObjectiveType.Preserve && CountGivenCargo(def, countItem) < instance.GivenCargoCount)
                {
                    p.Impossible = true;
                    changed = true;
                }
            }
            return changed;
        }

        public static QuestVerdict Judge(QuestDefinition def, QuestInstance instance)
        {
            var allMet = true;
            foreach (var o in def.Objectives)
            {
                if (o.Role != QuestObjectiveRole.Core) continue;
                var p = instance.Progress(o.Id);
                if (p == null) continue;
                if (p.Impossible) return QuestVerdict.Failed;
                if (!IsSatisfied(o, p)) allMet = false;
            }
            return allMet ? QuestVerdict.CoreMet : QuestVerdict.InProgress;
        }

        /// <summary>즉시 지급 대상 보조 목표 - 충족했고 아직 지급하지 않은 것. 제약형(기한·보존)은 성공 시점에만 정해지므로 뺀다.</summary>
        public static List<string> NewlyMetBonusIds(QuestDefinition def, QuestInstance instance)
        {
            var result = new List<string>();
            foreach (var o in def.Objectives)
            {
                if (o.Role != QuestObjectiveRole.Bonus || IsConstraint(o)) continue;
                var p = instance.Progress(o.Id);
                if (p != null && p.Met && !p.Impossible && !instance.PaidBonusIds.Contains(o.Id)) result.Add(o.Id);
            }
            return result;
        }

        /// <summary>성공 시점의 보조 충족 목록(기한 보조 포함, 이미 지급한 것 제외).</summary>
        public static List<string> BonusIdsAtSuccess(QuestDefinition def, QuestInstance instance)
        {
            var result = new List<string>();
            foreach (var o in def.Objectives)
            {
                if (o.Role != QuestObjectiveRole.Bonus) continue;
                var p = instance.Progress(o.Id);
                if (p != null && IsSatisfied(o, p) && !instance.PaidBonusIds.Contains(o.Id)) result.Add(o.Id);
            }
            return result;
        }

        public static bool IsSatisfied(QuestObjectiveDefinition o, QuestObjectiveProgress p)
            => IsConstraint(o) ? !p.Impossible : p.Met && !p.Impossible;

        /// <summary>행동이 아니라 "어기지 않으면 충족"인 목표 - 기한(만료 전), 보존(잃기 전).</summary>
        public static bool IsConstraint(QuestObjectiveDefinition o) => o.Type == QuestObjectiveType.Deadline || o.Type == QuestObjectiveType.Preserve;

        public static bool IsGivenCargo(QuestDefinition def, string itemId)
        {
            foreach (var c in def.Cargo)
            {
                if (c.ItemId == itemId) return true;
            }
            return false;
        }

        public static int CountGivenCargo(QuestDefinition def, Func<string, int> countItem)
        {
            var total = 0;
            var seen = new HashSet<string>();
            foreach (var c in def.Cargo)
            {
                if (seen.Add(c.ItemId)) total += countItem(c.ItemId);
            }
            return total;
        }

        /// <summary>objectiveId null = 성공 보상, 값 = 그 보조 목표의 추가 보상.</summary>
        public static List<QuestRewardDefinition> RewardsFor(QuestDefinition def, string objectiveId)
        {
            var list = new List<QuestRewardDefinition>();
            foreach (var r in def.Rewards)
            {
                var target = string.IsNullOrEmpty(r.ObjectiveId) ? null : r.ObjectiveId;
                if (target == objectiveId) list.Add(r);
            }
            return list;
        }

        public static int TotalGivenCargo(QuestDefinition def)
        {
            var total = 0;
            foreach (var c in def.Cargo) total += c.Count;
            return total;
        }
    }
}
