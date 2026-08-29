using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 포위망(Ring) 하나의 상태 - (적 군집, 사거리 값) 쌍 하나당 하나 생긴다(Docs/설계/12번 §13.3′,
    /// 1차 설계의 개별 각도 배정 방식은 "타겟이 다르면 그룹도 갈라져 군집을 못 둘러싼다"는 구조적
    /// 한계로 폐기됨). FrontlineFormationLine과 같은 자리지만 슬롯 오프셋 개념이 없다 - 명시적 각도
    /// 배정을 하지 않고 접근 방향이 곧 최종 각도가 되므로, "누가 어느 슬롯인지"를 추적할 필요가 없다.
    /// RangedSurroundCoordinator만 이 값을 갱신한다.
    /// </summary>
    public class SurroundRing
    {
        // 링 식별의 일부(소속 군집과 조합) - 생성 후 불변. 유닛마다 사거리가 달라도 각자 맞는 링에
        // 합류하게 하는 축(Docs/설계/12번 §13.3′ "사거리 종류만큼 포위망").
        public float DealerRange { get; }
        public Vector2 ClusterCenter { get; set; }
        public float ClusterBoundingRadius { get; set; }
        // 실제 반지름 - 초기값 ClusterBoundingRadius+DealerRange, 합류 멤버 중 공격 불가 상태가
        // 있으면 하한(ClusterBoundingRadius)까지 서서히 축소, 없으면 다시 서서히 복원(§13.3′-4).
        public float CurrentRadius { get; set; }
        // 이 링에 배정된 유닛 전체(접근 중 + 합류 완료 모두 포함) - 코디네이터가 사망/도주/타겟
        // 상실 시 여기서 제거한다.
        public List<IBattleCombatant> AssignedUnits { get; } = new();
        // AssignedUnits의 부분집합 - 링에 물리적으로 도달(콜리전 대체 판정)해 더 이상 접근 이동
        // (반지름 접근+아군 반발+빈 구간 끌어당김)을 하지 않는 유닛들. 한번 들어오면 CurrentRadius가
        // 바뀌어도 자동으로 빠지지 않는다(이벤트성 고정, §12.6과 같은 철학) - 빠지는 건 코디네이터의
        // 사망/도주/타겟 상실 제거뿐이다.
        public HashSet<IBattleCombatant> Joined { get; } = new();
        // 링이 상대하는 적 전체(합집합, 살아있는 개체만) - 신규 합류 후보의 CurrentTarget이 "이 링
        // 소속인지" 판정하는 기준(§13.3′ 군집 선택 기준 - 자기 CurrentTarget이 속한 군집).
        public List<IDamageable> RecognizedUnion { get; } = new();

        public SurroundRing(float dealerRange)
        {
            DealerRange = dealerRange;
        }

        /// <summary>
        /// selfPosition에서 ClusterCenter를 잇는 반지름 선을 CurrentRadius까지 연장한 점 - 명시적
        /// 각도 배정이 없는 이 설계에서, "접근 방향이 곧 최종 각도"가 되는 핵심 계산(§13.3′).
        /// 코디네이터(활동 반경 판정)와 전략(접근 이동/반지름 축소 추적) 양쪽이 같은 계산을 쓴다.
        /// </summary>
        public Vector2 ComputeRadialPoint(Vector2 selfPosition)
        {
            var toSelf = selfPosition - ClusterCenter;
            var direction = toSelf.sqrMagnitude > 0.0001f ? toSelf.normalized : Vector2.right;
            return ClusterCenter + direction * CurrentRadius;
        }
    }
}
