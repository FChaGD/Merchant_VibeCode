using System;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 최종 도착 신호 중계(Docs/설계/107번 §6.2). Bootstrap 상주 - Field의 상행 생명주기는 Field 로드마다 새로 만들어져 Bootstrap 상주 수신자(퀘스트)가 직접
    /// 구독할 수 없어서 둔다.
    /// </summary>
    public class TripArrivalSignal : MonoBehaviour, ITripArrivalSink, ITripArrivalSource, IManagedComponent
    {
        public event Action<int> FinalArrived;

        public void RegisterSelf(IDependencyRegistrar registrar)
        {
            registrar.Register<ITripArrivalSink>(this);
            registrar.Register<ITripArrivalSource>(this);
        }

        public void ResolveDependencies(IDependencyResolver registrar) { }

        public void NotifyFinalArrival(int cityId) => FinalArrived?.Invoke(cityId);
    }
}
