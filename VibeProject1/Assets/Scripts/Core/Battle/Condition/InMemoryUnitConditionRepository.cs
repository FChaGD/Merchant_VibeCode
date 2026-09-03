using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// IUnitConditionRepository의 인메모리 구현(설계 15번) - InMemoryFormationRepository와 같은 성격
    /// (Bootstrap 상주, 앱 재시작 시 소멸). 상행 스코프 데이터라 이 생명주기가 정확히 맞는다.
    /// </summary>
    public class InMemoryUnitConditionRepository : MonoBehaviour, IUnitConditionRepository, IManagedComponent
    {
        private readonly Dictionary<string, float> currentHpById = new();
        private readonly HashSet<string> deadUnitIds = new();
        // LiveBattleSimulationRule/BattleTestSimulationRule과 같은 기존 패턴 - 이 타입은 DI 등록
        // 대상이 아니라 필요한 곳마다 직접 new한다.
        private IBattleUnitStatProvider statProvider;

        // 엑셀 임포트 결과 테이블(Docs/설계/17번 §6) - roleGroupMap과 같은 배선 전례.
        [SerializeField] private CharacterStatsTableAsset characterStatsTable;

        private ICaravanRosterProvider rosterProvider;

        private void Awake() => statProvider = new TableBattleUnitStatProvider(characterStatsTable);

        public void RegisterSelf(IDependencyRegistrar registrar) => registrar.Register<IUnitConditionRepository>(this);

        public void ResolveDependencies(IDependencyRegistrar registrar) => registrar.TryResolve(out rosterProvider);

        public bool TryGetCurrentHp(string unitId, out float currentHp) => currentHpById.TryGetValue(unitId, out currentHp);

        public bool IsDead(string unitId) => deadUnitIds.Contains(unitId);

        public void ApplyBattleResult(string unitId, float currentHp, bool died)
        {
            if (died)
            {
                deadUnitIds.Add(unitId);
                currentHpById.Remove(unitId);
            }
            else
            {
                currentHpById[unitId] = currentHp;
            }
        }

        public void ResetAllToFull()
        {
            currentHpById.Clear();
            deadUnitIds.Clear();

            if (rosterProvider == null) return;

            foreach (var unit in rosterProvider.GetRoster())
            {
                // 캐릭터(용병)만 HP 추적 대상 - 마차/시설은 전투에 참여하지 않는다(기획 13번 §2).
                if (unit is IMercenaryUnit mercenary)
                {
                    currentHpById[unit.Id] = statProvider.GetStats(mercenary.Class).MaxHp;
                }
            }
        }
    }
}
