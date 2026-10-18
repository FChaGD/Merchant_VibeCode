using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Core
{
    /// <summary>
    /// 선택 화면 표시 인물 한 줄(설계 89 §6.2·§6.3) - 그림·이름을 직렬화 참조로 든다. 템플릿을 복제해도 참조가 복제본 하위로 따라오므로 화면 코드가 이름
    /// 문자열로 하위 요소를 찾지 않는다(2026-10-16 검진 - 인스톨러와 화면이 계층 이름을 이중으로 알던 결합 제거).
    /// </summary>
    public class FieldEventPersonView : MonoBehaviour
    {
        [SerializeField] private Image figure;
        [SerializeField] private TMP_Text nameText;

        public void Show(ChoicePerson person)
        {
            gameObject.SetActive(true);
            var color = person.Color;
            color.a = 1f;
            figure.color = color;
            nameText.text = person.Name;
        }

        public void Hide() => gameObject.SetActive(false);
    }
}
