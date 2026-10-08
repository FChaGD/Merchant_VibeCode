using System;
using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>
    /// 보유 마차·시설 개체 장부(Docs/설계/81번 §5.1). 개체 Id는 "{종류 Id}#{n}"이고 n은 종류별로 늘어나기만 한다 - 정비창 배치·적재 섹션·전투가
    /// 개체 Id를 붙잡고 있으므로, 표시 번호(같은 종류 안 순서)와 Id를 분리해 제거 시 재번호가 Id를 바꾸지 않게 한다(기획 80번 §4-1).
    /// Id 형식을 아는 곳은 이 클래스뿐이다 - 다른 곳은 TryGetKindId로만 종류를 얻는다.
    /// </summary>
    public sealed class OwnedCaravanAssetRegistry
    {
        private readonly Dictionary<string, int> lastSerialByKindId = new();
        private readonly Dictionary<string, (string kindId, FormationUnitKind kind)> byInstanceId = new();
        private readonly Dictionary<FormationUnitKind, List<string>> idsByKind = new();

        public string Add(string kindId, FormationUnitKind kind)
        {
            var serial = (lastSerialByKindId.TryGetValue(kindId, out var last) ? last : 0) + 1;
            lastSerialByKindId[kindId] = serial;

            var instanceId = $"{kindId}#{serial}";
            byInstanceId[instanceId] = (kindId, kind);
            if (!idsByKind.TryGetValue(kind, out var ids)) idsByKind[kind] = ids = new List<string>();
            ids.Add(instanceId);
            return instanceId;
        }

        public bool Remove(string instanceId)
        {
            if (instanceId == null || !byInstanceId.TryGetValue(instanceId, out var info)) return false;
            byInstanceId.Remove(instanceId);
            idsByKind[info.kind].Remove(instanceId);
            return true;
        }

        public bool TryGetKindId(string instanceId, out string kindId)
        {
            kindId = null;
            if (instanceId == null || !byInstanceId.TryGetValue(instanceId, out var info)) return false;
            kindId = info.kindId;
            return true;
        }

        public IReadOnlyList<string> GetIds(FormationUnitKind kind) => idsByKind.TryGetValue(kind, out var ids) ? ids : Array.Empty<string>();

        public int Count(FormationUnitKind kind) => GetIds(kind).Count;

        // 저장하지 않고 매번 센다 - 제거되면 다음 조회부터 자동으로 당겨진다.
        public int NumberOf(string instanceId)
        {
            if (instanceId == null || !byInstanceId.TryGetValue(instanceId, out var info)) return 0;
            var number = 0;
            foreach (var id in idsByKind[info.kind])
            {
                if (byInstanceId[id].kindId == info.kindId) number++;
                if (id == instanceId) return number;
            }
            return 0;
        }

        // 일련번호 카운터도 초기화한다 - DI 재해결(새 게임 상태)마다 처음부터 발급한다.
        public void Clear()
        {
            lastSerialByKindId.Clear();
            byInstanceId.Clear();
            idsByKind.Clear();
        }
    }
}
