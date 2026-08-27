using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 활동 반경(IActivityRadiusZone)을 벗어난 상태에서 추적을 그만두고 복귀·재타겟해야 하는지
    /// 판정한다(Docs/기획/12번 §2.3). ShouldDisengage는 "현재 살아있는 타겟을 추적 중"일 때만
    /// 매 틱 호출된다 - 타겟이 죽었을 때의 재타겟은 프리셋과 무관하게 기존 널 타겟 분기가 이미
    /// 처리하므로 이 인터페이스가 알 필요 없다(Docs/설계/12번 §3.4).
    /// </summary>
    public interface IPursuitPolicy
    {
        bool ShouldDisengage(float deltaTime, bool isOutsideRadius, bool justLandedHit);
        // HoldPosition만 실제로 zone 경계 안으로 당긴다 - 나머지 4개는 desiredDestination을 그대로
        // 반환한다(반경 밖 이동 자체는 허용).
        Vector2 ClampDestination(Vector2 desiredDestination, IActivityRadiusZone zone);
    }
}
