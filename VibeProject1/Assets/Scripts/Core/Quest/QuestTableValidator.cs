using System;
using System.Collections.Generic;
using System.Globalization;

namespace Game.Core
{
    /// <summary>임포트 검증에 쓰는 다른 테이블의 Id 집합(Docs/설계/107번 §4). 임포터가 원본 엑셀에서 직접 모아 넘긴다 - 임포트 순서와 무관하게 같은 결과.</summary>
    public sealed class QuestReferenceIds
    {
        public ISet<int> CityIds = new HashSet<int>();
        public ISet<string> TradeGoodsIds = new HashSet<string>();
        public ISet<string> QuestCargoIds = new HashSet<string>();
        public ISet<string> EnemyTypes = new HashSet<string>();
        public ISet<string> EventIds = new HashSet<string>();
    }

    /// <summary>
    /// 퀘스트 정의 검증(Docs/설계/107번 §4 임포트 검증 규칙). 임포터가 아니라 런타임 어셈블리에 두는 이유: 규칙을 EditMode 테스트로 고정하기 위해서다.
    /// 위반은 퀘스트 Id를 담은 FormatException - 임포트를 중단시켜 잘못된 정의가 실행 중에 조용히 무시되지 않게 한다.
    /// </summary>
    public static class QuestTableValidator
    {
        public static void Validate(IReadOnlyList<QuestDefinition> quests, QuestReferenceIds refs)
        {
            var ids = new HashSet<string>();
            foreach (var q in quests)
            {
                if (string.IsNullOrWhiteSpace(q.Id)) throw new FormatException("Quest: Id가 비어 있다.");
                if (!ids.Add(q.Id)) throw new FormatException($"Quest '{q.Id}': Id가 중복됐다.");
            }

            foreach (var q in quests) ValidateQuest(q, ids, refs);
        }

        private static void ValidateQuest(QuestDefinition q, HashSet<string> questIds, QuestReferenceIds refs)
        {
            void Fail(string message) => throw new FormatException($"Quest '{q.Id}': {message}");

            if (q.Acceptance == QuestAcceptance.Request && q.BoardCityIds.Count == 0) Fail("Request 퀘스트는 BoardCityIds가 필요하다.");
            foreach (var city in q.BoardCityIds)
            {
                if (!refs.CityIds.Contains(city)) Fail($"BoardCityIds의 도시 {city}가 City.xlsx에 없다.");
            }
            if (q.Completion == QuestCompletion.Report && !refs.CityIds.Contains(q.ReportCityId)) Fail("Report 퀘스트는 City.xlsx에 있는 ReportCityId가 필요하다.");
            if (!string.IsNullOrEmpty(q.RequiredQuestId) && !questIds.Contains(q.RequiredQuestId)) Fail($"RequiredQuestId '{q.RequiredQuestId}'가 없다.");
            if (q.Repeat == QuestRepeat.Repeatable && !q.HasRepostDays) Fail("반복성 퀘스트는 RepostDays가 필요하다.");
            if (q.HasRepostDays && q.RepostDays < 0) Fail("RepostDays는 0 이상이어야 한다.");
            if (q.FailGold < 0) Fail("FailGold는 0 이상이어야 한다.");

            var objectiveIds = new HashSet<string>();
            var hasCore = false;
            foreach (var o in q.Objectives)
            {
                if (string.IsNullOrWhiteSpace(o.Id) || !objectiveIds.Add(o.Id)) Fail($"목표 Id '{o.Id}'가 비었거나 중복됐다.");
                if (o.Role == QuestObjectiveRole.Core) hasCore = true;
                ValidateObjective(q, o, refs, Fail);
            }
            if (!hasCore) Fail("핵심 목표가 1개 이상 필요하다.");

            foreach (var c in q.Cargo)
            {
                if (!refs.QuestCargoIds.Contains(c.ItemId)) Fail($"Cargo '{c.ItemId}'는 무역품 테이블의 의뢰 화물(TradeKind QuestCargo) 행이어야 한다.");
                if (c.Count <= 0) Fail($"Cargo '{c.ItemId}'의 Count는 1 이상이어야 한다.");
            }

            foreach (var r in q.Rewards)
            {
                if (!string.IsNullOrEmpty(r.ObjectiveId))
                {
                    var target = q.FindObjective(r.ObjectiveId);
                    if (target == null || target.Role != QuestObjectiveRole.Bonus) Fail($"보상의 ObjectiveId '{r.ObjectiveId}'는 이 퀘스트의 보조 목표여야 한다.");
                }
                if (r.Type == QuestRewardType.Item && !refs.TradeGoodsIds.Contains(r.ItemId ?? string.Empty)) Fail($"아이템 보상 '{r.ItemId}'가 무역품 테이블에 없다.");
                if (r.Type == QuestRewardType.Unit && string.IsNullOrWhiteSpace(r.ItemId)) Fail("유닛 보상은 ItemId(캐릭터 Id)가 필요하다.");
                if ((r.Type == QuestRewardType.Gold || r.Type == QuestRewardType.Item) && r.Amount <= 0) Fail($"{r.Type} 보상의 Amount는 1 이상이어야 한다.");
            }

            var eventEffects = 0;
            foreach (var e in q.Effects)
            {
                // 발생 위치·발생 기록을 인스턴스에 하나만 두므로 확정 사건은 퀘스트당 하나다(설계 107번 §5.1).
                if (!string.IsNullOrEmpty(e.EventId) && ++eventEffects > 1) Fail("확정 사건(EventId)이 있는 효과는 퀘스트당 하나만 둘 수 있다.");
                if (e.Scope == QuestEffectScope.Destination && !TryGetTargetCity(q, out _)) Fail("Destination 효과는 도착·납품 목표 도시가 필요하다.");
                if (!string.IsNullOrEmpty(e.EventId) && !refs.EventIds.Contains(e.EventId)) Fail($"효과 EventId '{e.EventId}'가 Encounter.xlsx Event에 없다.");
                // 1(구간 끝)은 도착과 겹쳐 사건 판정이 닿지 않을 수 있다 - 1 미만만 허용(2026-10-27 검진 5).
                if (e.ProgressMin < 0f || e.ProgressMin >= 1f || e.ProgressMax < 0f || e.ProgressMax >= 1f) Fail("ProgressMin·ProgressMax는 0 이상 1 미만이어야 한다.");
            }

            foreach (var f in q.FollowUps)
            {
                if (!questIds.Contains(f.NextQuestId ?? string.Empty)) Fail($"후속 퀘스트 '{f.NextQuestId}'가 없다.");
                if (!string.IsNullOrEmpty(f.RequiredBonusId))
                {
                    var bonus = q.FindObjective(f.RequiredBonusId);
                    if (bonus == null || bonus.Role != QuestObjectiveRole.Bonus) Fail($"후속 조건 '{f.RequiredBonusId}'는 이 퀘스트의 보조 목표여야 한다.");
                }
            }
        }

