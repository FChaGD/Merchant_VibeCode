using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Core
{
    /// <summary>
    /// 배치 UI의 정보 패널. 팔레트/그리드에서 클릭으로 선택된 유닛의 정보를 표시한다.
    /// 구체적으로 표시할 데이터 필드는 캐릭터/마차/시설 데이터 모델 설계 이후 확장한다.
    /// </summary>
    public class FormationInfoPanelView : MonoBehaviour
    {
        [SerializeField] private Image iconImage;
        [SerializeField] private TMP_Text nameText;

        public void Show(IFormationUnit unit)
        {
            if (unit == null)
            {
                Clear();
                return;
            }

            if (iconImage != null)
            {
                iconImage.sprite = unit.Icon;
                iconImage.enabled = unit.Icon != null;
            }

            if (nameText != null)
            {
                nameText.text = unit.DisplayName;
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
        }
    }
}
