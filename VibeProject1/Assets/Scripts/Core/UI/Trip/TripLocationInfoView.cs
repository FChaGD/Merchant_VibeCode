using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Core
{
    /// <summary>
    /// 상행 준비 UI의 출발지/도착지 정보 패널. 지도에서 해당 핀을 클릭하기 전까지는 비어 있다.
    /// 출발지/도착지 양쪽에 동일하게 재사용한다.
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
