using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>
    /// 이력의 순수 구현(설계 87 §4). 플레이 세션 동안만 유지 - 저장 기능은 범위 밖이다(기획 86 §2). MonoBehaviour 껍데기(EncounterHistoryStore)가
    /// DI 등록과 상행 시작 신호 처리만 맡아, 규칙은 EditMode에서 그대로 검증한다.
    /// </summary>
    public sealed class EncounterHistory : IEncounterHistoryReader, IEncounterHistoryWriter, IEncounterScriptVariableStore
    {
        private readonly HashSet<string> flags = new();
        private readonly HashSet<string> clearedFlags = new();  // false로 설정된 플래그(미설정과 구분 - TryGetFlag)
        private readonly HashSet<string> occurred = new();
        private readonly HashSet<string> tripOccurred = new();
        private readonly Dictionary<string, object> scriptValues = new();

        public IReadOnlyCollection<string> ActiveFlags => flags;
        public IReadOnlyCollection<string> OccurredIds => occurred;
        public IReadOnlyCollection<string> TripOccurredIds => tripOccurred;
        public IReadOnlyDictionary<string, object> ScriptValues => scriptValues;

        public bool HasFlag(string flag) => !string.IsNullOrEmpty(flag) && flags.Contains(flag);
        public bool HasOccurred(string contentId) => !string.IsNullOrEmpty(contentId) && occurred.Contains(contentId);
        public bool OccurredThisTrip(string contentId) => !string.IsNullOrEmpty(contentId) && tripOccurred.Contains(contentId);

        public bool TryGetFlag(string flag, out bool value)
        {
            value = !string.IsNullOrEmpty(flag) && flags.Contains(flag);
            return value || (!string.IsNullOrEmpty(flag) && clearedFlags.Contains(flag));
        }

        public void SetFlag(string flag, bool value)
        {
            if (string.IsNullOrEmpty(flag)) return;
            if (value)
            {
                flags.Add(flag);
                clearedFlags.Remove(flag);
            }
            else
            {
                flags.Remove(flag);
                clearedFlags.Add(flag);
            }
        }

        public void RecordCompleted(string contentId)
        {
            if (string.IsNullOrEmpty(contentId)) return;
            occurred.Add(contentId);
            tripOccurred.Add(contentId);
        }

        public void ResetTrip() => tripOccurred.Clear();

        public void ResetAll()
        {
            flags.Clear();
            clearedFlags.Clear();
            occurred.Clear();
            tripOccurred.Clear();
            scriptValues.Clear();
        }

        public bool TryGetScriptValue(string name, out object value)
        {
            if (string.IsNullOrEmpty(name))
            {
                value = null;
                return false;
            }
            return scriptValues.TryGetValue(name, out value);
        }

        public void SetScriptValue(string name, object value)
        {
            if (string.IsNullOrEmpty(name)) return;
            scriptValues[name] = value;
        }
    }
}
