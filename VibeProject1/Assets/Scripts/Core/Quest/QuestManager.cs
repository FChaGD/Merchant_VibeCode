using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>QuestManager가 쓰는 신호·서비스 묶음. 전부 선택 - 없으면 그 판정·지급만 건너뛴다. 테스트는 필요한 것만 채운다.</summary>
    public sealed class QuestManagerSources
    {
        public IDateReader Date;
        public IReputationAdjuster Reputation;
        public ITradeGoodsInventoryRepository Inventory;
        public ITradeGoodsCargoSettlement Settlement;
        public IGoldIncome Gold;
        public IPlayerCurrencyWallet Wallet;
        public IBattleResultSource Battles;
        public ITripArrivalSource Arrivals;
        public IEventEncounterStatus Events;
        public ITripMilestoneSource Milestones;
        public ITripItinerary Itinerary;
        public ITripCurrentLocationReader Location;
        public IQuestUnitRewardGranter Units;
        public Func<float> Random01 = () => UnityEngine.Random.value;
        public Action<string> Warn = message => Debug.LogWarning(message);
    }

    /// <summary>
    /// 퀘스트 상태 저장소(Docs/설계/107번 §5·§6). 진행 중 인스턴스·게시 상태를 들고, 신호(도착·날짜·전투·화물·상행 계기·이벤트 시작)를 받아 목표 판정(QuestRules)에 넘긴 뒤
    /// 상태 전이를 확정한다. 판정 규칙·지급·배치는 협력 객체에 있다 - 여기는 "언제 무엇을 부르는가"만 둔다.
    /// - 자기 차감·지급·회수 중(processing)에는 인벤토리 변경 신호를 무시한다 - 납품 차감이 "의뢰 화물 손실"로 오판되지 않게. 대신 조작·신호 처리가 끝날 때(Settle)
    ///   남은 진행 퀘스트의 화물 판정을 다시 돌린다 - 같은 물품을 쓰는 다른 퀘스트의 상태가 낡지 않게(2026-10-27 검진 1).
    /// - 신호 처리는 진행 목록의 복사본을 돈다 - 같은 신호로 여러 퀘스트가 끝나며 목록이 바뀌어도 안전하게.
    /// - 변경 알림은 신호 하나당 한 번. 상행 영향(가산·확정 사건)은 QuestTripEffects가 맡는다.
    /// 플레이 세션 동안만 유지(저장 대비로 상태는 직렬화 가능한 값만, 설계 §5.3).
    /// </summary>
    public class QuestManager : MonoBehaviour, IQuestActions, IQuestRewardCleanupSource, IQuestCargoWarnings, IQuestDifficultyModifier, IQuestEncounterGate, IManagedComponent
    {
        private const string NoSpaceText = "적재 공간이 부족합니다";
        private const string NoRewardSpaceText = "보상을 받을 적재 공간이 부족합니다";
        private const string MainAbandonText = "메인 퀘스트는 포기할 수 없습니다";
        private const string NotReportCityText = "이 상인조합은 보고처가 아닙니다";
        private const string NotReportableText = "아직 목표를 모두 달성하지 않았습니다";
        private const string NotPostedText = "수락할 수 없는 의뢰입니다";
        private const string NotActiveText = "진행 중인 의뢰가 아닙니다";
        private const string ExpiredText = "기한이 지난 의뢰입니다";

        [SerializeField] private QuestTableAsset questTable;

        private readonly List<QuestDefinition> definitions = new();
        private readonly Dictionary<string, QuestDefinition> byId = new();
        private readonly List<QuestInstance> active = new();
        private QuestPostingState posting = new();
        private QuestManagerSources sources = new();
        private QuestCargoService cargo;
        private QuestRewardDispatcher rewards;
        private QuestTripEffects tripEffects;
        private bool processing;
        private bool cargoDirty;
        private bool pendingCleanup;

        public IReadOnlyList<QuestInstance> Active => active;
        public int Today => sources.Date != null ? sources.Date.Today.TotalDays : 0;
        public bool HasPendingCleanup => pendingCleanup;
        public QuestPostingState Posting => posting;

        public event Action Changed;
        public event Action RewardStaged;
        public event Action BonusChanged;
        /// <summary>메인 퀘스트 실패(퀘스트 Id) - 게임오버/재도전 흐름은 후속 기획(설계 107번 §6.3). 구독자가 없으면 경고만.</summary>
        public event Action<string> MainQuestFailed;

        public void RegisterSelf(IDependencyRegistrar registrar)
        {
            registrar.Register<IQuestActions>(this);
            registrar.Register<IQuestReader>(this);
            registrar.Register<IQuestStarter>(this);
            registrar.Register<IQuestRewardCleanupSource>(this);
            registrar.Register<IQuestCargoWarnings>(this);
            registrar.Register<IQuestDifficultyModifier>(this);
            registrar.Register<IQuestEncounterGate>(this);
        }

        // 등록 타입으로 조회한다 - 읽기 타입으로 찾으면 조용히 null이다.
        public void ResolveDependencies(IDependencyResolver registrar)
        {
            var s = new QuestManagerSources();
            if (registrar != null)
            {
                registrar.TryResolve(out s.Date);
                registrar.TryResolve(out s.Reputation);
                registrar.TryResolve(out s.Inventory);
                registrar.TryResolve(out s.Settlement);
                registrar.TryResolve(out s.Gold);
                registrar.TryResolve(out s.Wallet);
                registrar.TryResolve(out s.Battles);
                registrar.TryResolve(out s.Arrivals);
                registrar.TryResolve(out s.Events);
                registrar.TryResolve(out s.Milestones);
                registrar.TryResolve(out s.Itinerary);
                registrar.TryResolve(out ITripCurrentLocationRepository location);
                s.Location = location;
                registrar.TryResolve(out s.Units);
            }
            if (questTable == null) Debug.LogWarning($"{nameof(QuestManager)}: 퀘스트 테이블이 연결되지 않아 퀘스트가 없다(Tools > Game > Build Bootstrap Scene).");
            Initialize(questTable != null ? questTable.Quests : Array.Empty<QuestDefinition>(), s);
        }

        /// <summary>DI 없이 정의·의존성을 넣는다(테스트용 겸 ResolveDependencies 공용 경로). 다시 부르면 이전 구독을 끊고 상태를 비운다.</summary>
        public void Initialize(IReadOnlyList<QuestDefinition> questDefinitions, QuestManagerSources questSources)
        {
            Unsubscribe();
            definitions.Clear();
            byId.Clear();
            active.Clear();
            posting = new QuestPostingState();
            pendingCleanup = false;
            cargoDirty = false;

            foreach (var def in questDefinitions)
            {
                if (def?.Id == null || byId.ContainsKey(def.Id)) continue;
                definitions.Add(def);
                byId[def.Id] = def;
            }

            sources = questSources ?? new QuestManagerSources();
            sources.Warn ??= _ => { };
            sources.Random01 ??= () => 0f;
            cargo = sources.Inventory != null && sources.Settlement != null ? new QuestCargoService(sources.Inventory, sources.Settlement) : null;
            rewards = new QuestRewardDispatcher(sources.Gold, sources.Reputation, cargo, sources.Units, sources.Wallet, sources.Warn);
            tripEffects = new QuestTripEffects(active, byId, sources);
            tripEffects.RefreshBonusKey();
            Subscribe();
        }

        // ── 조회 ──

        public bool TryGetDefinition(string questId, out QuestDefinition definition)
        {
            definition = null;
            return questId != null && byId.TryGetValue(questId, out definition);
        }

        public bool TryGetInstance(string questId, out QuestInstance instance)
        {
            instance = FindActive(questId);
            return instance != null;
        }

        public IReadOnlyList<QuestDefinition> PostedAt(int cityId)
        {
            var list = new List<QuestDefinition>();
            var today = Today;
            var reputation = sources.Reputation?.Value ?? 0;
            foreach (var def in definitions)
            {
                if (QuestBoardRules.IsPosted(def, posting, cityId, today, reputation, IsActive)) list.Add(def);
            }
            return list;
        }

        public IReadOnlyList<string> Describe(IEnumerable<string> itemIds)
        {
            var names = new List<string>();
            if (itemIds == null) return names;
            var ids = new HashSet<string>(itemIds);
            foreach (var instance in active)
            {
                if (!byId.TryGetValue(instance.QuestId, out var def)) continue;
                foreach (var c in def.Cargo)
                {
                    if (!ids.Contains(c.ItemId)) continue;
                    if (!names.Contains(def.Title)) names.Add(def.Title);
                    break;
                }
            }
            return names;
        }

        public void ClearPendingCleanup() => pendingCleanup = false;

        public int CountOwned(string itemId) => CountItem(itemId);

        // ── 조작 ──

        public bool CanAccept(string questId, int cityId, out string reason)
        {
            reason = null;
            if (!TryGetDefinition(questId, out var def) || !QuestBoardRules.IsPosted(def, posting, cityId, Today, sources.Reputation?.Value ?? 0, IsActive))
            {
                reason = NotPostedText;
                return false;
            }
            if (QuestRules.HasExpiredCoreDeadline(def, Today))
            {
                reason = ExpiredText;
                return false;
            }
            return CanGiveCargo(def, out reason);
        }

        public bool TryStart(string questId, QuestStartSource source, int cityId, out string reason)
        {
            reason = null;
            if (source == QuestStartSource.Request)
            {
                if (!CanAccept(questId, cityId, out reason)) return false;
            }
            else if (!TryGetDefinition(questId, out _) || IsActive(questId))
            {
                reason = NotPostedText;
                return false;
            }

            var def = byId[questId];
            if (!CanGiveCargo(def, out reason)) return false;
            BeginQuest(def, cityId);
            Settle(changed: true);
            return true;
        }

        public bool CanAbandon(string questId, out string reason)
        {
            reason = null;
            if (FindActive(questId) == null)
            {
                reason = NotActiveText;
                return false;
            }
            if (byId[questId].Kind == QuestKind.Main)
            {
                reason = MainAbandonText;
                return false;
            }
            return true;
        }

        public bool TryAbandon(string questId, out string reason)
        {
            if (!CanAbandon(questId, out reason)) return false;
            Fail(byId[questId], FindActive(questId), QuestState.Abandoned);
            Settle(changed: true);
            return true;
        }

        public bool CanReport(string questId, int cityId, out string reason)
        {
            reason = null;
            var instance = FindActive(questId);
            if (instance == null)
            {
                reason = NotActiveText;
                return false;
            }
            var def = byId[questId];
            if (def.Completion != QuestCompletion.Report || def.ReportCityId != cityId)
            {
                reason = NotReportCityText;
                return false;
            }
            if (instance.State != QuestState.Reportable)
            {
                reason = NotReportableText;
                return false;
            }
            foreach (var o in def.Objectives)
            {
                if (o.Type != QuestObjectiveType.Deliver) continue;
                var have = cargo?.Count(o.ItemId) ?? 0;
                if (have < o.Count)
                {
                    reason = $"납품할 물품이 부족합니다({have}/{o.Count})";
                    return false;
                }
            }
            // 보상 공간은 납품 물품을 뺀 상태로 판정한다 - 차감이 지급보다 먼저라 그 자리가 보상에 쓰인다(검진 3).
            if (!rewards.CanReceiveItems(SuccessRewards(def, instance), DeliveredItems(def)))
            {
                reason = NoRewardSpaceText;
                return false;
            }
            return true;
        }

        public bool TryReport(string questId, int cityId, out string reason)
        {
            if (!CanReport(questId, cityId, out reason)) return false;
            Complete(byId[questId], FindActive(questId), allowStaging: false);
            Settle(changed: true);
            return true;
        }

        // ── 상행 영향 ──

        public int GetBonus(int destinationCityId) => tripEffects.GetBonus(destinationCityId);

        public bool IsEventDue(string questId, float legProgress) => tripEffects.IsEventDue(questId, legProgress);

        // ── 신호 ──

        private void HandleFinalArrival(int cityId)
        {
            var changed = false;
            foreach (var instance in Snapshot())
            {
                var def = byId[instance.QuestId];
                if (!QuestRules.OnArrive(def, instance, cityId, CountItem)) continue;
                ApplyVerdict(def, instance);
                changed = true;
            }
            Settle(changed);
        }

        private void HandleDateChanged()
        {
            var today = Today;
            foreach (var instance in Snapshot())
            {
                // 보고 가능 상태도 기한이 흐른다 - 보고형의 "기한 내 달성"은 기한 내 보고다(설계 107번 §16, 2026-10-27).
                var def = byId[instance.QuestId];
                if (QuestRules.OnDateChanged(def, instance, today)) ApplyVerdict(def, instance);
            }
            Settle(changed: true); // 재게시 일정·남은 일수 표시도 날짜에 따라 바뀐다
        }

        private void HandleBattleEnded(BattleResult result)
        {
            if (result.Outcome != BattleOutcome.Victory) return;
            var eventId = sources.Events != null && sources.Events.IsRunning ? sources.Events.CurrentEventId : null;
            var changed = false;
            foreach (var instance in Snapshot())
            {
                if (instance.State != QuestState.Active) continue;
                var def = byId[instance.QuestId];
                var isQuestEvent = tripEffects.IsQuestEvent(def, eventId);
                if (QuestRules.OnBattleWon(def, instance, result.EnemyType, isQuestEvent))
                {
                    ApplyVerdict(def, instance);
                    changed = true;
                }
            }
            Settle(changed);
        }

        private void HandleInventoryChanged()
        {
            if (processing || cargo == null) return;
            Settle(ReevaluateCargo());
        }

        // 진행 퀘스트 전체의 화물 판정(손실·조달 개수). 반환 = 상태가 바뀌었는지.
        private bool ReevaluateCargo()
        {
            if (cargo == null) return false;
            var changed = false;
            foreach (var instance in Snapshot())
            {
                if (!active.Contains(instance)) continue; // 앞 퀘스트의 판정으로 이미 끝났다
                var def = byId[instance.QuestId];
                if (QuestRules.OnCargoCountChanged(def, instance, CountItem))
                {
                    ApplyVerdict(def, instance);
                    changed = true;
                }
            }
            return changed;
        }

        private void HandleMilestone(TripMilestone milestone)
        {
            if (milestone != TripMilestone.TripStarted) return;
            pendingCleanup = false; // 정리하지 못한 표시가 다음 상행 Field에서 열리지 않게(설계 107번 §8.3)
            tripEffects.OnTripStarted();
        }

        private void HandleEventStarted(string eventId) => tripEffects.OnEventStarted(eventId);

        // ── 전이 ──

        // 이름이 Start면 Unity가 MonoBehaviour 메시지로 오인한다(인자 있는 Start 오류).
        private void BeginQuest(QuestDefinition def, int cityId)
        {
            if (def.HasGivenCargo) WithoutCargoSignal(() => cargo.TryGiveAll(CargoList(def)));

            var instance = QuestRules.Create(def, Today, cityId, QuestRules.TotalGivenCargo(def));
            active.Add(instance);
            QuestRules.OnDateChanged(def, instance, Today); // 수락 시점에 이미 지난 고정 기한(게시판 수락은 사전에 막힌다 - 트리거·후속 자동 수락용, 검진 4)
            if (cargo != null) QuestRules.OnCargoCountChanged(def, instance, CountItem); // 이미 가진 조달 물품(보고형)
            ApplyVerdict(def, instance);
        }

        // 판정 결과를 상태에 반영한다. 반환 = 상태가 바뀌었는지.
        private bool ApplyVerdict(QuestDefinition def, QuestInstance instance)
        {
            if (!active.Contains(instance)) return false;
            PayImmediateBonuses(def, instance);

            switch (QuestRules.Judge(def, instance))
            {
                case QuestVerdict.Failed:
                    Fail(def, instance, QuestState.Failed);
                    return true;
                case QuestVerdict.CoreMet:
                    if (def.Completion == QuestCompletion.Instant)
                    {
                        Complete(def, instance, allowStaging: true);
                        return true;
                    }
                    if (instance.State == QuestState.Reportable) return false;
                    instance.State = QuestState.Reportable;
                    return true;
                default:
                    if (instance.State != QuestState.Reportable) return false;
                    instance.State = QuestState.Active; // 보고 대기 중 납품 물품을 잃었다
                    return true;
            }
        }

        private void PayImmediateBonuses(QuestDefinition def, QuestInstance instance)
        {
            if (def.BonusTiming != QuestBonusTiming.Immediate) return;
            foreach (var bonusId in QuestRules.NewlyMetBonusIds(def, instance))
            {
                instance.PaidBonusIds.Add(bonusId);
                GrantRewards(RewardsFor(def, bonusId));
            }
        }

        private void Complete(QuestDefinition def, QuestInstance instance, bool allowStaging)
        {
            var success = SuccessRewards(def, instance);
            foreach (var bonusId in QuestRules.BonusIdsAtSuccess(def, instance)) instance.PaidBonusIds.Add(bonusId);

            if (cargo != null)
            {
                WithoutCargoSignal(() =>
                {
                    var delivered = new Dictionary<string, int>();
                    foreach (var o in def.Objectives)
                    {
                        if (o.Type != QuestObjectiveType.Deliver || !QuestRules.IsSatisfied(o, instance.Progress(o.Id))) continue;
                        var removed = cargo.Remove(o.ItemId, o.Count);
                        delivered[o.ItemId] = (delivered.TryGetValue(o.ItemId, out var n) ? n : 0) + removed;
                    }
                    // 납품하고 남은 지급 화물은 의뢰인에게 돌아간다 - 시장에서 팔 수 없는 전용 행이라 남겨 둘 이유가 없다.
                    foreach (var c in def.Cargo)
                    {
                        var leftover = c.Count - (delivered.TryGetValue(c.ItemId, out var n) ? n : 0);
                        ReturnGivenCargo(instance, c.ItemId, leftover);
                    }
                });
            }

            if (!allowStaging && !rewards.CanReceiveItems(success)) sources.Warn($"퀘스트 '{def.Id}': 보상 공간 판정 후 공간이 사라져 넘친 보상은 임시 보관으로 간다.");
            GrantRewards(success);

            instance.State = QuestState.Completed;
            active.Remove(instance);
            if (!posting.CompletedIds.Contains(def.Id)) posting.CompletedIds.Add(def.Id);
            if (def.Repeat == QuestRepeat.Repeatable) posting.SetRepost(def.Id, Today + def.RepostDays, never: false);
            ApplyFollowUps(def, instance);
        }

        private void Fail(QuestDefinition def, QuestInstance instance, QuestState state)
        {
            rewards.ApplyFailurePenalty(def);
            if (def.ReturnCargoOnFail && cargo != null)
            {
                WithoutCargoSignal(() =>
                {
                    foreach (var c in def.Cargo) ReturnGivenCargo(instance, c.ItemId, c.Count);
                });
            }

            instance.State = state;
            active.Remove(instance);
            if (def.Repeat == QuestRepeat.Repeatable) posting.SetRepost(def.Id, Today + def.RepostDays, never: false);
            else posting.SetRepost(def.Id, def.HasRepostDays ? Today + def.RepostDays : 0, never: !def.HasRepostDays);

            if (def.Kind != QuestKind.Main) return;
            if (MainQuestFailed != null) MainQuestFailed(def.Id);
            else sources.Warn($"메인 퀘스트 '{def.Id}' 실패 - 실패 흐름(게임오버/재도전)이 아직 없다(설계 107번 §6.3).");
        }

        private void ApplyFollowUps(QuestDefinition def, QuestInstance instance)
        {
            foreach (var f in def.FollowUps)
            {
                if (!string.IsNullOrEmpty(f.RequiredBonusId) && !instance.PaidBonusIds.Contains(f.RequiredBonusId)) continue;
                if (!byId.TryGetValue(f.NextQuestId, out var next)) continue;
                if (f.Mode == QuestFollowUpMode.AutoAccept && !IsActive(next.Id) && CanGiveCargo(next, out _))
                {
                    BeginQuest(next, sources.Location?.CurrentCityId ?? instance.SourceCityId);
                    continue;
                }
                if (f.Mode == QuestFollowUpMode.AutoAccept) sources.Warn($"후속 퀘스트 '{next.Id}'를 자동 수락하지 못해 게시판 해금으로 대신한다.");
                if (!posting.UnlockedIds.Contains(next.Id)) posting.UnlockedIds.Add(next.Id);
            }
        }

        // ── 보조 ──

        // 지급 화물 회수. 같은 전용 행을 다른 진행 중 퀘스트도 지급받았으면 그 몫은 남긴다 - 개체에 퀘스트 표시가 없어(설계 107번 §2) 개수로만 나눈다.
        private void ReturnGivenCargo(QuestInstance owner, string itemId, int amount)
        {
            if (amount <= 0) return;
            var reserved = 0;
            foreach (var other in active)
            {
                if (other == owner || !byId.TryGetValue(other.QuestId, out var otherDef)) continue;
                foreach (var c in otherDef.Cargo)
                {
                    if (c.ItemId == itemId) reserved += c.Count;
                }
            }
            var removable = Math.Min(amount, cargo.Count(itemId) - reserved);
            if (removable > 0) cargo.Remove(itemId, removable);
        }

        private void GrantRewards(List<QuestRewardDefinition> list)
        {
            if (list.Count == 0) return;
            var staged = 0;
            WithoutCargoSignal(() => staged = rewards.Grant(list));
            if (staged <= 0) return;
            pendingCleanup = true;
            RewardStaged?.Invoke();
        }

        private List<QuestRewardDefinition> SuccessRewards(QuestDefinition def, QuestInstance instance)
        {
            var list = RewardsFor(def, null);
            foreach (var bonusId in QuestRules.BonusIdsAtSuccess(def, instance)) list.AddRange(RewardsFor(def, bonusId));
            return list;
        }

        private static List<QuestRewardDefinition> RewardsFor(QuestDefinition def, string objectiveId) => QuestRules.RewardsFor(def, objectiveId);

        private bool CanGiveCargo(QuestDefinition def, out string reason)
        {
            reason = null;
            if (!def.HasGivenCargo) return true;
            if (cargo != null && cargo.CanFit(CargoList(def))) return true;
            reason = NoSpaceText;
            return false;
        }

        private static List<(string, int)> CargoList(QuestDefinition def)
        {
            var list = new List<(string, int)>();
            foreach (var c in def.Cargo) list.Add((c.ItemId, c.Count));
            return list;
        }

        // 보고 시 차감될 납품 물품(보상 공간 판정용).
        private static List<(string, int)> DeliveredItems(QuestDefinition def)
        {
            var list = new List<(string, int)>();
            foreach (var o in def.Objectives)
            {
                if (o.Type == QuestObjectiveType.Deliver) list.Add((o.ItemId, o.Count));
            }
            return list;
        }

        private int CountItem(string itemId) => cargo?.Count(itemId) ?? 0;

        private bool IsActive(string questId) => FindActive(questId) != null;

        private QuestInstance FindActive(string questId)
        {
            foreach (var instance in active)
            {
                if (instance.QuestId == questId) return instance;
            }
            return null;
        }

        private List<QuestInstance> Snapshot() => new(active);

        // 자기 인벤토리 조작 - 그 사이 변경 신호는 무시하고, 끝나면 Settle에서 다른 퀘스트를 다시 판정하도록 표시한다. 중첩 호출은 바깥 상태를 지킨다.
        private void WithoutCargoSignal(Action action)
        {
            var outer = processing;
            processing = true;
            try
            {
                action();
            }
            finally
            {
                processing = outer;
                cargoDirty = true;
            }
        }

        // 조작·신호 처리의 마무리 - 자기 조작으로 바뀐 화물 기준 재판정(그 재판정이 또 조작하면 다시) 뒤 알림 한 번.
        private void Settle(bool changed)
        {
            while (cargoDirty)
            {
                cargoDirty = false;
                if (ReevaluateCargo()) changed = true;
            }
            if (!changed) return;
            Changed?.Invoke();
            if (tripEffects.RefreshBonusKey()) BonusChanged?.Invoke();
        }

        private void Subscribe()
        {
            if (sources.Arrivals != null) sources.Arrivals.FinalArrived += HandleFinalArrival;
            if (sources.Date != null) sources.Date.Changed += HandleDateChanged;
            if (sources.Battles != null) sources.Battles.OnBattleEnded += HandleBattleEnded;
            // 임시 보관 변경도 같은 이벤트 멤버로 온다(IInventoryStagingReader 요약 주석) - 이름이 겹쳐 한쪽으로 지정한다.
            if (sources.Inventory != null) ((IInventoryReader)sources.Inventory).OnChanged += HandleInventoryChanged;
            if (sources.Milestones != null) sources.Milestones.Raised += HandleMilestone;
            if (sources.Events != null) sources.Events.EventStarted += HandleEventStarted;
        }

        private void Unsubscribe()
        {
            if (sources == null) return;
            if (sources.Arrivals != null) sources.Arrivals.FinalArrived -= HandleFinalArrival;
            if (sources.Date != null) sources.Date.Changed -= HandleDateChanged;
            if (sources.Battles != null) sources.Battles.OnBattleEnded -= HandleBattleEnded;
            if (sources.Inventory != null) ((IInventoryReader)sources.Inventory).OnChanged -= HandleInventoryChanged;
            if (sources.Milestones != null) sources.Milestones.Raised -= HandleMilestone;
            if (sources.Events != null) sources.Events.EventStarted -= HandleEventStarted;
        }

        private void OnDestroy() => Unsubscribe();
    }
}
