using System.Collections;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// Wagon/Facility 1기를 표현한다. 정비창 팔레트에서 이미 쓰고 있는 아이콘(마차=삼각형/시설=원형)을
    /// 그대로 재사용한다 - 배치할 때 본 모양과 전투에서 보는 모양이 일치하도록, 별도의 사각형
    /// Placeholder 도형을 새로 만들지 않는다. 아이콘이 없는 예외적 경우에만 단색 사각형으로 대체한다.
    /// 이동 없음(기획 §4), 파괴 연출만 있다. 월드 오브젝트(SpriteRenderer) 기반이다(Docs/설계/13번).
    /// 마차는 파괴돼도 잔해로 남아 길을 막고 표적이 될 수 있어(Docs/기획/77번 §4-8) 제거하지 않고 연두색으로 바꾼다 - 잔해 표적
    /// (BattleWagonWreck)은 별도 뷰 없이 이 뷰가 그대로 보여준다. 시설은 지금처럼 사라진다.
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
        private static readonly Color WreckColor = new(0.6f, 0.9f, 0.4f, 1f);
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
            // 크기는 모델(BattleProtectedUnit.HalfSize)에서 읽는다 - 장애물 회피 반경과 같은 값이어야 한다(Docs/설계/72번 §3.1).
            // unit.Icon(PPU=100, 128px)은 원본이 1.28 월드유닛이라 정확히 같은 크기는 아니지만(Placeholder 수준 근사),
            // 폴백 단색 사각형(1x1 월드유닛)에는 정확히 적용된다.
            var bodySize = unit is BattleProtectedUnit protectedUnit ? protectedUnit.HalfSize * 2f : ProtectedUnitTuning.BodySize;
            transform.localScale = Vector3.one * bodySize;
            // 고정 배치라 Update에서 매 프레임 갱신할 필요 없이 최초 1회만 계산한다.
            bodyRenderer.sortingOrder = -Mathf.RoundToInt(transform.position.y * SortingOrderYScale);
            gaugeView?.Bind(unit);

            unit.OnDamaged += HandleDamaged;
            unit.OnDied += HandleDestroyed;
        }

        private void HandleDamaged(float amount) => StartCoroutine(FlashWhite());

        private void HandleDestroyed()
        {
            if (unit is BattleProtectedUnit { Kind: ProtectedUnitKind.Wagon })
            {
                ShowWreck();
                return;
            }
            StartCoroutine(FadeAndDestroy());
        }

        // 복원 시점에 살아 있을 때만 원래 색으로 - 마지막 일격의 흰색 플래시가 잔해 색을 덮어쓰지 않게 한다.
        private IEnumerator FlashWhite() => BattleHitFlash.Run(bodyRenderer, FlashColor, baseColor, HitFlashSeconds, () => unit.IsAlive);

        // 잔해는 체력이 없어 게이지를 숨긴다(설계 79번 §3.4). 뷰는 전투 정리(BattleViewPresenter) 때 다른 뷰와 함께 제거된다.
        private void ShowWreck()
        {
            bodyRenderer.color = WreckColor;
            gaugeView?.SetAlpha(0f);
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
