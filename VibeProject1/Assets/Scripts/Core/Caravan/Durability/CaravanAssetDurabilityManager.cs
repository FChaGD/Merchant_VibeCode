using System;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 마차·시설 내구도 저장소(Docs/설계/91번 §3.2) - 원장(CaravanAssetDurabilityLedger)을 Bootstrap 수명에 붙인다. 상행을 넘어 유지된다(기획 90번 §3-1).
    /// 읽기·쓰기 계약을 각각 등록한다 - DI는 등록 타입으로만 조회되므로 읽기 소비자가 조용히 null을 받지 않게.
    /// DI를 다시 해결하면 기록을 버리고 현재 보유 목록으로 새로 채운다(보유 목록도 그때 다시 발급된다).
    /// </summary>
    public class CaravanAssetDurabilityManager : MonoBehaviour, ICaravanAssetDurabilityReader, ICaravanAssetDurabilityWriter, IManagedComponent
    {
        private CaravanAssetDurabilityLedger ledger;

        public event Action Changed;

        public void RegisterSelf(IDependencyRegistrar registrar)
        {
            registrar.Register<ICaravanAssetDurabilityReader>(this);
            registrar.Register<ICaravanAssetDurabilityWriter>(this);
        }

        public void ResolveDependencies(IDependencyResolver registrar)
        {
            DisposeLedger();
            if (registrar == null || !registrar.TryResolve(out IOwnedCaravanAssetReader owned))
            {
                Debug.LogWarning($"{nameof(CaravanAssetDurabilityManager)}: {nameof(IOwnedCaravanAssetReader)}가 없어 내구도를 기록하지 않는다(Tools > Game > Build Bootstrap Scene).");
                Changed?.Invoke();
                return;
            }

            ledger = new CaravanAssetDurabilityLedger(owned);
            ledger.Changed += RaiseChanged;
            RaiseChanged();
        }

        public bool TryGet(string instanceId, out float current, out int max)
        {
            current = 0f;
            max = 0;
            return ledger != null && ledger.TryGet(instanceId, out current, out max);
        }

        public bool TrySet(string instanceId, float value) => ledger != null && ledger.TrySet(instanceId, value);

        public bool TryRestore(string instanceId) => ledger != null && ledger.TryRestore(instanceId);

        private void RaiseChanged() => Changed?.Invoke();

        private void DisposeLedger()
        {
            if (ledger == null) return;
            ledger.Changed -= RaiseChanged;
            ledger.Dispose();
            ledger = null;
        }

        private void OnDestroy() => DisposeLedger();
    }
}
