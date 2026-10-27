using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 숙소 휴식의 실제 상태 변경(Docs/설계/105번). 하루가 지날 때마다 개인 골드에서 1일 비용을 빼고 날짜를 하루 넘긴다 -
    /// 결제는 기본 소지 골드만이라(기획 94 4-3) 골드 상자를 꺼내는 IGoldSpender가 아니라 지갑을 직접 쓴다.
    /// 비용 부족은 숙소 화면이 확정 단계에서 막고 진행 중엔 재화 HUD 입력도 막혀 정상 경로에 없다 - 그래도 생기면 하루는 이미 지났으므로
    /// 경고만 하고 날짜는 넘긴다. 회복은 보류(대상 없음, 기획 94 §6).
    /// </summary>
    public class InnRestService : MonoBehaviour, IInnRestService, IManagedComponent
    {
        [SerializeField] private int costPerDay = 10;

        private readonly InnRestSession session = new();
        private IPlayerCurrencyWallet wallet;
        private IDateAdvancer date;

        public int CostPerDay => costPerDay;

        public void RegisterSelf(IDependencyRegistrar registrar) => registrar.Register<IInnRestService>(this);

        public void ResolveDependencies(IDependencyResolver registrar)
        {
            IPlayerCurrencyWallet resolvedWallet = null;
            IDateAdvancer resolvedDate = null;
            if (registrar != null)
            {
                registrar.TryResolve(out resolvedWallet);
                registrar.TryResolve(out resolvedDate);
                if (resolvedWallet == null) Debug.LogWarning($"{nameof(InnRestService)}: {nameof(IPlayerCurrencyWallet)}가 없어 휴식 비용을 청구하지 않는다(Tools > Game > Build Bootstrap Scene).");
                if (resolvedDate == null) Debug.LogWarning($"{nameof(InnRestService)}: {nameof(IDateAdvancer)}가 없어 휴식으로 날짜가 지나지 않는다(Tools > Game > Build Bootstrap Scene).");
            }
            Initialize(resolvedWallet, resolvedDate);
        }

        /// <summary>DI 없이 의존성을 넣는다(테스트용 겸 ResolveDependencies 공용 경로).</summary>
        public void Initialize(IPlayerCurrencyWallet currencyWallet, IDateAdvancer dateAdvancer)
        {
            wallet = currencyWallet;
            date = dateAdvancer;
            session.End();
        }

        public void BeginRest(int plannedDays)
        {
            if (session.IsActive) Debug.LogWarning($"{nameof(InnRestService)}: 이전 휴식이 끝나지 않은 채 새 휴식을 시작한다(종료 누락).");
            if (!session.Begin(plannedDays)) Debug.LogWarning($"{nameof(InnRestService)}: 휴식 일수 {plannedDays}는 1 미만이라 시작하지 않는다.");
        }

        public void AdvanceDay()
        {
            if (!session.TryAdvance())
            {
                Debug.LogWarning($"{nameof(InnRestService)}: 진행 중인 휴식이 없거나 예정 일수를 넘어 하루 경과를 무시한다.");
                return;
            }

            if (wallet != null && !wallet.TrySpend(costPerDay))
            {
                Debug.LogWarning($"{nameof(InnRestService)}: 개인 골드가 1일 비용 {costPerDay}보다 적어 청구하지 못했다(날짜는 진행).");
            }
            date?.AdvanceDays(1);
        }

        public void EndRest(int restedDays)
        {
            if (session.IsActive && restedDays != session.ElapsedDays)
            {
                Debug.LogWarning($"{nameof(InnRestService)}: 종료 일수 {restedDays}가 실제 경과 {session.ElapsedDays}일과 다르다(반영은 경과 시점 기준).");
            }
            session.End();
        }
    }
}
