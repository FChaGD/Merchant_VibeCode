using System.Collections;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// Wagon/Facility 1기를 표현한다. 정비창 팔레트에서 이미 쓰고 있는 아이콘(마차=삼각형/시설=원형)을
    /// 그대로 재사용한다 - 배치할 때 본 모양과 전투에서 보는 모양이 일치하도록, 별도의 사각형
    /// Placeholder 도형을 새로 만들지 않는다. 아이콘이 없는 예외적 경우에만 단색 사각형으로 대체한다.
    /// 이동 없음(기획 §4), 파괴 연출만 있다. 월드 오브젝트(SpriteRenderer) 기반이다(Docs/설계/13번).
    /// </summary>
    public class BattleProtectedUnitView : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer bodyRenderer;
        // 체력 게이지바(사용자 요청, BattleHealthGaugeView) - BattleCharacterUnitView와 같은 방식으로 재사용.
        [SerializeField] private BattleHealthGaugeView gaugeView;

        private const float HitFlashSeconds = 0.1f;
        private const float DeathFadeSeconds = 0.3f;
        private static readonly Color FallbackBodyColor = new(0.85f, 0.75f, 0.3f, 1f);
        private static readonly Color FlashColor = Color.white;
        // 기존 UGUI 버전의 28px(=0.7 월드유닛, CoordinateToPixelScale=40 기준) 체감 크기를 그대로
        // 유지한다 - unit.Icon(FormationPlaceholderIcons 산출물, 기본 PPU=100, 128px 텍스처)을 쓸 땐
        // 원본이 이미 1.28 월드유닛이라 정확히 같은 크기는 아니지만(Placeholder 수준 근사), 폴백
        // 단색 사각형(BattlePlaceholderSprite, 1x1 월드유닛)에는 정확히 적용된다.
        private const float BodySize = 0.7f;
        private const int SortingOrderYScale = 100;

        private IDamageable unit;
        private Color baseColor;

        public void Bind(IDamageable unit)
        {
            this.unit = unit;

            if (unit.Icon != null)
            {
                bodyRenderer.sprite = unit.Icon;
                baseColor = Color.white; // 아이콘 원본 색(팔레트와 동일)을 그대로 보여준다
            }
            else
            {
                bodyRenderer.sprite = BattlePlaceholderSprite.WhiteSquare;
                baseColor = FallbackBodyColor; // 아이콘이 없는 예외적 경우에만 단색으로 대체
            }
            bodyRenderer.color = baseColor;

            transform.position = new Vector3(unit.Position.x, unit.Position.y, 0f); // 고정 배치, 이후 갱신 없음
            transform.localScale = Vector3.one * BodySize;
            // 고정 배치라 Update에서 매 프레임 갱신할 필요 없이 최초 1회만 계산한다.
            bodyRenderer.sortingOrder = -Mathf.RoundToInt(transform.position.y * SortingOrderYScale);
            gaugeView?.Bind(unit);

            unit.OnDamaged += HandleDamaged;
            unit.OnDied += HandleDestroyed;
        }

        private void HandleDamaged(float amount) => StartCoroutine(FlashWhite());
        private void HandleDestroyed() => StartCoroutine(FadeAndDestroy());

        private IEnumerator FlashWhite()
        {
            bodyRenderer.color = FlashColor;
            yield return new WaitForSeconds(HitFlashSeconds);
            if (bodyRenderer != null) bodyRenderer.color = baseColor;
        }

        private IEnumerator FadeAndDestroy()
        {
            var elapsed = 0f;
            while (elapsed < DeathFadeSeconds)
            {
                elapsed += Time.deltaTime;
                var alpha = Mathf.Lerp(baseColor.a, 0f, elapsed / DeathFadeSeconds);
                bodyRenderer.color = new Color(baseColor.r, baseColor.g, baseColor.b, alpha);
                gaugeView?.SetAlpha(alpha);
                yield return null;
            }
            Destroy(gameObject);
        }
    }
}
