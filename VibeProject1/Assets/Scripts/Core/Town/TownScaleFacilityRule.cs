using System;
using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>
    /// 마을 규모로 시설 제공 여부를 판정한다(Docs/기획/57번, 설계 58번 §5.3). 포함 관계를 "마을 규모 순서 ≥ 시설 최소 규모 순서"
    /// 하나로 판정해, 규모별 시설 목록을 따로 두지 않아도 포함 관계가 깨지지 않는다. MonoBehaviour와 분리해 EditMode 테스트 대상으로 둔다.
    /// 확장 지점: 마을별 시설 예외(도시 Id + 시설 Id + 추가/제외)가 생기면 IsFacilityAvailable 맨 앞에서 예외를 먼저 보고, 없을 때만
    /// 규모 규칙으로 내려가게 끼운다(기획 57번 §6).
    /// </summary>
    public sealed class TownScaleFacilityRule
    {
        private readonly List<TownScale> scales = new();
        private readonly Dictionary<string, TownScale> scaleById = new();
        private readonly Dictionary<string, string> facilityMinScale;
        private readonly IReadOnlyDictionary<int, string> scaleIdByCityId;
        private readonly Action<string> warn;
        private readonly HashSet<string> warnedKeys = new();

        public IReadOnlyList<TownScale> Scales => scales;

        /// <param name="scalesInOrder">작은 규모 → 큰 규모 순서.</param>
        /// <param name="facilityMinScale">시설 Id → 최소 규모 Id.</param>
        /// <param name="scaleIdByCityId">도시 Id → 규모 Id(도시 테이블 Scale 열, 빈 값 가능).</param>
        public TownScaleFacilityRule(IEnumerable<(string Id, string Label)> scalesInOrder, IReadOnlyDictionary<string, string> facilityMinScale, IReadOnlyDictionary<int, string> scaleIdByCityId, Action<string> warn)
        {
            foreach (var (id, label) in scalesInOrder)
            {
                if (string.IsNullOrWhiteSpace(id) || scaleById.ContainsKey(id)) continue;
                var scale = new TownScale(id, label, scales.Count);
                scales.Add(scale);
                scaleById[id] = scale;
            }

            this.facilityMinScale = new Dictionary<string, string>(facilityMinScale);
            this.scaleIdByCityId = scaleIdByCityId;
            this.warn = warn;
        }

        // 규모가 비었거나 정의되지 않은 규모 Id면 가장 작은 규모로 취급한다(기획 57번 §4.3) - 시설이 과하게 열리지 않게 한다.
        // 도시 자산에 없는 도시(현재 위치 미지정 등)도 가장 작은 규모다.
        public TownScale GetScale(int cityId)
        {
            if (scales.Count == 0) return default;

            if (scaleIdByCityId != null && scaleIdByCityId.TryGetValue(cityId, out var scaleId))
            {
                if (!string.IsNullOrWhiteSpace(scaleId) && scaleById.TryGetValue(scaleId, out var scale)) return scale;

                WarnOnce($"city:{cityId}", $"도시 {cityId}의 규모 '{scaleId}'가 비었거나 정의되지 않았다 - 가장 작은 규모({scales[0].Label})로 취급한다.");
            }

            return scales[0];
        }

        public bool IsFacilityAvailable(int cityId, string facilityId)
        {
            // 규모 정의가 하나도 없으면 판정할 기준이 없다 - 이전 동작(전부 제공)을 유지한다.
            if (scales.Count == 0) return true;

            if (!facilityMinScale.TryGetValue(facilityId, out var minScaleId) || !scaleById.TryGetValue(minScaleId ?? string.Empty, out var minScale))
            {
                WarnOnce($"facility:{facilityId}", $"시설 '{facilityId}'의 최소 규모가 없거나 정의되지 않은 규모다 - 모든 마을에서 제공한다.");
                return true;
            }

            return GetScale(cityId).Rank >= minScale.Rank;
        }

        private void WarnOnce(string key, string message)
        {
            if (warnedKeys.Add(key)) warn?.Invoke(message);
        }
    }
}
