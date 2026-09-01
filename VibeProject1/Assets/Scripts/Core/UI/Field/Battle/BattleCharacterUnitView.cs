using System.Collections;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 기획 08번 문서 §11 Placeholder 사양 - 단색 사각형으로 BattleCharacterUnit 1기를 표현한다.
    /// 월드 오브젝트(SpriteRenderer) 기반이다(Docs/설계/13-2026-08-29-전투뷰_월드오브젝트_전환_아키텍처.md -
    /// UGUI Screen Space Overlay와 시뮬레이션 좌표계의 스케일 차이가 커서 Scene 뷰 디버그 도구 등이
    /// 화면 구석에 조그맣게 그려지는 문제로 전환됨). 이동은 매 프레임 Position을 그대로 따라가고,
    /// 공격/피격/사망/도주는 유닛이 발행하는 이벤트에 반응해서만 처리한다(폴링 없음).
    /// </summary>
    public class BattleCharacterUnitView : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer bodyRenderer;

        private const float HitFlashSeconds = 0.1f;
        // 사망/도주 페이드아웃 공통 소요시간 - 보간(Lerp)으로 2초에 걸쳐 알파를 낮춘다.
        private const float FadeOutSeconds = 2f;
        private const float DeathGrowScale = 1.1f;
        // 1차 UGUI 버전은 20px(CoordinateToPixelScale=40 기준)였다 - 0.5 월드유닛으로 환산.
        private const float LungeDistance = 0.5f;
        private const float LungeSeconds = 0.1f;
        private const float ProjectileSeconds = 0.15f;
        // 이 거리 이상이면 원거리(투사체)로 간주한다 - 기획 §12 사거리를 감안한 연출 전용 임계치라
        // 애초에 픽셀 변환과 무관한 시뮬레이션 좌표 값이었다(전환 영향 없음).
        private const float RangedVisualThreshold = 2.5f;
        // 기존 UGUI 버전의 24px(=0.6 월드유닛, CoordinateToPixelScale=40 기준) 체감 크기를 그대로 유지.
        private const float BaseBodySize = 0.6f;
        // Y좌표 기반 정렬(Docs/설계/13번 §6 B안) - 화면 아래쪽(Y가 작은) 유닛이 위에 그려지도록.
        private const int SortingOrderYScale = 100;

        private static readonly Color AllyColor = new(0.3f, 0.5f, 1f, 1f);
        private static readonly Color EnemyColor = new(1f, 0.35f, 0.35f, 1f);
        private static readonly Color FlashColor = Color.white;
        // 사망 시점에 진영 색(파랑/빨강)을 덮어쓰는 색 - 도주와 시각적으로 구분하기 위한 것(§11).
        private static readonly Color DeathTintColor = new(0.75f, 0.05f, 0.05f, 1f);

        private IBattleCombatant unit;
        private Color baseColor;
        // 이 유닛이 원거리 공격을 할 때마다 재사용하는 투사체 - 매 발사마다 Destroy+Instantiate하지
        // 않는다(CLAUDE.md 최적화 규칙). transform이 아니라 transform.parent의 자식이라 이 유닛
        // (공격자)이 파괴돼도 함께 파괴되지 않는다 - 발사 후 공격자가 죽어도 날아가는 중인 투사체는
        // 그대로 유지된다는 기존 동작과 동일하다.
        private SpriteRenderer projectileRenderer;

        private void Awake()
        {
            if (bodyRenderer == null) bodyRenderer = GetComponent<SpriteRenderer>();
        }

        public void Bind(IBattleCombatant unit)
        {
            this.unit = unit;
            // 적 진영 구분(기획 08번 §13.1) - unit.Icon이 있으면(약탈자/괴수/적대자 도형) 그대로 쓰고,
            // 없으면(아군, 아직 직업별 아이콘 미도입) 기존 단색 사각형으로 대체한다. 흰색 스프라이트라
            // 아래 진영 틴트가 그대로 곱해져 도형과 무관하게 색 구분은 유지된다(BattlePlaceholderSprite 참고).
            bodyRenderer.sprite = unit.Icon != null ? unit.Icon : BattlePlaceholderSprite.WhiteSquare;
            baseColor = unit.IsAlly ? AllyColor : EnemyColor;
            bodyRenderer.color = baseColor;
            transform.position = ToWorld(unit.Position);
            // MaxHp를 크기 신호로 재사용 - 방패병(150) > 전사(100) > 궁수(70) 순서가 자연히 만들어진다(기획 §12).
            transform.localScale = Vector3.one * (BaseBodySize * Mathf.Clamp(unit.MaxHp / 100f, 0.7f, 1.5f));
            UpdateSortingOrder();

            unit.OnDamaged += HandleDamaged;
            unit.OnAttacked += HandleAttacked;
            unit.OnDied += HandleDied;
            unit.OnFled += HandleFled;
        }

        private void Update()
        {
            if (unit == null || !unit.IsAlive) return;
            transform.position = ToWorld(unit.Position);
            UpdateSortingOrder();
        }

        private void UpdateSortingOrder() => bodyRenderer.sortingOrder = -Mathf.RoundToInt(transform.position.y * SortingOrderYScale);

        private static Vector3 ToWorld(Vector2 position) => new(position.x, position.y, 0f);

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
                StartCoroutine(FireProjectile(ToWorld(target.Position)));
            }
        }

        // 사망 처리 시점(HP 0 도달) 자체는 BattleCharacterUnit.TakeDamage에서 이 이벤트보다 먼저
        // 동기적으로 끝나 있다 - 여기서는 그 결과에 반응하는 순수 연출만 한다. 즉시 진영 색을 사망
        // 색으로 덮고 1.1배로 키운 뒤(§11), 페이드아웃하며 제거해 도주(HandleFled)와 구분한다.
        private void HandleDied()
        {
            bodyRenderer.color = DeathTintColor;
            transform.localScale *= DeathGrowScale;
            StartCoroutine(FadeOutAndDestroy());
        }

        // 전장을 완전히 벗어난 시점(OnFled)에도 도주 방향으로 계속 이동하며 페이드아웃한다(§11) -
        // 타겟팅/생사 판정은 이미 이 시점 이전에 끝나 있으므로(IsAlive), 여기서부터는 시뮬레이션
        // Tick 없이 뷰가 마지막 도주 속도(FleeVelocity)를 그대로 이어받아 순수 연출로만 이동시킨다.
        private void HandleFled() => StartCoroutine(FleeFadeOutAndDestroy(unit.FleeVelocity));

        private IEnumerator FlashWhite()
        {
            bodyRenderer.color = FlashColor;
            yield return new WaitForSeconds(HitFlashSeconds);
            // 사망 연출(빨간 틴트)이 이미 시작됐다면 원래 색으로 되돌리지 않는다 - 킬링 블로우처럼
            // OnDamaged와 OnDied가 같은 프레임에 함께 발생하면, 이 지연 복원이 사망 틴트를 덮어써버린다.
            if (bodyRenderer != null && unit.IsAlive) bodyRenderer.color = baseColor;
        }

        private IEnumerator Lunge(Vector2 direction)
        {
            var origin = transform.position;
            var peak = origin + (Vector3)(direction * LungeDistance);
            var half = LungeSeconds * 0.5f;

            var t = 0f;
            while (t < half)
            {
                t += Time.deltaTime;
                transform.position = Vector3.Lerp(origin, peak, t / half);
                yield return null;
            }
            t = 0f;
            while (t < half)
            {
                t += Time.deltaTime;
                transform.position = Vector3.Lerp(peak, origin, t / half);
                yield return null;
            }
        }

        private IEnumerator FireProjectile(Vector3 targetWorldPosition)
        {
            if (projectileRenderer == null)
            {
                var projectileGo = new GameObject("Projectile", typeof(SpriteRenderer));
                projectileGo.transform.SetParent(transform.parent, true);
                projectileGo.layer = gameObject.layer;
                projectileRenderer = projectileGo.GetComponent<SpriteRenderer>();
                projectileRenderer.sprite = BattlePlaceholderSprite.WhiteSquare;
                projectileGo.transform.localScale = Vector3.one * 0.15f;
            }

            projectileRenderer.color = baseColor;
            projectileRenderer.sortingOrder = short.MaxValue; // 투사체는 정렬 대상이 아니라 항상 최상단.
            projectileRenderer.gameObject.SetActive(true);

            var origin = transform.position;
            var elapsed = 0f;
            while (elapsed < ProjectileSeconds)
            {
                elapsed += Time.deltaTime;
                projectileRenderer.transform.position = Vector3.Lerp(origin, targetWorldPosition, elapsed / ProjectileSeconds);
                yield return null;
            }
            projectileRenderer.gameObject.SetActive(false);
        }

        private IEnumerator FadeOutAndDestroy()
        {
            var elapsed = 0f;
            var startColor = bodyRenderer.color;
            while (elapsed < FadeOutSeconds)
            {
                elapsed += Time.deltaTime;
                var alpha = Mathf.Lerp(startColor.a, 0f, elapsed / FadeOutSeconds);
                bodyRenderer.color = new Color(startColor.r, startColor.g, startColor.b, alpha);
                yield return null;
            }
            Destroy(gameObject);
        }

        // FadeOutAndDestroy와 거의 같지만, 도주는 정지 상태로 페이드아웃하지 않는다 - OnFled 이후에도
        // 마지막 도주 속도로 계속 이동한다(§11 "도주를 멈추지 않고"). 위치가 계속 바뀌므로 정렬 순서도
        // 함께 갱신한다.
        private IEnumerator FleeFadeOutAndDestroy(Vector2 velocity)
        {
            var elapsed = 0f;
            var startColor = bodyRenderer.color;
            while (elapsed < FadeOutSeconds)
            {
                elapsed += Time.deltaTime;
                transform.position += (Vector3)(velocity * Time.deltaTime);
                UpdateSortingOrder();
                var alpha = Mathf.Lerp(startColor.a, 0f, elapsed / FadeOutSeconds);
                bodyRenderer.color = new Color(startColor.r, startColor.g, startColor.b, alpha);
                yield return null;
            }
            Destroy(gameObject);
        }
    }
}
