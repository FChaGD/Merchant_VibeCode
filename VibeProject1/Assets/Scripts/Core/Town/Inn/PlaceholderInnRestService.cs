using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 숙소 휴식 서비스 Placeholder(Docs/설계/103번 §4.1) - 1일 비용만 인스펙터 값으로 주고 시작·경과·종료는 아무것도 하지 않는다.
    /// 실제 시스템 설계 후 대체/제거 대상(날짜 진행·비용 청구·회복은 system 브랜치).
    /// </summary>
    public class PlaceholderInnRestService : MonoBehaviour, IInnRestService, IManagedComponent
    {
        [SerializeField] private int costPerDay = 10;

        public int CostPerDay => costPerDay;

        public void RegisterSelf(IDependencyRegistrar registrar) => registrar.Register<IInnRestService>(this);

        public void ResolveDependencies(IDependencyResolver registrar)
        {
        }

        public void BeginRest(int plannedDays)
        {
        }

        public void AdvanceDay()
        {
        }

        public void EndRest(int restedDays)
        {
        }
    }
}
