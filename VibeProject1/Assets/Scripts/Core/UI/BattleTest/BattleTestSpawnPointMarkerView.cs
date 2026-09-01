using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 고정 스폰 포인트(12곳) 하나를 나타내는 클릭 가능한 월드 마커. BattleTestUnitClickTarget과
    /// 달리 전투 시작/리셋과 무관하게 항상 존재한다(동적 Instantiate/Destroy 없음, 인스톨러가 12개를
    /// 미리 만들어둠). 미배정(회색 원)/예약됨(대표 타입 도형+빨간 틴트)을 색+도형으로 함께 구분한다 -
    /// 한 지점에 여러 타입이 동시에 예약될 수 있어(BattleTestSpawnPointReservations.Composition) 이
    /// 마커는 정확한 개수까지는 표시하지 않고 "예약이 있다/없다"와 대표 타입 하나만 보여준다 -
    /// 정확한 타입별 개수는 클릭해서 여는 패널에서 확인한다.
    /// </summary>
    [RequireComponent(typeof(CircleCollider2D))]
    public class BattleTestSpawnPointMarkerView : MonoBehaviour
    {
        private static readonly Color UnassignedColor = new(0.6f, 0.6f, 0.6f, 0.65f);
        private static readonly Color ReservedColor = new(0.95f, 0.25f, 0.25f, 1f);
        private const float ColliderRadius = 0.6f;

        [SerializeField] private SpriteRenderer bodyRenderer;

        public int SpawnPointIndex { get; private set; }

        private void Awake()
        {
            var collider = GetComponent<CircleCollider2D>();
            collider.isTrigger = false;
            collider.radius = ColliderRadius;
        }

        public void Initialize(int spawnPointIndex) => SpawnPointIndex = spawnPointIndex;

        public void SetReservation(BattleTestSpawnPointReservations.Composition composition)
        {
            var primaryType = ResolvePrimaryType(composition);
            bodyRenderer.sprite = primaryType.HasValue ? BattlePlaceholderSprite.ForEnemyType(primaryType.Value) : BattlePlaceholderSprite.WhiteCircle;
            bodyRenderer.color = primaryType.HasValue ? ReservedColor : UnassignedColor;
        }

        private static EnemyType? ResolvePrimaryType(BattleTestSpawnPointReservations.Composition composition)
        {
            if (composition.Marauder > 0) return EnemyType.Marauder;
            if (composition.Monster > 0) return EnemyType.Monster;
            if (composition.Adversary > 0) return EnemyType.Adversary;
            return null;
        }
    }
}
