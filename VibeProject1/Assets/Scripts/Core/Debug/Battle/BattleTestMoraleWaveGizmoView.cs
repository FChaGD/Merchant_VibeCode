#if UNITY_EDITOR
using Game.Core;
using UnityEngine;

namespace Game.Core.DebugTools
{
    /// <summary>
    /// 디버깅 전용 - 사기 파동(MoraleWave)이 팽창하는 모습을 원으로 그린다. 다른 전투 디버그
    /// 기즈모(BattleFrontlineGizmoView 등)와 같은 자리·같은 패턴이지만, 이 기즈모는 배틀 테스트 씬
    /// 전용이다 - BattleSimulationLoop.AllyWaveCoordinator/EnemyWaveCoordinator·
    /// MoraleWaveCoordinator.ActiveWaves 둘 다 이번에 추가된 디버그 전용 접근자라, 실제 Bootstrap
    /// 씬에도 설치는 가능하지만 지금은 배틀 테스트 씬에만 설치한다(BattleTestSceneInstaller).
    /// </summary>
    public class BattleTestMoraleWaveGizmoView : MonoBehaviour
    {
        private static readonly Color AllyWaveColor = Color.blue;
        private static readonly Color EnemyWaveColor = Color.red;
        private const int CircleSegments = 32;

        private BattleSimulationLoop simulation;

        private void Awake()
        {
            var events = GetComponent<IBattleSimulationEvents>();
            if (events != null) events.OnSimulationBuilt += loop => simulation = loop;

            var resettable = GetComponent<IResettableBattleSimulation>();
            if (resettable != null) resettable.OnReset += () => simulation = null;
        }

        private void OnDrawGizmos()
        {
            if (!Application.isPlaying || simulation == null) return;

            DrawWaves(simulation.AllyWaveCoordinator, AllyWaveColor);
            DrawWaves(simulation.EnemyWaveCoordinator, EnemyWaveColor);
        }

        private static void DrawWaves(MoraleWaveCoordinator coordinator, Color color)
        {
            Gizmos.color = color;
            foreach (var wave in coordinator.ActiveWaves)
            {
                DrawCircle(wave.Center, wave.Radius);
            }
        }

        // BattleSurroundGizmoView.DrawCircle과 같은 이유(DrawWireSphere는 시야각에 따라 구처럼 보임) -
        // XY 평면 위 다각형으로 직접 그린다.
        private static void DrawCircle(Vector2 center, float radius)
        {
            var previous = center + new Vector2(radius, 0f);
            for (var i = 1; i <= CircleSegments; i++)
            {
                var angle = i / (float)CircleSegments * Mathf.PI * 2f;
                var current = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                Gizmos.DrawLine(ToWorld(previous), ToWorld(current));
                previous = current;
            }
        }

        private static Vector3 ToWorld(Vector2 position) => new(position.x, position.y, 0f);
    }
}
#endif
