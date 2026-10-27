using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 순수 C# 협력 객체. FieldUIController가 최초 1회 생성해 유지하고, IBattleSimulationEvents를
    /// 구독해 매 전투 시작마다 View를 새로 스폰한다(FieldEncounterFlowCoordinator와 같은
    /// Bind/RebindViews 2단 구조 - 이벤트 구독은 최초 1회, 씬 참조 교체는 Field 씬 로드마다).
    /// 시뮬레이션(LiveBattleSimulationRule)은 이 클래스를 전혀 모른다 - 렌더링과 로직을 분리한다.
    /// 유닛 뷰는 풀로 재사용한다(Docs/Refactor/2026-10-26_씬전용.md A-1·A-3) - 배틀 테스트 컨트롤러도 미리보기 뷰를 이 클래스를 통해서만
    /// 만들고 지워, 같은 컨테이너에 생성 경로가 둘이던 것(SS-8)을 하나로 합쳤다. 풀은 이번 씬의 컨테이너에 속하므로 RebindViews마다 새로 만든다.
    /// </summary>
    internal class BattleViewPresenter
    {
        private bool eventsBound;

        // 월드 오브젝트 전환(Docs/설계/13번) - Canvas 하위 RectTransform이 아니라 BattleWorldRoot
        // 산하 일반 Transform이다(UI 좌표계와 섞이지 않도록).
        private Transform allyContainer;
        private Transform enemyContainer;
        private BattleCharacterUnitView characterViewPrefab;
        private BattleProtectedUnitView protectedViewPrefab;
        private BattlePendingReinforcementView pendingReinforcementViewPrefab;
        private BattleFieldWorldCameraView cameraView;
        private BattleBackgroundGridView backgroundView;

        private ComponentPool<BattleCharacterUnitView> characterPool = new();
        private ComponentPool<BattleProtectedUnitView> protectedPool = new();
        // Field 배치 시간(설계 25번 §6.3) 전투 중 신규 소환 유령 - get-or-create 재사용(매 틱 갱신되므로
        // 파괴 후 재생성하면 낭비가 크다).
        private readonly List<BattlePendingReinforcementView> activeReinforcementViews = new();

        public void Bind(IBattleSimulationEvents simulationEvents)
        {
            if (eventsBound) return;

            simulationEvents.OnSimulationBuilt += Present;
            // Field 배치 시간(Docs/설계/25번 §6.2) 타이머가 전투 도중 완료돼 아군이 늦게 합류할 때 -
            // OnSimulationBuilt(전투 시작 1회)에는 반영되지 않는 유닛이라 뷰를 별도로 스폰해야 한다.
            simulationEvents.OnAllySpawnedMidBattle += unit => SpawnCharacter(unit, isAlly: true);
            simulationEvents.OnPendingReinforcementsChanged += HandlePendingReinforcementsChanged;
            eventsBound = true;
        }

        public void RebindViews(
            Transform allyContainer, Transform enemyContainer,
            BattleCharacterUnitView characterViewPrefab, BattleProtectedUnitView protectedViewPrefab,
            BattlePendingReinforcementView pendingReinforcementViewPrefab,
            BattleFieldWorldCameraView cameraView, BattleBackgroundGridView backgroundView)
        {
            this.allyContainer = allyContainer;
            this.enemyContainer = enemyContainer;
            this.characterViewPrefab = characterViewPrefab;
            this.protectedViewPrefab = protectedViewPrefab;
            this.pendingReinforcementViewPrefab = pendingReinforcementViewPrefab;
            this.cameraView = cameraView;
            this.backgroundView = backgroundView;
            // 이전 씬 컨테이너의 인스턴스는 씬과 함께 파괴됐다 - 참조만 버린다.
            characterPool = new ComponentPool<BattleCharacterUnitView>();
            protectedPool = new ComponentPool<BattleProtectedUnitView>();
            activeReinforcementViews.Clear();
        }

        private void Present(BattleSimulationLoop simulation)
        {
            ClearAll();
            // 유닛을 스폰하기 전에 이번 전투의 전장 크기로 카메라/배경을 먼저 리셋한다(기획 §5) -
            // 리셋 시점을 스폰 직전으로 맞춰야 이전 전투의 확대 상태/타일 배치가 새 유닛에 잠깐이라도
            // 안 맞게 겹쳐 보이지 않는다.
            cameraView?.ConfigureFieldBounds(simulation.FieldRadius);
            backgroundView?.ConfigureField(simulation.SpawnRadius);
            foreach (var unit in simulation.Allies) SpawnCharacter(unit, isAlly: true);
            foreach (var unit in simulation.Enemies) SpawnCharacter(unit, isAlly: false);
            foreach (var unit in simulation.ProtectedUnits) SpawnProtected(unit);
        }

        /// <summary>유닛 뷰를 풀에서 꺼내 바인딩한다. 사망·도주 페이드가 끝나면 스스로 반환된다. 프리팹·컨테이너가 없으면 null.</summary>
        public BattleCharacterUnitView SpawnCharacter(IBattleCombatant unit, bool isAlly)
        {
            var parent = isAlly ? allyContainer : enemyContainer;
            if (characterViewPrefab == null || parent == null) return null;
            var view = characterPool.Rent(characterViewPrefab, parent);
            view.Bind(unit, Release);
            return view;
        }

        private void SpawnProtected(IDamageable unit)
        {
            if (protectedViewPrefab == null || allyContainer == null) return;
            protectedPool.Rent(protectedViewPrefab, allyContainer).Bind(unit);
        }

        /// <summary>뷰 하나를 풀로 돌려보낸다(배틀 테스트 배치 취소 등). 이미 반환된 뷰는 무시한다.</summary>
        public void Release(BattleCharacterUnitView view)
        {
            if (view == null) return;
            view.Unbind();
            characterPool.Return(view);
        }

        /// <summary>한쪽 진영 컨테이너의 유닛 뷰만 반환한다(배틀 테스트 적 구성 적용).</summary>
        public void ClearSide(bool isAlly)
        {
            var parent = isAlly ? allyContainer : enemyContainer;
            for (var i = characterPool.Active.Count - 1; i >= 0; i--)
            {
                var view = characterPool.Active[i];
                if (view != null && view.transform.parent == parent) Release(view);
            }
        }

        // Field 배치 시간(설계 25번 §6.3) - 전투 중 진행 상태가 바뀔 때마다(매 틱) 전체 목록을 받아
        // get-or-create로 재사용한다. 개수가 줄면 남는 인스턴스는 숨기기만 하고 파괴하지 않는다.
        private void HandlePendingReinforcementsChanged(IReadOnlyList<PendingReinforcementInfo> pending)
        {
            if (pendingReinforcementViewPrefab == null || allyContainer == null) return;

            while (activeReinforcementViews.Count < pending.Count)
            {
                activeReinforcementViews.Add(Object.Instantiate(pendingReinforcementViewPrefab, allyContainer));
            }

            for (var i = 0; i < activeReinforcementViews.Count; i++)
            {
                if (i < pending.Count)
                {
                    activeReinforcementViews[i].gameObject.SetActive(true);
                    activeReinforcementViews[i].Bind(pending[i].Position, pending[i].RemainingSeconds);
                }
                else
                {
                    activeReinforcementViews[i].gameObject.SetActive(false);
                }
            }
        }

        /// <summary>
        /// 다음 전투 시작 시 이전 전투의 뷰가 남아있지 않도록 전부 반환한다. 개별 뷰는 사망/도주 페이드 뒤 스스로 반환되지만,
        /// 전투가 도중에 끝나거나(살아남은 유닛) 배틀 테스트가 초기화될 때를 위한 것이다. 대기 증원 유령은 숨기기만 한다(재사용).
        /// </summary>
        public void ClearAll()
        {
            characterPool.ReturnAll(view => view.Unbind());
            protectedPool.ReturnAll(view => view.Unbind());
            foreach (var view in activeReinforcementViews) { if (view != null) view.gameObject.SetActive(false); }
        }
    }
}
