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
    /// (BattleWagonWreck)은 별도 뷰 없이 이 뷰가 그대로 보여준다. 시설도 잔해로 남는다(기획 90번 §3-7) - 화물이 없어 표적은 되지 않는다.
    /// 풀에서 재사용된다(Docs/Refactor/2026-10-26_씬전용.md A-1) - 전투 시작 정리 때 반환된다.
    /// </summary>
    public class BattleProtectedUnitView : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer bodyRenderer;
        // 체력 게이지바(사용자 요청, BattleHealthGaugeView) - BattleCharacterUnitView와 같은 방식으로 재사용.
        [SerializeField] private BattleHealthGaugeView gaugeView;

        private const float HitFlashSeconds = 0.1f;
        private static readonly Color FallbackBodyColor = new(0.85f, 0.75f, 0.3f, 1f);
        private static readonly Color FlashColor = Color.white;
        private static readonly Color WreckColor = new(0.6f, 0.9f, 0.4f, 1f);
        private const int SortingOrderYScale = 100;

        private IDamageable unit;
        private Color baseColor;

        public void Bind(IDamageable unit)
        {
            if (bodyRenderer == null) bodyRenderer = GetComponent<SpriteRenderer>();
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

        /// <summary>풀 반환 직전(Docs/Refactor/2026-10-26_씬전용.md A-2) - 유닛 이벤트 해제·깜빡임 정지. 잔해 색·게이지 알파는 다음 Bind가 되돌린다.</summary>
        public void Unbind()
        {
            if (unit != null)
            {
                unit.OnDamaged -= HandleDamaged;
                unit.OnDied -= HandleDestroyed;
            }
            unit = null;
            StopAllCoroutines();
            gaugeView?.Unbind();
        }

        // 보호 대상은 재생이 없어 체력이 피격으로만 바뀐다 - 게이지를 매 프레임이 아니라 피격 때 갱신한다(FO-2).
        private void HandleDamaged(float amount)
        {
            gaugeView?.Tick();
            StartCoroutine(FlashWhite());
        }

        private void HandleDestroyed() => ShowWreck();

        // 복원 시점에 살아 있을 때만 원래 색으로 - 마지막 일격의 흰색 플래시가 잔해 색을 덮어쓰지 않게 한다.
        private IEnumerator FlashWhite() => BattleHitFlash.Run(bodyRenderer, FlashColor, baseColor, HitFlashSeconds, () => unit.IsAlive);

        // 잔해는 체력이 없어 게이지를 숨긴다(설계 79번 §3.4). 뷰는 전투 정리(BattleViewPresenter) 때 다른 뷰와 함께 풀로 반환된다.
        private void ShowWreck()
        {
            bodyRenderer.color = WreckColor;
            gaugeView?.SetAlpha(0f);
        }
    }
}
