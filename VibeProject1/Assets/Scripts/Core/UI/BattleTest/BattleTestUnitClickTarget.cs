using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 세팅 단계(전투 시작 전/리셋 후) 미리보기 유닛에만 동적으로 부착된다(BattleTestController.
    /// HandleUnitAdded가 IsRunning일 때는 부착하지 않는다) - BattleCharacterUnit.stats는 생성 후
    /// 불변이라 이미 싸우는 실제 전투 유닛은 수치를 바꿀 방법이 없고, "배치 취소"도 세팅 단계에만
    /// 의미가 있다("배치"를 취소하는 것이지 이미 시작한 전투에서 유닛을 제거하는 기능이 아니다).
    /// 클릭 판정은 이 컴포넌트가 직접 하지 않는다 - BattleTestUnitPickerView가
    /// Physics2D.OverlapPoint로 찾아서 GetComponent로 조회하는 순수 식별자(Id 보관)다(OnMouseDown은
    /// 레거시 Input Manager 기반이라 새 Input System 프로젝트에서 아예 발화하지 않아 쓸 수 없다).
    /// CircleCollider2D는 여기서 동적으로 추가한다 - BattleCharacterUnitView 프리팹(Field 씬과 공유)
    /// 자체에는 필요 없는 컴포넌트라 프리팹에 넣지 않고 배틀 테스트 씬의 인스턴스에만 붙인다.
    /// </summary>
    [RequireComponent(typeof(CircleCollider2D))]
    public class BattleTestUnitClickTarget : MonoBehaviour
    {
        private const float ClickRadius = 0.6f; // BattleCharacterUnitView.BaseBodySize(0.6f)보다 살짝 넉넉하게.

        public bool IsAlly { get; private set; }
        public int EntryId { get; private set; }

        public void Initialize(bool isAlly, int entryId)
        {
            IsAlly = isAlly;
            EntryId = entryId;

            var circleCollider = GetComponent<CircleCollider2D>();
            // Physics2D.OverlapPoint는 기본적으로 트리거도 감지하지만, 프로젝트의 Queries Hit
            // Triggers 설정이 꺼져 있으면 트리거 콜라이더를 건너뛴다 - 그 설정과 무관하게 항상
            // 잡히도록 트리거가 아닌 일반 콜라이더로 둔다(다른 Rigidbody2D와 얽힐 일이 없어 부작용 없음).
            circleCollider.isTrigger = false;
            circleCollider.radius = ClickRadius;
        }
    }
}
