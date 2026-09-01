#if UNITY_EDITOR
using Game.Core;
using UnityEngine;

namespace Game.Core.DebugTools
{
    /// <summary>
    /// 디버깅 전용 - 각 아군 유닛이 이번 틱 실제로 이동하려는 목적지(IBattleCombatant.
    /// DebugMoveTarget)까지 선을 그린다. 다른 전투 디버그 기즈모(BattleSurroundGizmoView,
    /// BattleFrontlineGizmoView)와 완전히 독립적이다 - 이 기능만 빼고 싶으면 이 파일(+.meta) 삭제
    /// 후 Tools/Game/Debug/Remove Battle Gizmos를 한 번 실행하면 된다(설치/제거는
    /// BattleGizmoInstaller가 담당 - ManagerHierarchyInstaller가 아님, "게임 빌드"와 "디버그 도구
    /// 켜고 끄기"는 다른 관심사라 분리함). IBattleCombatant.DebugMoveTarget/BattleCharacterUnit의
    /// 대응 필드도 다른 기즈모가 안 쓰므로 함께 지워도 안전(그 주석 참고). BattleManager와 같은
    /// GameObject에 부착된 형제 컴포넌트로, 전역 DI 대상이 아니다(Awake에서
    /// GetComponent&lt;IBattleSimulationEvents&gt;()로 직접 구독).
    /// </summary>
    public class BattleMoveTargetGizmoView : MonoBehaviour
    {
        private static readonly Color MoveTargetColor = Color.yellow;

        private BattleSimulationLoop simulation;

        private void Awake()
        {
            var events = GetComponent<IBattleSimulationEvents>();
            if (events != null) events.OnSimulationBuilt += loop => simulation = loop;

            // 배틀 테스트 씬(BattleTestSimulationRule)에서만 구현되는 마커 - 리셋 시 캐싱해둔
            // simulation 참조를 지워야 리셋 이후에도 이전 전투의 선이 계속 그려지는 걸 막는다.
            // 실제 게임의 LiveBattleSimulationRule은 이 인터페이스를 구현하지 않아 그냥 건너뛴다.
            var resettable = GetComponent<IResettableBattleSimulation>();
            if (resettable != null) resettable.OnReset += () => simulation = null;
        }

        private void OnDrawGizmos()
        {
            if (!Application.isPlaying || simulation == null) return;

            Gizmos.color = MoveTargetColor;
            foreach (var unit in simulation.Allies)
            {
                if (unit.IsAlive && unit.DebugMoveTarget.HasValue)
                {
                    Gizmos.DrawLine(ToWorld(unit.Position), ToWorld(unit.DebugMoveTarget.Value));
                }
            }
        }

        private static Vector3 ToWorld(Vector2 position) => new(position.x, position.y, 0f);
    }
}
#endif
