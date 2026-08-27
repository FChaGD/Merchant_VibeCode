using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// "여기가 내 활동 반경 안인가"만 판정한다. 판정 방법(고정/표준/광역)을 몰라도 되는 소비자
    /// (IEnemyRecognitionTracker/IPursuitPolicy)가 결과만 참조하도록 분리했다 - 반경 계산 방식이
    /// 늘어도 그 두 인터페이스는 무변경(ISP, Docs/설계/12번 §7).
    /// </summary>
    public interface IActivityRadiusZone
    {
        bool Contains(Vector2 worldPosition);
        // HoldPosition 프리셋(IPursuitPolicy.ClampDestination)만 쓴다 - 반경 밖이면 경계 위의
        // 가장 가까운 지점으로 당겨준다. 나머지 4개 프리셋은 반경을 벗어나는 이동 자체는 허용하므로
        // 이 메서드를 호출하지 않는다. Contains만으로는(참/거짓) 어디로 당겨야 할지 알 수 없어
        // 제작 단계에서 추가했다.
        Vector2 ClampToZone(Vector2 desiredPosition);
    }
}
