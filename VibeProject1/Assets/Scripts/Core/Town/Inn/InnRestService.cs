using System;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 숙소 휴식의 실제 상태 변경(Docs/설계/105번, 109번 §4.3). 하루 처리마다 1일 비용을 개인 골드 → 골드 상자 순으로 결제하고(IGoldSpender), 성공한 날만
    /// 날짜를 넘기고 회복한다 - 결제 부족일은 미성립이라(기획 108 §3.3) 상태를 바꾸지 않고 흐름 객체가 휴식을 끝낸다.
    /// 지출 계약이 없으면 경고 후 결제 없이 성립으로 본다(지갑 없음과 같은 성격). 회복 구현체는 지략·행정력 구현 때 생긴다(없으면 회복 없음).
    /// </summary>
    public class InnRestService : MonoBehaviour, IInnRestService, IManagedComponent
    {
        [SerializeField] private int costPerDay = 10;

        private readonly InnRestSession session = new();
        private IGoldSpender spender;
        private IDateAdvancer date;
        private IRestDayRecovery recovery;

        public int CostPerDay => costPerDay;

        public void RegisterSelf(IDependencyRegistrar registrar) => registrar.Register<IInnRestService>(this);

        public void ResolveDependencies(IDependencyResolver registrar)
        {
            IGoldSpender resolvedSpender = null;
            IDateAdvancer resolvedDate = null;
            IRestDayRecovery resolvedRecovery = null;
            if (registrar != null)
            {
                registrar.TryResolve(out resolvedSpender);
                registrar.TryResolve(out resolvedDate);
                registrar.TryResolve(out resolvedRecovery);
                if (resolvedSpender == null) Debug.LogWarning($"{nameof(InnRestService)}: {nameof(IGoldSpender)}가 없어 휴식 비용을 청구하지 않는다(Tools > Game > Build Bootstrap Scene).");
                if (resolvedDate == null) Debug.LogWarning($"{nameof(InnRestService)}: {nameof(IDateAdvancer)}가 없어 휴식으로 날짜가 지나지 않는다(Tools > Game > Build Bootstrap Scene).");
            }
            Initialize(resolvedSpender, resolvedDate, resolvedRecovery);
        }

        /// <summary>DI 없이 의존성을 넣는다(테스트용 겸 ResolveDependencies 공용 경로).</summary>
        public void Initialize(IGoldSpender goldSpender, IDateAdvancer dateAdvancer, IRestDayRecovery dayRecovery)
        {
            spender = goldSpender;
            date = dateAdvancer;
            recovery = dayRecovery;
            session.End();
        }

        public void BeginRest(int plannedDays)
        {
            if (session.IsActive) Debug.LogWarning($"{nameof(InnRestService)}: 이전 휴식이 끝나지 않은 채 새 휴식을 시작한다(종료 누락).");
            if (!session.Begin(plannedDays)) Debug.LogWarning($"{nameof(InnRestService)}: 휴식 일수 {plannedDays}는 1 미만이라 시작하지 않는다.");
        }

        public bool CanSettleDay => session.CanAdvance;

        public RestDaySettlement SettleDay()
        {
            // 흐름 객체가 CanSettleDay를 확인하고 부른다 - 여기 오면 호출 순서 오류다. 결과 값으로 돌려주면 "골드 부족" 종료와 구분되지 않는다.
            if (!session.CanAdvance) throw new InvalidOperationException($"{nameof(InnRestService)}: 진행 중인 휴식이 없거나 예정 일수를 넘어 하루 처리를 할 수 없다.");
            if (spender != null && !spender.TrySpend(costPerDay)) return RestDaySettlement.InsufficientGold;

            session.TryAdvance();
            date?.AdvanceDays(1);
            recovery?.RecoverOneDay();
            return RestDaySettlement.Paid;
        }

        public void EndRest(int restedDays)
        {
            if (session.IsActive && restedDays != session.ElapsedDays)
            {
                Debug.LogWarning($"{nameof(InnRestService)}: 종료 일수 {restedDays}가 실제 성립 {session.ElapsedDays}일과 다르다(반영은 성립 시점 기준).");
            }
            session.End();
        }
    }
}
