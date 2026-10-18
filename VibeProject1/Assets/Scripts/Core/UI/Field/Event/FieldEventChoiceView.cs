using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Core
{
    /// <summary>
    /// 선택 화면(설계 89 §6.2) - 설명문(즉시 전체 표시)·표시 인물(Placeholder 색 사각형 + 이름, 반전 없음)·선택지(제한시간 없음, 잠김은 흐림 + 잠김 문구).
    /// 인물·선택지 줄은 템플릿에서 처음 필요할 때만 만들고 이후 재사용한다. 루트 켜기·끄기는 패널(ViewRootPanel)이 한다.
    /// 줄 표시는 템플릿의 표시 컴포넌트(FieldEventPersonView·FieldEventOptionView)가 한다 - 하위 요소를 이름 문자열로 찾지 않는다(2026-10-16 검진).
    /// </summary>
    public class FieldEventChoiceView : MonoBehaviour
    {
        [SerializeField] private TMP_Text descriptionText;
        [SerializeField] private FieldEventPersonView personRowTemplate;
        [SerializeField] private FieldEventOptionView optionRowTemplate;
        [SerializeField] private Button formationButton;

        private readonly List<FieldEventPersonView> people = new();
        private readonly List<FieldEventOptionView> options = new();

        public event Action FormationRequested;

        private void OnEnable()
        {
            if (formationButton != null) formationButton.onClick.AddListener(RaiseFormation);
        }

        private void OnDisable()
        {
            if (formationButton != null) formationButton.onClick.RemoveListener(RaiseFormation);
        }

        private void RaiseFormation() => FormationRequested?.Invoke();

        public void Show(EventChoiceContent content, Action<int> onChosen)
        {
            descriptionText.text = content.Description;

            for (var i = 0; i < content.People.Count; i++) GetPerson(i).Show(content.People[i]);
            for (var i = content.People.Count; i < people.Count; i++) people[i].Hide();

            for (var i = 0; i < content.Options.Count; i++)
            {
                var option = content.Options[i];
                var text = $"{i + 1}. {option.Text}";
                var label = option.IsAvailable || option.LockText.Length == 0 ? text : $"{text} — {option.LockText}";
                var index = option.Index;
                GetOption(i).Show(label, option.IsAvailable, () => onChosen(index));
            }
            for (var i = content.Options.Count; i < options.Count; i++) options[i].Hide();
        }

        private FieldEventPersonView GetPerson(int index)
        {
            while (people.Count <= index) people.Add(Instantiate(personRowTemplate, personRowTemplate.transform.parent));
            return people[index];
        }

        private FieldEventOptionView GetOption(int index)
        {
            while (options.Count <= index) options.Add(Instantiate(optionRowTemplate, optionRowTemplate.transform.parent));
            return options[index];
        }
    }
}
