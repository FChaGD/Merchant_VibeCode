#if UNITY_EDITOR
using System;

namespace Game.Core.DebugTools
{
    /// <summary>
    /// 지도 위 도시 드래그의 의미(이동/선 긋기)를 가르는 on/off 상태 보관소. 상태만 들고 있는
    /// 순수 C# 클래스로, TripMapInteractionCoordinator가 소유한다.
    /// </summary>
    internal class TripDebugRoadModeController : ITripDebugRoadModeController
    {
        public bool IsRoadModeActive { get; private set; }

        public event Action<bool> Changed;

        public void Toggle()
        {
            IsRoadModeActive = !IsRoadModeActive;
            Changed?.Invoke(IsRoadModeActive);
        }
    }
}
#endif
