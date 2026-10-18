namespace Game.Core
{
    /// <summary>
    /// 상행 시계 조정(설계 89 §4.3·§5.1) - 이벤트 단축과 도로 난이도 재계산만 쓴다. ISessionState에 넣지 않은 이유: 진행 표시·날짜 정산처럼 읽기만
    /// 하는 소비자(테스트 가짜 포함)에게 조정 권한을 넓히지 않기 위해서다(ISP). 지연(ExtendDuration)은 기존 계약에 이미 있다.
    /// </summary>
    public interface ITripClockAdjuster
    {
        /// <summary>현재 구간 기본 시간에 더해진 추가 시간(지연 +, 단축 −). 새 구간 시작 시 0.</summary>
        float AddedSeconds { get; }
        /// <summary>단축. 지나온 시간 아래로 줄지 않는다. 실제 줄어든 초. 도착한 구간이면 0.</summary>
        float ShortenDuration(float seconds);
        /// <summary>기본 시간 교체(난이도 재계산). 추가 시간은 유지하고 진행 비율을 맞춘다.</summary>
        void RebaseDuration(float baseSeconds);
    }
}
