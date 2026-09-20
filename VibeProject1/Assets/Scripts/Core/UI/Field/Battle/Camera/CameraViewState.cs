namespace Game.Core
{
    /// <summary>
    /// 카메라 정책이 읽기만 하는 상태 묶음. 상태의 소유자는 OrthographicCameraZoomController이고,
    /// 정책은 자체 상태를 갖지 않도록 매 호출마다 이 값을 받는다.
    /// </summary>
    internal readonly struct CameraViewState
    {
        public readonly float BaselineSize; // 전장 전체가 여백 없이 보이는 기준 orthographicSize
        public readonly float CurrentSize;
        public readonly float FieldRadius;
        public readonly float Aspect;

        public CameraViewState(float baselineSize, float currentSize, float fieldRadius, float aspect)
        {
            BaselineSize = baselineSize;
            CurrentSize = currentSize;
            FieldRadius = fieldRadius;
            Aspect = aspect;
        }
    }

    /// <summary>orthographicSize는 작을수록 확대이므로 ZoomedIn이 작은 값, ZoomedOut이 큰 값이다.</summary>
    internal readonly struct ZoomSizeRange
    {
        public readonly float ZoomedIn;
        public readonly float ZoomedOut;

        public ZoomSizeRange(float zoomedIn, float zoomedOut)
        {
            ZoomedIn = zoomedIn;
            ZoomedOut = zoomedOut;
        }
    }
}
