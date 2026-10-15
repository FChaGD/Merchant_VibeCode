#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Game.Core.DebugTools
{
    /// <summary>
    /// 디버깅 전용 - 전투 뷰 화면에 "강제 승리"/"강제 패배" 버튼을 그린다(OnGUI). 적이 약해 자연
    /// 패배를 재현하기 어려운 검증 시나리오(상행중 정비창 배치시간 설계 25번 §10 7번)를 위한 도구.
    /// 승패 판정 로직 자체(LiveBattleSimulationRule.Update의 IsEnemyWiped/IsAllyWiped)는 건드리지
    /// 않고, 반대 진영 전체에 즉사 피해(TakeDamage)를 입혀 그 판정이 자연스럽게 통과하도록 만드는
    /// 방식이라 실제 승리/패배 흐름(결과 팝업, 배치 활동 강제완료 등)을 그대로 재현한다. BattleManager와
    /// 같은 GameObject에 부착된 형제 컴포넌트로, 전역 DI 대상이 아니다(BattleSurroundGizmoView와
    /// 동일 자리 - Awake에서 GetComponent&lt;IBattleSimulationEvents&gt;()로 직접 구독). 설치/제거는
    /// BattleGizmoInstaller가 아니라 별도의 BattleDebugResultInstaller가 담당한다(게임 빌드와 디버그
    /// 도구 켜고 끄기는 다른 관심사).
    /// </summary>
    public class BattleDebugResultView : MonoBehaviour
    {
        private BattleSimulationLoop simulation;

        private void Awake()
        {
            var events = GetComponent<IBattleSimulationEvents>();
            if (events != null) events.OnSimulationBuilt += loop => simulation = loop;

            var resettable = GetComponent<IResettableBattleSimulation>();
            if (resettable != null) resettable.OnReset += () => simulation = null;

            // BattleManager는 Bootstrap 상주라 실전투가 끝나도 참조가 남으면 Field 이동 중·Hub에서도 버튼이 그려진다(2026-10-15 확인).
            // 실전투에는 리셋이 없으므로 전투 종료 시 지워 전투 중에만 보이게 한다.
            var resultSource = GetComponent<IBattleResultSource>();
            if (resultSource != null) resultSource.OnBattleEnded += _ => simulation = null;
        }

        private void OnGUI()
        {
            if (!Application.isPlaying || simulation == null) return;

            GUILayout.BeginArea(new Rect(10, 10, 180, 70));
            if (GUILayout.Button("[Debug] 강제 승리(적 전멸)"))
            {
                Wipe(simulation.Enemies, simulation.Allies.FirstOrDefault(a => a.IsAlive));
            }
            if (GUILayout.Button("[Debug] 강제 패배(아군 전멸)"))
            {
                Wipe(simulation.Allies, simulation.Enemies.FirstOrDefault(e => e.IsAlive));
            }
            GUILayout.EndArea();
        }

        // attacker가 반대 진영에서 살아있는 유닛이어야 TakeDamage 내부의 처치 통지(NotifyKilledEnemy)가
        // 안전하다 - 한쪽이 이미 전멸 상태면(공격 대상 진영이 attacker 자신의 진영인 극단 상황) 아무
        // 것도 하지 않는다.
        private static void Wipe(IReadOnlyList<IBattleCombatant> targets, IBattleCombatant attacker)
        {
            if (attacker == null) return;
            foreach (var target in targets)
            {
                if (target.IsAlive) target.TakeDamage(float.MaxValue, attacker);
            }
        }
    }
}
#endif
