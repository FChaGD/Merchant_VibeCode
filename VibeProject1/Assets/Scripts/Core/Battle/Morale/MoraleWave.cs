using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 사기 파동 1개의 상태(기획 08번 §7.3, 설계 14번 §6) - 순수 데이터, 변경(팽창)은
    /// MoraleWaveCoordinator가 담당한다(SRP). 불변 구조체라 반지름 갱신은 새 인스턴스 교체로 처리한다.
    /// </summary>
    public readonly struct MoraleWave
    {
        public Vector2 Center { get; }
        public float Delta { get; } // §7.1/§7.2 최종 델타(배율 반영 후)
        public float Speed { get; } // §7.3 공식으로 생성 시점에 1회 계산해 고정
        public float Radius { get; }

        public MoraleWave(Vector2 center, float delta, float radius = 0f)
        {
            Center = center;
            Delta = delta;
            Speed = Mathf.Min(MoraleTuning.WaveSpeedCap, MoraleTuning.WaveSpeedPerDeltaUnit * Mathf.Abs(delta) / 10f);
            Radius = radius;
        }

        public MoraleWave WithExpandedRadius(float deltaTime) => new(Center, Delta, Radius + Speed * deltaTime);
    }
}
