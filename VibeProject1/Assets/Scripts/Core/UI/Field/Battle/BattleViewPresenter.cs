using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 순수 C# 협력 객체. FieldUIController가 최초 1회 생성해 유지하고, IBattleSimulationEvents를
    /// 구독해 매 전투 시작마다 View를 새로 스폰한다(FieldEncounterFlowCoordinator와 같은
    /// Bind/RebindViews 2단 구조 - 이벤트 구독은 최초 1회, 씬 참조 교체는 Field 씬 로드마다).
    /// 시뮬레이션(LiveBattleSimulationRule)은 이 클래스를 전혀 모른다 - 렌더링과 로직을 분리한다.
    /// </summary>
    internal class BattleViewPresenter
    {
        private bool eventsBound;

        private RectTransform allyContainer;
        private RectTransform enemyContainer;
        private BattleCharacterUnitView characterViewPrefab;
        private BattleProtectedUnitView protectedViewPrefab;

        private readonly List<BattleCharacterUnitView> activeCharacterViews = new();
        private readonly List<BattleProtectedUnitView> activeProtectedViews = new();

        public void Bind(IBattleSimulationEvents simulationEvents)
        {
            if (eventsBound) return;

            simulationEvents.OnSimulationBuilt += Present;
            eventsBound = true;
        }

        public void RebindViews(
            RectTransform allyContainer, RectTransform enemyContainer,
            BattleCharacterUnitView characterViewPrefab, BattleProtectedUnitView protectedViewPrefab)
        {
            this.allyContainer = allyContainer;
            this.enemyContainer = enemyContainer;
            this.characterViewPrefab = characterViewPrefab;
            this.protectedViewPrefab = protectedViewPrefab;
        }

        private void Present(BattleSimulationLoop simulation)
        {
            Clear();
            foreach (var unit in simulation.Allies) SpawnCharacterView(unit, allyContainer);
            foreach (var unit in simulation.Enemies) SpawnCharacterView(unit, enemyContainer);
            foreach (var unit in simulation.ProtectedUnits) SpawnProtectedView(unit, allyContainer);
        }

        private void SpawnCharacterView(IBattleCombatant unit, RectTransform parent)
        {
            if (characterViewPrefab == null || parent == null) return;
            var view = Object.Instantiate(characterViewPrefab, parent);
            view.Bind(unit);
            activeCharacterViews.Add(view);
        }

        private void SpawnProtectedView(IDamageable unit, RectTransform parent)
        {
            if (protectedViewPrefab == null || parent == null) return;
            var view = Object.Instantiate(protectedViewPrefab, parent);
            view.Bind(unit);
            activeProtectedViews.Add(view);
        }

        // 다음 전투 시작 시 이전 전투의 View가 남아있지 않도록 정리한다. 개별 View는 사망/도주 시
        // 스스로 Destroy되지만(FadeAndDestroy), 전투가 도중에 중단되는 경우까지 대비한 안전장치다.
        private void Clear()
        {
            foreach (var view in activeCharacterViews) { if (view != null) Object.Destroy(view.gameObject); }
            foreach (var view in activeProtectedViews) { if (view != null) Object.Destroy(view.gameObject); }
            activeCharacterViews.Clear();
            activeProtectedViews.Clear();
        }
    }
}
