using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Core
{
    /// <summary>
    /// 상행 진행 게이지 표시 전담. ISessionState를 직접 구독하지 않는 순수 뷰다 - FieldUIController가
    /// 이벤트를 받아 SetProgress를 호출한다. 구간 라벨("구간 n/N · 등급", Docs/설계/76번 §7.2)도 함께 든다.
    /// </summary>
    public class FieldProgressGaugeView : MonoBehaviour
    {
        [SerializeField] private Image fillImage;
        [SerializeField] private TMP_Text legLabel;

        public void SetLegLabel(string text)
        {
            if (legLabel != null) legLabel.text = text;
        }

        public void SetProgress(float progress)
        {
            if (fillImage != null)
            {
                fillImage.fillAmount = Mathf.Clamp01(progress);
            }
        }
    }
}
