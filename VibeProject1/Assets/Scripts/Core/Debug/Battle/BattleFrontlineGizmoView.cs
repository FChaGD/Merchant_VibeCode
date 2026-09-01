#if UNITY_EDITOR
using Game.Core;
using UnityEngine;

namespace Game.Core.DebugTools
{
    /// <summary>
    /// 디버깅 전용 - 방진선(Frontline Formation Line), 그 위 유닛 배치 슬롯 위치, 그리고 보호대상
    /// →라인 연결선을 Scene 뷰에 그린다. 다른 전투 디버그 기즈모(BattleSurroundGizmoView,
    /// BattleMoveTargetGizmoView)와 완전히 독립적이다 - 이 기능만 빼고 싶으면 이 파일(+.meta) 삭제
    /// 후 Tools/Game/Debug/Remove Battle Gizmos를 한 번 실행하면 된다(설치/제거는
    /// BattleGizmoInstaller가 담당 - ManagerHierarchyInstaller가 아님, "게임 빌드"와 "디버그 도구
    /// 켜고 끄기"는 다른 관심사라 분리함). BattleSimulationLoop.FrontlineCoordinator/
    /// FrontlineFormationLine.AnchorCandidates는 다른 기즈모가 안 쓰므로 함께 지워도 안전(각 프로퍼티
    /// 주석 참고). BattleManager와 같은 GameObject에 부착된 형제 컴포넌트로, 전역 DI 대상이
    /// 아니다(Awake에서 GetComponent&lt;IBattleSimulationEvents&gt;()로 직접 구독).
    /// </summary>
    public class BattleFrontlineGizmoView : MonoBehaviour
    {
        private static readonly Color LineColor = new(1f, 0.5f, 0f, 1f); // 주황
        private static readonly Color SlotColor = Color.green;
        private static readonly Color AnchorLineColor = Color.green;
        private const float SlotMarkerSize = 0.4f;

        private BattleSimulationLoop simulation;

        private void Awake()
        {
            var events = GetComponent<IBattleSimulationEvents>();
            if (events != null) events.OnSimulationBuilt += loop => simulation = loop;

            // 배틀 테스트 씬(BattleTestSimulationRule)에서만 구현되는 마커 - 리셋 시 캐싱해둔
            // simulation 참조를 지워야 리셋 이후에도 이전 전투의 라인이 계속 그려지는 걸 막는다.
            // 실제 게임의 LiveBattleSimulationRule은 이 인터페이스를 구현하지 않아 그냥 건너뛴다.
            var resettable = GetComponent<IResettableBattleSimulation>();
            if (resettable != null) resettable.OnReset += () => simulation = null;
        }

        private void OnDrawGizmos()
        {
            if (!Application.isPlaying || simulation == null) return;

            foreach (var line in simulation.FrontlineCoordinator.ActiveLines)
            {
                // 슬롯 간격(1m) 기준으로 소속 인원 수만큼은 항상 보이도록 여유를 두고 길이를 잡는다 -
                // 정확한 점유 범위(offset min/max)는 FrontlineFormationLine이 private로 감춰뒀으므로
                // (Docs/설계/12번 §12.6) 근사치다. 실제 슬롯 위치는 아래에서 정확히 그린다.
                var halfLength = Mathf.Max(4f, line.Members.Count * TacticsTuning.LineSlotSpacingMeters * 0.6f);
                var lineStart = line.LinePoint - line.LineDir * halfLength;
                var lineEnd = line.LinePoint + line.LineDir * halfLength;
                Gizmos.color = LineColor;
                Gizmos.DrawLine(ToWorld(lineStart), ToWorld(lineEnd));

                Gizmos.color = SlotColor;
                foreach (var member in line.Members)
                {
                    if (line.TryGetSlotPosition(member, out var slotPosition))
                    {
                        DrawCross(slotPosition, SlotMarkerSize);
                    }
                }

                // 이 라인이 실제로 누구를 보호하고 있는지(§12.3 앵커 후보) 초록 선으로 잇는다 -
                // 보호대상이 여럿이거나 여러 라인이 있을 때 "이 라인이 지키는 대상"을 한눈에 구분하기 위함.
                Gizmos.color = AnchorLineColor;
                foreach (var candidate in line.AnchorCandidates)
                {
                    Gizmos.DrawLine(ToWorld(candidate.Position), ToWorld(line.LinePoint));
                }
            }
        }

        private static void DrawCross(Vector2 center, float size)
        {
            var half = size * 0.5f;
            Gizmos.DrawLine(ToWorld(center + new Vector2(-half, 0f)), ToWorld(center + new Vector2(half, 0f)));
            Gizmos.DrawLine(ToWorld(center + new Vector2(0f, -half)), ToWorld(center + new Vector2(0f, half)));
        }

        private static Vector3 ToWorld(Vector2 position) => new(position.x, position.y, 0f);
    }
}
#endif
