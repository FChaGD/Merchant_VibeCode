using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Core
{
    /// <summary>
    /// Wagon/Facility 1기를 표현한다. 정비창 팔레트에서 이미 쓰고 있는 아이콘(마차=삼각형/시설=원형)을
    /// 그대로 재사용한다 - 배치할 때 본 모양과 전투에서 보는 모양이 일치하도록, 별도의 사각형
    /// Placeholder 도형을 새로 만들지 않는다. 아이콘이 없는 예외적 경우에만 단색 사각형으로 대체한다.
    /// 이동 없음(기획 §4), 파괴 연출만 있다.
    /// </summary>
    public class BattleProtectedUnitView : MonoBehaviour
    {
        [SerializeField] private Image bodyImage;

        private const float PositionScale = 40f;
        private const float HitFlashSeconds = 0.1f;
        private const float DeathFadeSeconds = 0.3f;
        private static readonly Color FallbackBodyColor = new(0.85f, 0.75f, 0.3f, 1f);
        private static readonly Color FlashColor = Color.white;

        private IDamageable unit;
        private Color baseColor;

        public void Bind(IDamageable unit)
        {
            this.unit = unit;

            if (unit.Icon != null)
            {
                bodyImage.sprite = unit.Icon;
                baseColor = Color.white; // 아이콘 원본 색(팔레트와 동일)을 그대로 보여준다
            }
            else
            {
                baseColor = FallbackBodyColor; // 아이콘이 없는 예외적 경우에만 단색으로 대체
            }
            bodyImage.color = baseColor;

            ((RectTransform)transform).anchoredPosition = unit.Position * PositionScale; // 고정 배치, 이후 갱신 없음

            unit.OnDamaged += HandleDamaged;
            unit.OnDied += HandleDestroyed;
        }

        private void HandleDamaged(float amount) => StartCoroutine(FlashWhite());
        private void HandleDestroyed() => StartCoroutine(FadeAndDestroy());

        private IEnumerator FlashWhite()
        {
            bodyImage.color = FlashColor;
            yield return new WaitForSeconds(HitFlashSeconds);
            if (bodyImage != null) bodyImage.color = baseColor;
        }

        private IEnumerator FadeAndDestroy()
        {
            var elapsed = 0f;
            while (elapsed < DeathFadeSeconds)
            {
                elapsed += Time.deltaTime;
                var alpha = Mathf.Lerp(baseColor.a, 0f, elapsed / DeathFadeSeconds);
                bodyImage.color = new Color(baseColor.r, baseColor.g, baseColor.b, alpha);
                yield return null;
            }
            Destroy(gameObject);
        }
    }
}
