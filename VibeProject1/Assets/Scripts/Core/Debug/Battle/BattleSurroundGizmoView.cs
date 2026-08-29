#if UNITY_EDITOR
using Game.Core;
using UnityEngine;

namespace Game.Core.DebugTools
{
    /// <summary>
    /// 디버깅 전용 - 포위망(Surround Ring) 원을 Scene 뷰에 그린다. 다른 전투 디버그 기즈모
    /// (BattleFrontlineGizmoView, BattleMoveTargetGizmoView)와 완전히 독립적이다 - 이 기능만
    /// 빼고 싶으면 이 파일(+.meta) 삭제 후 Tools/Game/Debug/Remove Battle Gizmos를 한 번 실행하면
    /// 된다(설치/제거는 BattleGizmoInstaller가 담당 - ManagerHierarchyInstaller가 아님, "게임 빌드"와
    /// "디버그 도구 켜고 끄기"는 다른 관심사라 분리함). BattleSimulationLoop.SurroundCoordinator는
    /// 다른 기즈모가 안 쓰므로 함께 지워도 안전(그 프로퍼티 주석 참고). BattleManager와 같은
    /// GameObject에 부착된 형제 컴포넌트로, 전역 DI 대상이 아니다(Awake에서
    /// GetComponent&lt;IBattleSimulationEvents&gt;()로 직접 구독).
    /// </summary>
    public class BattleSurroundGizmoView : MonoBehaviour
    {
        private static readonly Color RingColor = Color.cyan;
        private const int CircleSegments = 32;

        private BattleSimulationLoop simulation;

        private void Awake()
        {
            var events = GetComponent<IBattleSimulationEvents>();
            if (events != null) events.OnSimulationBuilt += loop => simulation = loop;
        }

        private void OnDrawGizmos()
        {
            if (!Application.isPlaying || simulation == null) return;

            Gizmos.color = RingColor;
            foreach (var ring in simulation.SurroundCoordinator.ActiveRings)
            {
                DrawCircle(ring.ClusterCenter, ring.CurrentRadius);
            }
        }

        // Gizmos.DrawWireSphere는 3개의 큰 원(XY/XZ/YZ)을 함께 그려 위에서 내려다보지 않으면 구처럼
        // 보인다 - 시야각과 무관하게 항상 평평한 원으로 보이도록 XY 평면 위 다각형으로 직접 그린다.
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
