using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>
    /// 배틀 테스트 씬 전용 - 고정 스폰 포인트(BattleFieldGeometry.SpawnPointCount=12) 각각에 "전투
    /// 시작 시 이 지점에서 타입별로 몇 마리가 나온다"는 예약만 저장하는 순수 데이터 홀더. 한 지점에
    /// 여러 타입을 동시에 예약할 수 있다(적 구성 편집 패널과 같은 "타입별 개수" 입력 방식, 사용자
    /// 요청: "각 타입 옆에 숫자 입력칸"). 로스터(BattleTestEnemyRoster)와 별개다 - 여기 기록한다고
    /// 바로 필드에 유닛이 보이지 않는다(사용자 확인: "전투 시작 전 예약" 방식).
    /// BattleTestSimulationRule.Evaluate()가 전투를 시작할 때만 이 예약을 로스터 엔트리로 변환한다.
    /// </summary>
    public class BattleTestSpawnPointReservations
    {
        public readonly struct Composition
        {
            public readonly int Marauder;
            public readonly int Monster;
            public readonly int Adversary;

            public Composition(int marauder, int monster, int adversary)
            {
                Marauder = marauder > 0 ? marauder : 0;
                Monster = monster > 0 ? monster : 0;
                Adversary = adversary > 0 ? adversary : 0;
            }

            public bool IsEmpty => Marauder <= 0 && Monster <= 0 && Adversary <= 0;

            public int GetCount(EnemyType type) => type switch
            {
                EnemyType.Marauder => Marauder,
                EnemyType.Monster => Monster,
                EnemyType.Adversary => Adversary,
                _ => 0,
            };
        }

        private readonly Dictionary<int, Composition> reservations = new();

        public Composition Get(int spawnPointIndex) => reservations.TryGetValue(spawnPointIndex, out var composition) ? composition : default;

        public void Set(int spawnPointIndex, Composition composition)
        {
            if (composition.IsEmpty) reservations.Remove(spawnPointIndex);
            else reservations[spawnPointIndex] = composition;
        }

        public IReadOnlyDictionary<int, Composition> All => reservations;
    }
}
