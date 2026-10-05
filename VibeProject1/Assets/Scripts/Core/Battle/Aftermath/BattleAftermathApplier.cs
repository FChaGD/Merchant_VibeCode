using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 전투 결과 반영(설계 79번 §5). 전투 중에는 원장에 기록만 하고(§15-2), 패배 결과까지 정해진 뒤 여기서 한 번에 저장소에 반영한다.
    /// Bootstrap 상주 IManagedComponent인 이유: 조율자(Field UI)는 씬마다 다시 묶이지만 반영 대상 저장소들은 전부 Bootstrap 상주다.
    /// 의존성은 모두 선택적이다 - 배틀 테스트 씬처럼 일부 저장소가 없는 구성에서도 결과 흐름은 진행되어야 하므로, 없는 쪽 반영만 건너뛴다.
    /// 순서 제약: 대열 재배치용 로스터 조회는 보유 마차를 빼기 **전에** 만든다 - 뺀 뒤에는 파괴 마차 Id를 찾지 못해 그 칸을 마차로
    /// 인식하지 못한다.
    /// </summary>
    public class BattleAftermathApplier : MonoBehaviour, IBattleAftermathApplier, IManagedComponent
    {
        private ITradeGoodsCargoSettlement cargoSettlement;
        private ITradeGoodsInventoryRepository inventoryRepository;
        private IOwnedCaravanAssetRemover assetRemover;
        private IFormationRepository formationRepository;
        private ICaravanRosterProvider rosterProvider;
        private IFieldFormationActivityRepository activityRepository;
        private ISessionState sessionState;

        public void RegisterSelf(IDependencyRegistrar registrar)
        {
            registrar.Register<IBattleAftermathApplier>(this);
        }

        public void ResolveDependencies(IDependencyResolver registrar)
        {
            var missing = new List<string>();
            if (!registrar.TryResolve(out cargoSettlement)) missing.Add(nameof(ITradeGoodsCargoSettlement));
            if (!registrar.TryResolve(out inventoryRepository)) missing.Add(nameof(ITradeGoodsInventoryRepository));
            if (!registrar.TryResolve(out assetRemover)) missing.Add(nameof(IOwnedCaravanAssetRemover));
            if (!registrar.TryResolve(out formationRepository)) missing.Add(nameof(IFormationRepository));
            if (!registrar.TryResolve(out rosterProvider)) missing.Add(nameof(ICaravanRosterProvider));
            if (!registrar.TryResolve(out activityRepository)) missing.Add(nameof(IFieldFormationActivityRepository));
            if (!registrar.TryResolve(out sessionState)) missing.Add(nameof(ISessionState));

            // 해석은 등록 시 1회뿐이라 여기서 한 번만 경고한다 - Apply마다 경고하면 전투마다 같은 로그가 쌓인다.
            if (missing.Count > 0)
            {
                Debug.LogWarning($"{nameof(BattleAftermathApplier)}: {string.Join(", ", missing)}가 없어 해당 정산 반영을 건너뛴다(Tools > Game > Build Bootstrap Scene).");
            }
        }

        public BattleAftermathSummary Apply(BattleResult result, DefeatConsequence? consequence)
        {
            if (consequence == DefeatConsequence.Captured || consequence == DefeatConsequence.Death) return default;

            IEnumerable<InventoryItemInstance> currentItems = inventoryRepository != null ? inventoryRepository.Items : Array.Empty<InventoryItemInstance>();
            var plan = BattleAftermathCalculator.Plan(result, consequence, currentItems, () => UnityEngine.Random.value);

            // 1. 화물 제거(환급 없음).
            if (cargoSettlement != null)
            {
                foreach (var instanceId in plan.RemoveInstanceIds) cargoSettlement.RemoveWithoutRefund(instanceId);
            }

            // 2. 대열 정리 - 보유 마차 제거 전 로스터로 조회 함수를 만든다(요약 주석 참고). 에디터 전용 디버그 핀은 넘기지 않는다 - 핀은 정비창
            //    패널의 디버그 도구라 여기서 참조하면 런타임 정산이 디버그 도구에 묶인다. 핀을 쓰면 ③ 진입 판정과 정리 모드의 덩어리 판정이 어긋날 수
            //    있으나 에디터 전용이라 감수한다(2026-10-05 검진 지적 6).
            int relocated = 0, released = 0;
            var disconnected = false;
            if (formationRepository != null && formationRepository.TryLoadCurrent(out var layout) && layout != null)
            {
                var lookup = FormationAreaRules.LookupFrom(rosterProvider);
                if (plan.DestroyedWagonIds.Count > 0)
                {
                    IReadOnlyList<FormationActivity> activities = activityRepository != null ? activityRepository.ActiveActivities : Array.Empty<FormationActivity>();
                    var relocation = FormationWreckRelocator.Relocate(layout, plan.DestroyedWagonIds, lookup, activities);
                    formationRepository.Apply(relocation.Layout);

                    if (activityRepository != null)
                    {
                        foreach (var unitId in relocation.CancelledActivityUnitIds)
                        {
                            if (activityRepository.IsUnitBusy(unitId)) activityRepository.Cancel(unitId);
                        }
                    }

                    relocated = relocation.Relocated.Count;
                    released = relocation.Released.Count;
                    disconnected = relocation.ComponentCount > 1;
                }
                else
                {
                    // 파괴가 없어도 이미 끊어진 대열(이전 궤주 뒤 등)이면 ③ 대열 정리 단계 대상이다.
                    disconnected = !FormationArea.Compute(layout, lookup).WagonsConnected;
                }
            }

            // 3. 보유 목록에서 파괴 마차 제거 - 물류품 섹션도 보유 동기화로 함께 사라진다(설계 79번 §5.2).
            if (assetRemover != null)
            {
                foreach (var wagonId in plan.DestroyedWagonIds) assetRemover.TryRemoveOwned(wagonId);
            }

            // 4. 회수 물품 임시보관(차감 없음, 승리일 때만 계획에 들어 있다).
            if (cargoSettlement != null)
            {
                foreach (var definition in plan.Recover)
                {
                    if (definition != null) cargoSettlement.StageWithoutCharge(definition);
                }
            }

            // 5. 상행 시간 추가(승리·도주에만 계획에 들어 있다).
            if (sessionState != null && plan.ExtraSeconds > 0f) sessionState.ExtendDuration(plan.ExtraSeconds);

            return new BattleAftermathSummary(plan.Broken, plan.StolenLost, plan.Recoverable, plan.DefeatLost, plan.DestroyedWagonIds.Count,
                relocated, released, plan.ExtraSeconds, disconnected);
        }
    }
}
