using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 캐릭터/보호대상(마차/시설) 공용 체력 게이지바(사용자 요청) - 둥근 모서리 배경 위에 초록 채움
    /// 바를 올리고, IDamageable.CurrentHp/MaxHp 비율만큼 왼쪽 기준으로 가로 폭을 줄인다.
    /// BattleCharacterUnitView/BattleProtectedUnitView 둘 다 이 컴포넌트를 자식으로 붙여 재사용한다 -
    /// 부모 트랜스폼(유닛 본체)의 스케일을 그대로 물려받으므로, 유닛 크기가 달라져도(체력 기반 크기
    /// 신호) 게이지바도 같은 비율로 함께 커지고 위치도 자연히 본체 위에 맞는다(별도 역스케일 보정
    /// 불필요 - 이 컴포넌트의 Width/Height/오프셋은 "부모 스케일 1일 때" 기준 로컬 값이다).
    /// HpRegenPerSecond(괴수 재생, 기획 08번 §13.1)처럼 이벤트 없이 매 틱 조금씩 바뀌는 값도 있어
    /// OnDamaged 이벤트만으로는 못 따라간다 - Update()에서 매 프레임 갱신한다.
    /// </summary>
    public class BattleHealthGaugeView : MonoBehaviour
    {
        private const float Width = 0.9f;
        private const float Height = 0.16f;
        // 본체 스프라이트(1x1, 중앙 피벗) 위 여백 - 본체 절반(0.5)보다 살짝 더 띄운다.
        private const float VerticalOffset = 0.62f;

        private static readonly Color BackgroundColor = new(0.15f, 0.15f, 0.15f, 0.85f);
        private static readonly Color FillColor = new(0.25f, 0.85f, 0.3f, 1f);
        // 항상 모든 유닛 스프라이트보다 위에 그려지도록 Y기반 정렬 범위보다 훨씬 높은 고정값을 쓴다
        // (BattleCharacterUnitView 투사체의 short.MaxValue와 같은 이유, 다만 투사체보단 아래로 둔다).
        private const int SortingOrder = 30000;

        [SerializeField] private SpriteRenderer backgroundRenderer;
        [SerializeField] private SpriteRenderer fillRenderer;

        private IDamageable unit;
        private float fillFullScaleX;

        private void Awake()
        {
            transform.localPosition = new Vector3(0f, VerticalOffset, 0f);

            backgroundRenderer.sprite = BattleGaugeSprite.Centered;
            backgroundRenderer.color = BackgroundColor;
            backgroundRenderer.sortingOrder = SortingOrder;
            ApplyWorldSize(backgroundRenderer, Width, Height);

            // 좌측 피벗 스프라이트로 만들어봤으나 실제로는 중앙 기준으로 줄어드는 결과가 나왔다(실전투
            // 확인됨) - 대신 배경과 같은 중앙 피벗 스프라이트를 쓰고, 폭(scale.x)과 중심 위치(localPosition.x)를
            // 매 프레임 함께 계산해 왼쪽 끝이 고정된 것처럼 보이게 한다(Refresh 참고) - 스프라이트
            // 피벗에 의존하지 않아 더 확실하다.
            fillRenderer.sprite = BattleGaugeSprite.Centered;
            fillRenderer.color = FillColor;
            fillRenderer.sortingOrder = SortingOrder + 1;
            ApplyWorldSize(fillRenderer, Width, Height);
            fillFullScaleX = fillRenderer.transform.localScale.x;
        }

        public void Bind(IDamageable unit)
        {
            this.unit = unit;
            unit.OnDamaged += HandleDamaged;
            Refresh();
        }

        private void Update() => Refresh();

        private void HandleDamaged(float amount) => Refresh();

        private void Refresh()
        {
            if (unit == null || unit.MaxHp <= 0f) return;

            var ratio = Mathf.Clamp01(unit.CurrentHp / unit.MaxHp);

            var scale = fillRenderer.transform.localScale;
            scale.x = fillFullScaleX * ratio;
            fillRenderer.transform.localScale = scale;

            // 중앙 피벗 스프라이트라 폭만 줄이면 좌우 양쪽에서 좁아진다 - 왼쪽 끝(-Width/2)은 고정하고
            // 줄어든 폭의 중심으로 위치를 함께 당겨야 "왼쪽 정렬, 오른쪽부터 깎임"으로 보인다.
            var position = fillRenderer.transform.localPosition;
            position.x = -Width * 0.5f * (1f - ratio);
            fillRenderer.transform.localPosition = position;
        }

        /// <summary>부모(본체) 사망/도주 페이드아웃과 함께 게이지바도 같이 옅어지도록 호출한다.</summary>
        public void SetAlpha(float alpha)
        {
            var background = backgroundRenderer.color;
            background.a = alpha * BackgroundColor.a;
            backgroundRenderer.color = background;

            var fill = fillRenderer.color;
            fill.a = alpha * FillColor.a;
            fillRenderer.color = fill;
        }

        private static void ApplyWorldSize(SpriteRenderer renderer, float width, float height)
        {
            var nativeSize = renderer.sprite.bounds.size; // 스케일 1일 때 실제 로컬 크기
            var scale = renderer.transform.localScale;
            scale.x = width / nativeSize.x;
            scale.y = height / nativeSize.y;
            renderer.transform.localScale = scale;
        }
    }
}
