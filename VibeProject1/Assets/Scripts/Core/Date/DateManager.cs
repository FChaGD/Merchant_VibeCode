using System;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 게임 날짜 전역 매니저(Docs/설계/85번 §3.1). Bootstrap 상주, 조회·진행 계약을 각 타입으로 따로 등록한다(상위·하위 타입 등록으로는
    /// 조회되지 않음). 상행 규칙(초→일)은 모른다 - 누가 며칠을 더할지는 소비자(상행 날짜 시계 TripDayClock 등)가 정한다.
    /// 저장/불러오기가 없어 ResolveDependencies마다 1월 1일로 시작한다. 달력을 새로 만들어도 소비자는 이 컴포넌트의 Changed를
    /// 구독하므로 구독이 끊기지 않는다(GoldHoldingsService와 같은 방식).
    /// </summary>
    public class DateManager : MonoBehaviour, IDateReader, IDateAdvancer, IManagedComponent
    {
        private GameCalendar calendar = new();

        public event Action Changed;

        public GameDate Today => calendar.Today;

        public void RegisterSelf(IDependencyRegistrar registrar)
        {
            registrar.Register<IDateReader>(this);
            registrar.Register<IDateAdvancer>(this);
        }

        public void ResolveDependencies(IDependencyResolver registrar)
        {
            calendar.Changed -= RaiseChanged;
            calendar = new GameCalendar();
            calendar.Changed += RaiseChanged;
        }

        public void AdvanceDays(int days) => calendar.AdvanceDays(days);

        private void RaiseChanged() => Changed?.Invoke();
    }
}
