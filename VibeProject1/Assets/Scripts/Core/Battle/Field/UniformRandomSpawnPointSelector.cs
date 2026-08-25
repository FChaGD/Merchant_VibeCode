using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// Docs/기획/08_전투_해석로직_기획.md §2 - 이번 단계는 균등 랜덤 스폰. 인카운터 종류별 스폰 지점
    /// 고정(매복 등)은 이 인터페이스의 다른 구현체로 교체하는 것으로 확장한다(OCP).
    /// </summary>
    public class UniformRandomSpawnPointSelector : IEncounterSpawnPointSelector
    {
        public int SelectSpawnPointIndex() => Random.Range(0, BattleFieldGeometry.SpawnPointCount);
    }
}
