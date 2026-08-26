using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Core
{
    /// <summary>
    /// 기획 08번 문서 §11 Placeholder 사양 - 단색 사각형으로 BattleCharacterUnit 1기를 표현한다.
    /// Field 전투 뷰가 UGUI(Canvas) 기반이라 SpriteRenderer가 아니라 RectTransform/Image로 그린다.
    /// 이동은 매 프레임 Position을 그대로 따라가고, 공격/피격/사망/도주는 유닛이 발행하는 이벤트에
    /// 반응해서만 처리한다(폴링 없음).
    /// </summary>
    public class BattleCharacterUnitView : MonoBehaviour
    {
        [SerializeField] private Image bodyImage;

        private const float HitFlashSeconds = 0.1f;
        // 사망/도주 페이드아웃 공통 소요시간 - 보간(Lerp)으로 2초에 걸쳐 알파를 낮춘다.
        private const float FadeOutSeconds = 2f;
        private const float DeathGrowScale = 1.1f;
        private const float LungeDistance = 20f;
        private const float LungeSeconds = 0.1f;
        private const float ProjectileSeconds = 0.15f;
        // 이 거리 이상이면 원거리(투사체)로 간주한다 - 기획 §12 사거리(전사 1.5/궁수 6.0/방패병 1.2)를
        // 감안한 연출 전용 임계치일 뿐, 실제 사거리 값과는 별개다.
        private const float RangedVisualThreshold = 2.5f;

        private static readonly Color AllyColor = new(0.3f, 0.5f, 1f, 1f);
        private static readonly Color EnemyColor = new(1f, 0.35f, 0.35f, 1f);
        private static readonly Color FlashColor = Color.white;
        // 사망 시점에 진영 색(파랑/빨강)을 덮어쓰는 색 - 도주와 시각적으로 구분하기 위한 것(§11).
        private static readonly Color DeathTintColor = new(0.75f, 0.05f, 0.05f, 1f);

        private RectTransform rectTransform;
        private IBattleCombatant unit;
        private Color baseColor;

        private void Awake()
        {
            rectTransform = (RectTransform)transform;
        }

        public void Bind(IBattleCombatant unit)
        {
            this.unit = unit;
            baseColor = unit.IsAlly ? AllyColor : EnemyColor;
            bodyImage.color = baseColor;
            rectTransform.anchoredPosition = unit.Position * BattleFieldGeometry.CoordinateToPixelScale;
            // MaxHp를 크기 신호로 재사용 - 방패병(150) > 전사(100) > 궁수(70) 순서가 자연히 만들어진다(기획 §12).
            rectTransform.localScale = Vector3.one * Mathf.Clamp(unit.MaxHp / 100f, 0.7f, 1.5f);

            unit.OnDamaged += HandleDamaged;
            unit.OnAttacked += HandleAttacked;
            unit.OnDied += HandleDied;
            unit.OnFled += HandleFled;
        }

        private void Update()
        {
            if (unit == null || !unit.IsAlive) return;
            rectTransform.anchoredPosition = unit.Position * BattleFieldGeometry.CoordinateToPixelScale;
        }

        private void HandleDamaged(float amount) => StartCoroutine(FlashWhite());

        private void HandleAttacked(IDamageable target)
        {
            var distance = Vector2.Distance(unit.Position, target.Position);
            if (distance < RangedVisualThreshold)
            {
                StartCoroutine(Lunge((target.Position - unit.Position).normalized));
            }
            else
            {
                StartCoroutine(FireProjectile(target.Position * BattleFieldGeometry.CoordinateToPixelScale));
            }
        }

        // 사망 처리 시점(HP 0 도달) 자체는 BattleCharacterUnit.TakeDamage에서 이 이벤트보다 먼저
        // 동기적으로 끝나 있다 - 여기서는 그 결과에 반응하는 순수 연출만 한다. 즉시 진영 색을 사망
        // 색으로 덮고 1.1배로 키운 뒤(§11), 페이드아웃하며 제거해 도주(HandleFled)와 구분한다.
        private void HandleDied()
        {
            bodyImage.color = DeathTintColor;
            rectTransform.localScale *= DeathGrowScale;
            StartCoroutine(FadeOutAndDestroy());
        }

        // 전장을 완전히 벗어난 시점(OnFled)에도 도주 방향으로 계속 이동하며 페이드아웃한다(§11) -
        // 타겟팅/생사 판정은 이미 이 시점 이전에 끝나 있으므로(IsAlive), 여기서부터는 시뮬레이션
        // Tick 없이 뷰가 마지막 도주 속도(FleeVelocity)를 그대로 이어받아 순수 연출로만 이동시킨다.
        private void HandleFled() => StartCoroutine(FleeFadeOutAndDestroy(unit.FleeVelocity));

        private IEnumerator FlashWhite()
        {
            bodyImage.color = FlashColor;
            yield return new WaitForSeconds(HitFlashSeconds);
            // 사망 연출(빨간 틴트)이 이미 시작됐다면 원래 색으로 되돌리지 않는다 - 킬링 블로우처럼
            // OnDamaged와 OnDied가 같은 프레임에 함께 발생하면, 이 지연 복원이 사망 틴트를 덮어써버린다.
            if (bodyImage != null && unit.IsAlive) bodyImage.color = baseColor;
        }

        private IEnumerator Lunge(Vector2 direction)
        {
            var origin = rectTransform.anchoredPosition;
            var peak = origin + direction * LungeDistance;
            var half = LungeSeconds * 0.5f;

            var t = 0f;
            while (t < half)
            {
                t += Time.deltaTime;
                rectTransform.anchoredPosition = Vector2.Lerp(origin, peak, t / half);
                yield return null;
            }
            t = 0f;
            while (t < half)
            {
                t += Time.deltaTime;
                rectTransform.anchoredPosition = Vector2.Lerp(peak, origin, t / half);
                yield return null;
            }
        }

        private IEnumerator FireProjectile(Vector2 targetAnchoredPosition)
        {
            var projectileGo = new GameObject("Projectile", typeof(RectTransform), typeof(Image));
            projectileGo.transform.SetParent(rectTransform.parent, false);
            var projectileRect = (RectTransform)projectileGo.transform;
            projectileRect.sizeDelta = new Vector2(6f, 6f);
            projectileGo.GetComponent<Image>().color = baseColor;

            var origin = rectTransform.anchoredPosition;
            var elapsed = 0f;
            while (elapsed < ProjectileSeconds)
            {
                elapsed += Time.deltaTime;
                projectileRect.anchoredPosition = Vector2.Lerp(origin, targetAnchoredPosition, elapsed / ProjectileSeconds);
                yield return null;
            }
            Destroy(projectileGo);
        }

        private IEnumerator FadeOutAndDestroy()
        {
            var elapsed = 0f;
            var startColor = bodyImage.color;
            while (elapsed < FadeOutSeconds)
            {
                elapsed += Time.deltaTime;
                var alpha = Mathf.Lerp(startColor.a, 0f, elapsed / FadeOutSeconds);
                bodyImage.color = new Color(startColor.r, startColor.g, startColor.b, alpha);
                yield return null;
            }
            Destroy(gameObject);
        }

        // FadeOutAndDestroy와 거의 같지만, 도주는 정지 상태로 페이드아웃하지 않는다 - OnFled 이후에도
        // 마지막 도주 속도로 계속 이동한다(§11 "도주를 멈추지 않고").
        private IEnumerator FleeFadeOutAndDestroy(Vector2 velocity)
        {
            var elapsed = 0f;
            var startColor = bodyImage.color;
            while (elapsed < FadeOutSeconds)
            {
                elapsed += Time.deltaTime;
                rectTransform.anchoredPosition += velocity * BattleFieldGeometry.CoordinateToPixelScale * Time.deltaTime;
                var alpha = Mathf.Lerp(startColor.a, 0f, elapsed / FadeOutSeconds);
                bodyImage.color = new Color(startColor.r, startColor.g, startColor.b, alpha);
                yield return null;
            }
            Destroy(gameObject);
        }
    }
}
