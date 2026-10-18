using System;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 상행 중에만 동작하는 조율 요소의 전역 매니저(설계 87 §6.1). 하위 컴포넌트(정지 조율·판정 시계·계기 신호)는 같은 GameObject에 붙고 전역 DI 대상이
    /// 아니다 - 여기서 조회해 소비자가 필요한 계약 단위로 대신 등록한다(GameManager → SessionStateTracker와 같은 구조). 대화 진행자(88, TripDialogueCoordinator)와
    /// 이벤트 진행자(89, EventEncounterCoordinator), 이벤트 중 Field UI 가림(89 §13, FieldUIHideCoordinator)이 붙어 있다.
    /// </summary>
    public class FieldManager : MonoBehaviour, IManagedComponent
    {
        private TripHaltCoordinator halt;
        private TripCheckClock clock;
        private TripMilestoneSignal milestones;
        private TripDialogueCoordinator dialogue;
        private EventEncounterCoordinator events;
        private FieldUIHideCoordinator uiHide;

        public void RegisterSelf(IDependencyRegistrar registrar)
        {
            halt = Require<TripHaltCoordinator>();
            clock = Require<TripCheckClock>();
            milestones = Require<TripMilestoneSignal>();

            registrar.Register<ITripHaltControl>(halt);
            registrar.Register<ITripHaltReader>(halt);
            registrar.Register<ITripCheckSignal>(clock);
            registrar.Register<ITripMilestoneSink>(milestones);
            registrar.Register<ITripMilestoneSource>(milestones);

            dialogue = Require<TripDialogueCoordinator>();
            registrar.Register<ITripDialogueViewBinder>(dialogue);
            registrar.Register<ITripDialogueStatus>(dialogue);
            registrar.Register<ITripDialogueDebug>(dialogue);

            events = Require<EventEncounterCoordinator>();
            registrar.Register<IEventEncounterViewBinder>(events);
            registrar.Register<IEventEncounterDebug>(events);
            registrar.Register<IEventBattleOutcomeSink>(events);

            uiHide = Require<FieldUIHideCoordinator>();
            registrar.Register<IFieldUIHideControl>(uiHide);
            registrar.Register<IFieldUIHideReader>(uiHide);   // 읽기 타입도 따로 등록 - 등록 타입으로만 조회된다
        }

        public void ResolveDependencies(IDependencyResolver registrar)
        {
            var session = registrar.Resolve<ISessionState>();
            registrar.TryResolve<IFieldFormationActivityRepository>(out var activities);
            halt.Bind(session, activities, milestones);
            clock.Bind(session, milestones);
            dialogue.Configure(registrar, clock, milestones, halt);
            uiHide.Bind(milestones);
            events.Configure(registrar, clock, milestones, halt, milestones);
        }

        private T Require<T>() where T : Component
        {
            var component = GetComponent<T>();
            if (component == null)
            {
                throw new InvalidOperationException($"{nameof(FieldManager)}와 같은 GameObject에 {typeof(T).Name}가 없다(Tools > Game > Build Bootstrap Scene).");
            }
            return component;
        }
    }
}