        private static void ValidateObjective(QuestDefinition q, QuestObjectiveDefinition o, QuestReferenceIds refs, Action<string> fail)
        {
            var label = $"목표 '{o.Id}'";
            switch (o.Type)
            {
                case QuestObjectiveType.Arrive:
                    if (!refs.CityIds.Contains(o.CityId)) fail($"{label}: CityId가 City.xlsx에 없다.");
                    break;
                case QuestObjectiveType.Deliver:
                    if (!refs.CityIds.Contains(o.CityId)) fail($"{label}: CityId가 City.xlsx에 없다.");
                    if (!refs.TradeGoodsIds.Contains(o.ItemId ?? string.Empty)) fail($"{label}: ItemId가 무역품 테이블에 없다.");
                    if (o.Count <= 0) fail($"{label}: Count는 1 이상이어야 한다.");
                    // 차감이 보고 시점이므로 보고처가 곧 납품처다(설계 107 §13-1). 분리가 필요해지면 이 규칙만 푼다.
                    if (q.Completion == QuestCompletion.Report && o.CityId != q.ReportCityId) fail($"{label}: 보고형 퀘스트의 납품 도시는 ReportCityId와 같아야 한다.");
                    break;
                case QuestObjectiveType.Deadline:
                    if (o.HasDeadlineDays == o.HasDeadlineDate) fail($"{label}: DeadlineDays·DeadlineDate 중 하나만 지정해야 한다.");
                    if (o.HasDeadlineDays && o.DeadlineDays < 0) fail($"{label}: DeadlineDays는 0 이상이어야 한다.");
                    break;
                case QuestObjectiveType.BattleWin:
                    if (o.Count <= 0) fail($"{label}: Count는 1 이상이어야 한다.");
                    if (!string.IsNullOrEmpty(o.EnemyType) && !refs.EnemyTypes.Contains(o.EnemyType)) fail($"{label}: EnemyType '{o.EnemyType}'가 적 스탯 테이블에 없다.");
                    break;
                case QuestObjectiveType.Preserve:
                    if (!q.HasGivenCargo) fail($"{label}: 보존 목표는 지급 화물(Cargo 행)이 필요하다.");
                    break;
            }
        }

        /// <summary>Destination 효과의 대상 도시 = 첫 도착·납품 목표 도시(설계 107 §4).</summary>
        public static bool TryGetTargetCity(QuestDefinition q, out int cityId)
        {
            foreach (var o in q.Objectives)
            {
                if (o.Type == QuestObjectiveType.Arrive || o.Type == QuestObjectiveType.Deliver)
                {
                    cityId = o.CityId;
                    return true;
                }
            }
            cityId = 0;
            return false;
        }
    }

    /// <summary>고정 기한 날짜 표기 `경과연차-월-일`(0년 1월 1일 = 0일, Docs/설계/107번 §4).</summary>
    public static class QuestDateFormat
    {
        public static bool TryParse(string text, out int totalDays)
        {
            totalDays = 0;
            var parts = text?.Trim().Split('-');
            if (parts == null || parts.Length != 3) return false;
            if (!TryInt(parts[0], out var year) || !TryInt(parts[1], out var month) || !TryInt(parts[2], out var day)) return false;
            if (year < 0 || month < 1 || month > GameDate.MonthsPerYear || day < 1 || day > GameDate.DaysPerMonth) return false;
            totalDays = year * GameDate.DaysPerYear + (month - 1) * GameDate.DaysPerMonth + (day - 1);
            return true;
        }

        private static bool TryInt(string s, out int value) => int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }
}
