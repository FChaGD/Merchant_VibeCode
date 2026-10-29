using UnityEngine;
using Random = UnityEngine.Random;

namespace Game.Core
{
    /// <summary>
    /// 효과 명령 라우터를 Bootstrap 수명에 붙인다(설계 109 §5.1) - 라우터 등록과 Yarn 명령 등록(한 번)만 한다. 상행·휴식 공용 서비스(골드·무역품·명단)
    /// 조립은 정적 함수로 둔다: 각 진행자 껍데기가 자기 ResolveDependencies 시점에 자기 resolver로 부르면 매니저 간 초기화 순서에 묶이지 않는다.
    /// </summary>
    public class EventEffectCommandHub : MonoBehaviour, IManagedComponent
    {
        private readonly EventEffectCommandRouter router = new(message => Debug.LogWarning(message));
        private IDialogueCommandRegistry registeredOn;

        public void RegisterSelf(IDependencyRegistrar registrar) => registrar.Register<IEventEffectRouter>(router);

        public void ResolveDependencies(IDependencyResolver registrar)
        {
            if (!registrar.TryResolve(out IDialogueCommandRegistry commands))
            {
                Debug.LogWarning($"{nameof(EventEffectCommandHub)}: 대사 실행기가 없어 효과 명령을 등록하지 않는다(Tools > Game > Build Bootstrap Scene).");
                return;
            }
            if (commands == registeredOn) return;   // 같은 실행기에 같은 이름을 다시 등록하면 Yarn 오류
            router.Register(commands);
            registeredOn = commands;
        }

        /// <summary>
        /// 상행·휴식 공용 효과 서비스(설계 109 §5.1). 맥락 전용 서비스(상행 시계·도로 난이도·휴식 종료)는 호출자가 더한다. 대상 저장소들은 Bootstrap
        /// 상주라 호출마다 묶음을 새로 만들어도 같은 인스턴스를 가리킨다.
        /// </summary>
        public static EventEffectServices CreateBaseServices(IDependencyResolver registrar)
        {
            registrar.TryResolve(out IGoldEventAdjuster gold);
            registrar.TryResolve(out ITradeGoodsInventoryRepository inventory);
            registrar.TryResolve(out ITradeGoodsCargoSettlement settlement);
            registrar.TryResolve(out ICharacterCatalogReader catalog);
            registrar.TryResolve(out IHiredCharacterRoster roster);
            registrar.TryResolve(out IHiredCharacterRemover remover);
            registrar.TryResolve(out IFormationRepository formation);
            registrar.TryResolve(out IFieldFormationActivityRepository activities);

            return new EventEffectServices
            {
                Gold = gold,
                Goods = inventory != null && settlement != null
                    ? new EventCargoGoods(inventory, settlement, id => gold != null && gold.IsGoldItem(id), n => Random.Range(0, n))
                    : null,
                // Hub에는 진행 중 배치 활동이 없어 "바쁨"은 상행에서만 참이 된다(설계 109 §5.6).
                Roster = roster != null
                    ? new EventRosterEffects(catalog, roster, remover, formation, id => activities != null && activities.IsUnitBusy(id), id => activities?.Cancel(id))
                    : null,
                Warn = message => Debug.LogWarning(message),
            };
        }
    }
}
