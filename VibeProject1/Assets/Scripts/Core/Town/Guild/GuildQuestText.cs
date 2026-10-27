using System;
using System.Collections.Generic;
using System.Text;

namespace Game.Core
{
    /// <summary>
    /// 게시판 상세 문구(Docs/설계/107번 §11.2). 순수 함수 - 테스트로 문구를 고정한다. 목표 표시문이 데이터에 없으면 목표 종류·값으로 기계적으로 만든다
    /// (도시·품목·횟수만 옮기는 설명이라 창작이 아니다). 기호(✓·✗)는 한글 폰트 대체 글꼴에 없을 수 있어 글자로 쓴다.
    /// </summary>
    public static class GuildQuestText
    {
        public static string Objectives(QuestDefinition def, QuestInstance instance, Func<string, int> countItem, Func<int, string> cityName, Func<string, string> itemName, int today)
        {
            var builder = new StringBuilder();
            foreach (var o in def.Objectives)
            {
                if (builder.Length > 0) builder.Append('\n');
                builder.Append(o.Role == QuestObjectiveRole.Core ? "[핵심] " : "[보조] ");
                builder.Append(Label(o, cityName, itemName));
                var p = instance?.Progress(o.Id);
                builder.Append(Progress(o, p, countItem, today, instance != null));
            }
            return builder.ToString();
        }

        public static string Label(QuestObjectiveDefinition o, Func<int, string> cityName, Func<string, string> itemName)
        {
            if (!string.IsNullOrWhiteSpace(o.Label) && o.Label != PlaceholderText.Missing) return o.Label;
            switch (o.Type)
            {
                case QuestObjectiveType.Arrive: return $"{cityName(o.CityId)} 도착";
                case QuestObjectiveType.Deliver: return $"{itemName(o.ItemId)} {o.Count}개 납품({cityName(o.CityId)})";
                case QuestObjectiveType.Deadline: return "기한 준수";
                case QuestObjectiveType.BattleWin: return o.QuestBattleOnly ? $"의뢰 전투 승리 {o.Count}회" : $"전투 승리 {o.Count}회";
                default: return "의뢰 화물 보존";
            }
        }

        private static string Progress(QuestObjectiveDefinition o, QuestObjectiveProgress p, Func<string, int> countItem, int today, bool accepted)
        {
            if (!accepted || p == null) return string.Empty;
            if (p.Impossible) return o.Type == QuestObjectiveType.Deadline ? " (기한 초과)" : " (실패)";
            switch (o.Type)
            {
                case QuestObjectiveType.Deliver:
                    return $" ({Math.Min(countItem(o.ItemId), o.Count)}/{o.Count})";
                case QuestObjectiveType.BattleWin:
                    return p.Met ? " (완료)" : $" ({p.Value}/{o.Count})";
                case QuestObjectiveType.Deadline:
                    return p.DeadlineDay >= 0 ? $" ({DateText(p.DeadlineDay)}까지)" : string.Empty;
                case QuestObjectiveType.Arrive:
                    return p.Met ? " (완료)" : string.Empty;
                default:
                    return string.Empty;
            }
        }

        /// <summary>대표 기한 - 핵심 기한이 있으면 그것, 없으면 첫 보조 기한. 수락 전은 데이터 값으로 안내한다.</summary>
        public static string Deadline(QuestDefinition def, QuestInstance instance, int today)
        {
            var o = PickDeadline(def);
            if (o == null) return "기한 없음";
            var p = instance?.Progress(o.Id);
            if (p == null) return o.HasDeadlineDays ? $"수락 후 {o.DeadlineDays}일 이내" : $"{DateText(o.DeadlineTotalDays)}까지";
            if (p.Impossible) return "기한 초과";
            return $"{DateText(p.DeadlineDay)}까지 ({Math.Max(0, p.DeadlineDay - today)}일 남음)";
        }

        public static string Reward(QuestDefinition def, Func<string, string> itemName, Func<int, string> cityName)
        {
            var builder = new StringBuilder(QuestRewardDispatcher.Describe(RewardsFor(def, null), itemName));
            foreach (var o in def.Objectives)
            {
                if (o.Role != QuestObjectiveRole.Bonus) continue;
                var bonus = RewardsFor(def, o.Id);
                if (bonus.Count == 0) continue;
                builder.Append($"\n(보조 · {Label(o, cityName, itemName)}) {QuestRewardDispatcher.Describe(bonus, itemName)}");
            }
            return builder.ToString();
        }

        public static string Penalty(QuestDefinition def)
        {
            var parts = new List<string>();
            if (def.FailReputation != 0) parts.Add(def.FailReputation > 0 ? $"평판 +{def.FailReputation}" : $"평판 {def.FailReputation}");
            if (def.FailGold > 0) parts.Add($"위약금 {def.FailGold:N0}골드");
            return parts.Count > 0 ? string.Join(", ", parts) : null;
        }

        private static string DateText(int totalDays) => new GameDate(totalDays).ToDisplayString();

        private static QuestObjectiveDefinition PickDeadline(QuestDefinition def)
        {
            QuestObjectiveDefinition bonus = null;
            foreach (var o in def.Objectives)
            {
                if (o.Type != QuestObjectiveType.Deadline) continue;
                if (o.Role == QuestObjectiveRole.Core) return o;
                bonus ??= o;
            }
            return bonus;
        }

        private static List<QuestRewardDefinition> RewardsFor(QuestDefinition def, string objectiveId) => QuestRules.RewardsFor(def, objectiveId);
    }
}
