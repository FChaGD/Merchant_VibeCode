using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 전투 결과 반영(설계 79번 §5). 전투 중에는 원장에 기록만 하고(§15-2), 패배 결과까지 정해진 뒤 여기서 한 번에 저장소에 반영한다.
    /// Bootstrap 상주 IManagedComponent인 이유: 조율자(Field UI)는 씬마다 다시 묶이지만 반영 대상 저장소들은 전부 Bootstrap 상주다.
    /// 의존성은 모두 선택적이다 - 배틀 테스트 씬처럼 일부 저장소가 없는 구성에서도 결과 흐름은 진행되어야 하므로, 없는 쪽 반영만 건너뛴다.
    /// 대열 정리·보유 제거 순서 제약은 공용 파괴 처리(CaravanAssetDestroyer)가 갖는다(설계 91번 §4.4).
    /// </summary>
    public class BattleAftermathApplier : MonoBehaviour, IBattleAftermathApplier, IManagedComponent
    {
        private ITradeGoodsCargoSettlement cargoSettlement;
        private ITradeGoodsInventoryRepository inventoryRepository;
        private ISessionState sessionState;
        private ICaravanAssetDurabilityWriter durabilityWriter;
        private CaravanAssetDestroyer destroyer;

        public void RegisterSelf(IDependencyRegistrar registrar)
        {
            registrar.Register<IBattleAftermathApplier>(this);
        }

        public void ResolveDependencies(IDependencyResolver registrar)
        {
            var missing = new List<string>();
            if (!registrar.TryResolve(out cargoSettlement)) missing.Add(nameof(ITradeGoodsCargoSettlement));
            if (!registrar.TryResolve(out inventoryRepository)) missing.Add(nameof(ITradeGoodsInventoryRepository));
            if (!registrar.TryResolve(out IOwnedCaravanAssetRemover assetRemover)) missing.Add(nameof(IOwnedCaravanAssetRemover));
            if (!registrar.TryResolve(out IFormationRepository formationRepository)) missing.Add(nameof(IFormationRepository));
            if (!registrar.TryResolve(out ICaravanRosterProvider rosterProvider)) missing.Add(nameof(ICaravanRosterProvider));
            if (!registrar.TryResolve(out IFieldFormationActivityRepository activityRepository)) missing.Add(nameof(IFieldFormationActivityRepository));
            if (!registrar.TryResolve(out sessionState)) missing.Add(nameof(ISessionState));
            if (!registrar.TryResolve(out durabilityWriter)) missing.Add(nameof(ICaravanAssetDurabilityWriter));
            destroyer = new CaravanAssetDestroyer(formationRepository, rosterProvider, activityRepository, assetRemover);

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

            // 2. 살아남은 마차·시설 내구도 저장(설계 91번 §4.2) - 파괴된 개체는 3에서 보유 목록과 함께 기록이 지워진다.
            if (durabilityWriter != null)
            {
                foreach (var unit in result.ProtectedUnits)
                {
                    if (!unit.Destroyed && !string.IsNullOrEmpty(unit.UnitId)) durabilityWriter.TrySet(unit.UnitId, unit.RemainingDurability);
                }
            }

            // 3. 파괴 마차·시설 → 대열 정리 → 보유 제거(공용 파괴 처리, 설계 91번 §4.4). 물류품 섹션도 보유 동기화로 함께 사라진다(설계 79번 §5.2).
            //    에디터 전용 디버그 핀은 넘기지 않는다 - 핀은 정비창 패널의 디버그 도구라 여기서 참조하면 런타임 정산이 디버그 도구에 묶인다.
            //    핀을 쓰면 ③ 진입 판정과 정리 모드의 덩어리 판정이 어긋날 수 있으나 에디터 전용이라 감수한다(2026-10-05 검진 지적 6).
            var destroyedIds = new List<string>(plan.DestroyedWagonIds);
            destroyedIds.AddRange(plan.DestroyedFacilityIds);
            var destruction = destroyer != null ? destroyer.Destroy(destroyedIds) : default;

            // 4. 회수 물품 임시보관(차감 없음, 승리일 때만 계획에 들어 있다).
            if (cargoSettlement != null)
            {
                foreach (var definition in plan.Recover)
                {
                    if (definition != null) cargoSettlement.StageWithoutCharge(definition);
                }
            }

            // 5. 상행 시간 추가(승리·도주에만 계획에 들어 있다, 파괴 마차만 - 기획 90번 §3-7).
            if (sessionState != null && plan.ExtraSeconds > 0f) sessionState.ExtendDuration(plan.ExtraSeconds);

            return new BattleAftermathSummary(plan.Broken, plan.StolenLost, plan.Recoverable, plan.DefeatLost, plan.DestroyedWagonIds.Count,
                destruction.Relocated, destruction.Released, plan.ExtraSeconds, destruction.Disconnected, plan.DestroyedFacilityIds.Count);
        }
    }
}
