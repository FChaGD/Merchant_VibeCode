using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 이력(EncounterHistory)의 Bootstrap 상주 껍데기(설계 87 §4). 계약 3종을 각 타입으로 따로 등록한다 - 상위 타입 등록으로는 조회되지 않는다.
    /// 상행 시작 신호에 이번 상행 기록을 비운다.
    /// </summary>
    public class EncounterHistoryStore : MonoBehaviour, IEncounterHistoryReader, IEncounterHistoryWriter, IEncounterScriptVariableStore, IManagedComponent
    {
        private readonly EncounterHistory history = new();
        private ITripMilestoneSource milestones;

        public void RegisterSelf(IDependencyRegistrar registrar)
        {
            registrar.Register<IEncounterHistoryReader>(this);
            registrar.Register<IEncounterHistoryWriter>(this);
            registrar.Register<IEncounterScriptVariableStore>(this);
        }

        public void ResolveDependencies(IDependencyResolver registrar)
        {
            if (milestones != null) milestones.Raised -= HandleMilestone;
            if (registrar.TryResolve(out milestones)) milestones.Raised += HandleMilestone;
        }

        private void HandleMilestone(TripMilestone milestone)
        {
            if (milestone == TripMilestone.TripStarted) history.ResetTrip();
        }

        private void OnDestroy()
        {
            if (milestones != null) milestones.Raised -= HandleMilestone;
        }

        public bool HasFlag(string flag) => history.HasFlag(flag);
        public bool TryGetFlag(string flag, out bool value) => history.TryGetFlag(flag, out value);
        public bool HasOccurred(string contentId) => history.HasOccurred(contentId);
        public bool OccurredThisTrip(string contentId) => history.OccurredThisTrip(contentId);
        public IReadOnlyCollection<string> ActiveFlags => history.ActiveFlags;
        public IReadOnlyCollection<string> OccurredIds => history.OccurredIds;
        public IReadOnlyCollection<string> TripOccurredIds => history.TripOccurredIds;
        public void SetFlag(string flag, bool value) => history.SetFlag(flag, value);
        public void RecordCompleted(string contentId) => history.RecordCompleted(contentId);
        public void ResetTrip() => history.ResetTrip();
        public void ResetAll() => history.ResetAll();
        public bool TryGetScriptValue(string name, out object value) => history.TryGetScriptValue(name, out value);
        public void SetScriptValue(string name, object value) => history.SetScriptValue(name, value);
        public IReadOnlyDictionary<string, object> ScriptValues => history.ScriptValues;
    }
}
