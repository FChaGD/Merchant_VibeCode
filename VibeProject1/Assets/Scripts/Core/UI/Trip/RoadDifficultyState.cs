using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 도로 난이도 상태(Docs/설계/76번 §4.4 → 설계 89 §5.2로 교체). 도로 조각의 기본값은 지도 데이터에서 읽기만 하고, 게임 중 변경은 도시 쌍 변경 항목으로 둔다 -
    /// 관문을 지나는 구간도 플레이어에게는 도로 하나이고, 같은 관문을 쓰는 다른 도시 쌍에 변경이 새지 않아야 하기 때문이다. 게임 날짜 변경 신호에서
    /// 만료일이 지난 기간 항목만 빠진다(다른 기간 항목·영구 항목은 남음). 데이터(기본값)와 게임 상태(변경 항목)를 나눠야 이벤트가 값을 바꿔도 엑셀·자산이 흔들리지 않는다.
    /// 플레이 세션 동안만 유지(현재 위치 저장소와 같은 수준).
    /// </summary>
    public class RoadDifficultyState : MonoBehaviour, IRoadDifficultyReader, ICityPairDifficultyEditor, IManagedComponent
    {
        private readonly CityPairDifficultyOverrides overrides = new();
        private IWorldMapReader worldMap;
        private IDateReader date; // 선택적 - 없으면 기간 변경은 받지 않는다(만료되지 않고 영구로 남기 때문)

        public event Action<CityPairKey> Changed;

        public void RegisterSelf(IDependencyRegistrar registrar)
        {
            registrar.Register<IRoadDifficultyReader>(this);
            registrar.Register<ICityPairDifficultyReader>(this);
            registrar.Register<ICityPairDifficultyEditor>(this);
        }

        public void ResolveDependencies(IDependencyResolver registrar)
        {
            registrar.TryResolve(out worldMap);
            if (date != null) date.Changed -= HandleDateChanged;
            if (registrar.TryResolve(out date)) date.Changed += HandleDateChanged;
        }

        public int GetDifficulty(RoadKey road) => worldMap != null ? worldMap.GetRoadBaseDifficulty(road) : TripTravelSettings.DefaultRoadDifficulty;

        public int GetDifficulty(CityPairKey pair, int baseDifficulty) => overrides.GetDifficulty(pair, baseDifficulty);

        public IReadOnlyList<CityPairDifficultyChange> GetChanges(CityPairKey pair) => overrides.GetChanges(pair);

        // 사건 Id의 월일은 오늘 날짜 - 날짜 계약이 없으면 1월 1일.
        public string Apply(CityPairKey pair, string sourceEventId, int change, int? expiresOnDay)
        {
            if (expiresOnDay.HasValue && date == null) return null;
            var id = overrides.Apply(pair, sourceEventId, date != null ? date.Today : new GameDate(0), change, expiresOnDay);
            if (id != null) Changed?.Invoke(pair);
            return id;
        }

        private void HandleDateChanged()
        {
            foreach (var pair in overrides.ExpireThrough(date.Today.TotalDays)) Changed?.Invoke(pair);
        }

        private void OnDestroy()
        {
            if (date != null) date.Changed -= HandleDateChanged;
        }
    }
}
