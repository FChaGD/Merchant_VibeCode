using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 도로 난이도의 현재값(Docs/설계/76번 §4.4). 기본값은 지도 데이터(RoadData.Difficulty)에 있고, 게임 중 바뀐 값만 여기 둔다 - 데이터(기본값)와
    /// 게임 상태(현재값)를 나눠야 이벤트가 값을 바꿔도 엑셀·자산이 흔들리지 않는다. 플레이 세션 동안만 유지(현재 위치 저장소와 같은 수준).
    /// 값을 바꾸는 이벤트는 향후(기획 75번 §6)라 지금은 바뀐 값이 비어 있다.
    /// </summary>
    public class RoadDifficultyState : MonoBehaviour, IRoadDifficultyRepository, IManagedComponent
    {
        private readonly Dictionary<RoadKey, int> overrides = new();
        private IWorldMapReader worldMap;

        public void RegisterSelf(IDependencyRegistrar registrar)
        {
            registrar.Register<IRoadDifficultyReader>(this);
            registrar.Register<IRoadDifficultyRepository>(this);
        }

        public void ResolveDependencies(IDependencyResolver registrar)
        {
            registrar.TryResolve(out worldMap);
        }

        public int GetDifficulty(RoadKey road)
        {
            if (overrides.TryGetValue(road, out var value)) return value;
            return worldMap != null ? worldMap.GetRoadBaseDifficulty(road) : TripTravelSettings.DefaultRoadDifficulty;
        }

        public void SetDifficulty(RoadKey road, int difficulty) => overrides[road] = TripTravelRules.ClampDifficulty(difficulty);

        public void ResetDifficulty(RoadKey road) => overrides.Remove(road);
    }
}
