using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 진영 하나의 사기 파동 전체를 관리한다(기획 08번 §7.3, 설계 14번 §6) - PartyMorale과 같은 자리,
    /// 진영별로 하나씩 만들어진다. 매 틱 각 파동을 팽창시키고, "이전 반지름~이번 반지름" 사이(=테두리가
    /// 스쳐 지나간 순간)에 들어온 같은 진영 유닛에게 델타를 전달한 뒤, 전장 반지름을 넘어선 파동은
    /// 제거한다. 화면에 표시되는 시각 효과가 아니라 순수 판정용 개념이다(기획 §14.5 무관).
    /// 진영마다 코디네이터를 따로 둬서 "상대 진영에는 영향 없음"을 매 틱 필터링 없이 구조 자체로
    /// 보장한다 - Update에 그 진영 유닛 목록만 넘기면 된다.
    /// </summary>
    public class MoraleWaveCoordinator
    {
        private readonly List<MoraleWave> waves = new();
        private readonly float fieldRadius;

        public MoraleWaveCoordinator(float fieldRadius) => this.fieldRadius = fieldRadius;

        public void SpawnWave(Vector2 center, float delta) => waves.Add(new MoraleWave(center, delta));

        public void Update(float deltaTime, IReadOnlyList<IBattleCombatant> units)
        {
            for (var i = waves.Count - 1; i >= 0; i--)
            {
                var previousRadius = waves[i].Radius;
                var expanded = waves[i].WithExpandedRadius(deltaTime);

                foreach (var unit in units)
                {
                    if (!unit.IsAlive) continue;
                    var distance = (unit.Position - expanded.Center).magnitude;
                    if (distance > previousRadius && distance <= expanded.Radius)
                    {
                        unit.ReceiveMoraleWave(expanded.Delta);
                    }
                }

                if (expanded.Radius >= fieldRadius) waves.RemoveAt(i);
                else waves[i] = expanded;
            }
        }
    }
}
