using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 사기(도주) 시스템과 무관한 생존 지향 이동(Docs/기획/12번 §3.3). true를 반환하면 그 destination이
    /// IPositioningStrategy의 결과를 덮어쓴다(Docs/설계/12번 §4) - Kiting과 Stationary처럼 서로 반대
    /// 되는 값이 같은 유닛에 함께 선택될 수 있어, 이 우선순위로 모순을 해소한다.
    /// deltaTime/range는 설계 문서 최초 스케치엔 없었으나, FallBackOnHeavyDamage의 "일시"(시간
    /// 기반 지속) 구현과 Kiting의 사거리 비율 판정에 실제로 필요해 제작 단계에서 추가했다.
    /// </summary>
    public interface ISelfPreservationModifier
    {
        void NotifyDamaged(float amount, float currentHpRatio);
        bool TryGetOverrideMovement(float deltaTime, Vector2 selfPosition, IDamageable target, float range, out Vector2 destination);
    }
}
