using System;
using UnityEngine;

namespace Game.Core
{
    /// <summary>FieldManager 하위 컴포넌트 - 계기 신호 중계. 전역 DI 대상이 아니다(FieldManager가 발신·수신 계약으로 대신 등록).</summary>
    public class TripMilestoneSignal : MonoBehaviour, ITripMilestoneSink, ITripMilestoneSource
    {
        public event Action<TripMilestone> Raised;

        public void Raise(TripMilestone milestone) => Raised?.Invoke(milestone);
    }
}
