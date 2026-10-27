using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 상인조합 게시판(Docs/설계/107번 §11.1) - 퀘스트 상태를 현재 마을 기준으로 걸러 목록·상세·버튼 사유로 바꾼다. PlaceholderGuildQuestBoard 대체.
    /// 목록 = 보고 가능(이 조합이 보고처) → 진행 중(이 조합에서 수락) → 수락 가능(이 조합 게시) 순. 보고 가능한 것은 수락 조합이 달라도 보인다.
    /// 변경 알림은 퀘스트·평판·현재 위치·무역품 인벤토리 변경을 묶는다 - 납품 개수·공간 사유가 인벤토리에 따라 바뀌기 때문이다.
    /// 의존성은 등록 타입으로 조회한다(읽기 타입 미등록 함정). 퀘스트 계약이 없으면 빈 게시판.
    /// </summary>
    public class GuildQuestBoard : MonoBehaviour, IGuildQuestBoard, IManagedComponent
    {
        private IQuestActions quests;
        private ITripCurrentLocationReader location;
        private IReputationReader reputation;
        private ITradeGoodsInventoryRepository inventory;
        private IWorldMapReader worldMap;

        public event Action Changed;

        public void RegisterSelf(IDependencyRegistrar registrar) => registrar.Register<IGuildQuestBoard>(this);

        public void ResolveDependencies(IDependencyResolver registrar)
        {
            IQuestActions questActions = null;
            ITripCurrentLocationRepository currentLocation = null;
            IReputationReader reputationReader = null;
            ITradeGoodsInventoryRepository tradeGoods = null;
            IWorldMapReader map = null;
            if (registrar != null)
            {
                registrar.TryResolve(out questActions);
                registrar.TryResolve(out currentLocation);
                registrar.TryResolve(out reputationReader);
                registrar.TryResolve(out tradeGoods);
                registrar.TryResolve(out map);
            }
            if (questActions == null) Debug.LogWarning($"{nameof(GuildQuestBoard)}: 퀘스트 계약이 없어 게시판이 비어 있다(Tools > Game > Build Bootstrap Scene).");
            Initialize(questActions, currentLocation, reputationReader, tradeGoods, map);
        }

        /// <summary>DI 없이 의존성을 넣는다(테스트용 겸 ResolveDependencies 공용 경로).</summary>
        public void Initialize(IQuestActions questActions, ITripCurrentLocationReader currentLocation, IReputationReader reputationReader, ITradeGoodsInventoryRepository tradeGoods, IWorldMapReader map)
        {
            Unsubscribe();
            quests = questActions;
            location = currentLocation;
            reputation = reputationReader;
            inventory = tradeGoods;
            worldMap = map;
            Subscribe();
        }

        private int City => location?.CurrentCityId ?? -1;

        public IReadOnlyList<GuildQuestInfo> Quests
        {
            get
            {
                var list = new List<GuildQuestInfo>();
                if (quests == null) return list;
                var city = City;
                var reportable = new List<GuildQuestInfo>();
                var active = new List<GuildQuestInfo>();
                foreach (var instance in quests.Active)
                {
                    if (!quests.TryGetDefinition(instance.QuestId, out var def)) continue;
                    if (instance.State == QuestState.Reportable && def.ReportCityId == city) reportable.Add(Row(def, GuildQuestStatus.Reportable));
                    else if (instance.SourceCityId == city) active.Add(Row(def, GuildQuestStatus.Active));
                }
                list.AddRange(reportable);
                list.AddRange(active);
                foreach (var def in quests.PostedAt(city)) list.Add(Row(def, GuildQuestStatus.Available));
                return list;
            }
        }

        public GuildQuestDetail GetDetail(string questId)
        {
            if (quests == null || !quests.TryGetDefinition(questId, out var def)) return null;
            quests.TryGetInstance(questId, out var instance);
            var today = quests.Today;
            return new GuildQuestDetail
            {
                Title = def.Title,
                Requester = def.Requester,
                Description = def.Description,
                Objectives = GuildQuestText.Objectives(def, instance, CountItem, CityName, ItemName, today),
                Deadline = GuildQuestText.Deadline(def, instance, today),
                Reward = GuildQuestText.Reward(def, ItemName, CityName),
                Status = instance == null ? GuildQuestStatus.Available
                    : instance.State == QuestState.Reportable && def.ReportCityId == City ? GuildQuestStatus.Reportable : GuildQuestStatus.Active,
                IsReportType = def.Completion == QuestCompletion.Report,
                Penalty = GuildQuestText.Penalty(def),
            };
        }

        public bool CanAccept(string questId, out string reason)
        {
            reason = null;
            return quests != null && quests.CanAccept(questId, City, out reason);
        }

        public bool CanAbandon(string questId, out string reason)
        {
            reason = null;
            return quests != null && quests.CanAbandon(questId, out reason);
        }

        public bool CanReport(string questId, out string reason)
        {
            reason = null;
            return quests != null && quests.CanReport(questId, City, out reason);
        }

        public bool TryAccept(string questId) => quests != null && quests.TryStart(questId, QuestStartSource.Request, City, out _);
        public bool TryAbandon(string questId) => quests != null && quests.TryAbandon(questId, out _);
        public bool TryReport(string questId) => quests != null && quests.TryReport(questId, City, out _);

        private static GuildQuestInfo Row(QuestDefinition def, GuildQuestStatus status) => new(def.Id, def.Title, def.Requester, status);

        private int CountItem(string itemId) => quests?.CountOwned(itemId) ?? 0;

        private string ItemName(string itemId)
            => inventory != null && itemId != null && inventory.TryGetDefinition(itemId, out var definition) ? definition.DisplayName : PlaceholderText.Missing;

        private string CityName(int cityId) => TripCityNames.DisplayName(worldMap, cityId);

        private void Raise() => Changed?.Invoke();

        private void Subscribe()
        {
            if (quests != null) quests.Changed += Raise;
            if (location != null) location.Changed += Raise;
            if (reputation != null) reputation.Changed += Raise;
            if (inventory != null) ((IInventoryReader)inventory).OnChanged += Raise;
        }

        private void Unsubscribe()
        {
            if (quests != null) quests.Changed -= Raise;
            if (location != null) location.Changed -= Raise;
            if (reputation != null) reputation.Changed -= Raise;
            if (inventory != null) ((IInventoryReader)inventory).OnChanged -= Raise;
        }

        private void OnDestroy() => Unsubscribe();
    }
}
