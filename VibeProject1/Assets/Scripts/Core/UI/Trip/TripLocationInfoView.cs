using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.Core
{
    /// <summary>
    /// 상행 준비 UI의 출발지/도착지 정보 패널. 지도에서 해당 도시를 클릭하기 전까지는 비어 있다.
    /// 출발지/도착지 양쪽에 동일하게 재사용한다. 패널 자신을 클릭하면 "변경 모드"를 켤 수 있다
    /// (02번 기획 3.1.1절) - 실제 모드 전환 판단은 TripOriginDestinationAssigner가 담당하고, 이
    /// 컴포넌트는 클릭 이벤트를 그대로 위임만 한다.
    /// </summary>
    public class TripLocationInfoView : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private Image iconImage;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text descriptionText;

        private Action onPanelClicked;

        public void SetPanelClickHandler(Action panelClicked)
        {
            onPanelClicked = panelClicked;
        }

        public void OnPointerClick(PointerEventData eventData) => onPanelClicked?.Invoke();

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
