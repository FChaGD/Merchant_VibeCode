using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Core
{
    /// <summary>
    /// 상행 준비 UI의 출발지/도착지 정보 패널. 출발지/도착지 양쪽에 동일하게 재사용하는 표시 전용
    /// 컴포넌트다 - 패널을 클릭해 재지정하는 기능(구 02번 기획 3.1.1절)은 지도 클릭으로 지정 수단을
    /// 통일하면서 폐기됐다(기획 16번 §6).
    /// </summary>
    public class TripLocationInfoView : MonoBehaviour
    {
        [SerializeField] private Image iconImage;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text descriptionText;

        public void Show(ITripLocationInfo info)
        {
            if (info == null)
            {
                Clear();
                return;
            }

            if (iconImage != null)
            {
                iconImage.sprite = info.Icon;
                iconImage.enabled = info.Icon != null;
            }

            if (nameText != null)
            {
                nameText.text = info.DisplayName;
            }

            if (descriptionText != null)
            {
                descriptionText.text = info.Description;
            }
        }

        public void Clear()
        {
            if (iconImage != null)
            {
                iconImage.enabled = false;
            }

            if (nameText != null)
            {
                nameText.text = string.Empty;
            }

            if (descriptionText != null)
            {
                descriptionText.text = string.Empty;
            }
        }
    }
}
