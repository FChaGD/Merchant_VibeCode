using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Core
{
    /// <summary>
    /// Field 말풍선 + 좌우 스탠딩(설계 88 §5). 표시 전용 - 자리·밝기·이름은 StagedDialogueView가 정해 준다. IUIPanel이 아니다(상행을 막지 않고 이동 뷰 위에 겹칠 뿐,
    /// FieldEncounterWarningView와 같은 성격). 루트는 항상 켜 두고 Body만 켜고 끈다 - 진행자가 루트 컴포넌트를 계속 참조하기 때문이다.
    /// 선택지 줄은 템플릿에서 처음 필요할 때만 만들고 이후 재사용한다.
    /// </summary>
    public class FieldDialogueView : MonoBehaviour, ITripDialogueView
    {
        private const float LockedAlpha = 0.5f;

        [SerializeField] private GameObject body;
        [SerializeField] private FieldDialogueClickCatcher clickCatcher;
        [SerializeField] private TMP_Text speakerNameText;
        [SerializeField] private TMP_Text lineText;
        [SerializeField] private GameObject continueMarker;
        [SerializeField] private GameObject leftRoot;
        [SerializeField] private Image leftFigure;
        [SerializeField] private TMP_Text leftName;
        [SerializeField] private GameObject rightRoot;
        [SerializeField] private Image rightFigure;
        [SerializeField] private TMP_Text rightName;
        [SerializeField] private GameObject optionsRoot;
        [SerializeField] private Button optionTemplate;
        [SerializeField] private Image timerFill;

        private sealed class OptionRow
        {
            public Button Button;
            public TMP_Text Label;
            public CanvasGroup Group;
        }

        private readonly List<OptionRow> rows = new();

        public event Action Clicked;

        private void Awake()
        {
            if (clickCatcher != null) clickCatcher.Clicked += RaiseClicked;
            SetVisible(false);
        }

        private void OnDestroy()
        {
            if (clickCatcher != null) clickCatcher.Clicked -= RaiseClicked;
        }

        private void RaiseClicked() => Clicked?.Invoke();

        public void SetVisible(bool visible)
        {
            body.SetActive(visible);
            if (clickCatcher != null) clickCatcher.gameObject.SetActive(visible);
            if (!visible) HideOptions();
        }

        public void SetBubble(string speakerName, string text)
        {
            DialogueStageRenderer.SetBubble(speakerNameText, lineText, speakerName, text);
        }

        public void SetVisibleCharacters(int count) => lineText.maxVisibleCharacters = count;

        public void SetContinueMarker(bool shown) => continueMarker.SetActive(shown);

        public void SetStanding(DialogueSide side, StandingState state)
        {
            var root = side == DialogueSide.Left ? leftRoot : rightRoot;
            var figure = side == DialogueSide.Left ? leftFigure : rightFigure;
            var nameText = side == DialogueSide.Left ? leftName : rightName;
            DialogueStageRenderer.SetStanding(root, figure, nameText, state);
        }

        public void ShowOptions(IReadOnlyList<DialogueOptionData> options, Action<int> onChosen)
        {
            optionsRoot.SetActive(true);
            for (var i = 0; i < options.Count; i++)
            {
                var row = GetRow(i);
                var option = options[i];
                row.Button.gameObject.SetActive(true);
                row.Button.interactable = option.IsAvailable;
                row.Group.alpha = option.IsAvailable ? 1f : LockedAlpha;
                row.Label.text = option.IsAvailable || option.LockText.Length == 0 ? option.Text : $"{option.Text} ({option.LockText})";
                row.Button.onClick.RemoveAllListeners();
                var index = option.Index;
                row.Button.onClick.AddListener(() => onChosen(index));
            }
            for (var i = options.Count; i < rows.Count; i++) rows[i].Button.gameObject.SetActive(false);
            timerFill.gameObject.SetActive(false);   // 제한시간이 있으면 진행자가 곧바로 SetOptionTimeRemaining을 부른다
        }

        public void SetOptionTimeRemaining(float fraction01)
        {
            timerFill.gameObject.SetActive(true);
            timerFill.fillAmount = fraction01;
        }

        public void HideOptions()
        {
            optionsRoot.SetActive(false);
            timerFill.gameObject.SetActive(false);
        }

        private OptionRow GetRow(int index)
        {
            while (rows.Count <= index)
            {
                var button = Instantiate(optionTemplate, optionTemplate.transform.parent);
                rows.Add(new OptionRow
                {
                    Button = button,
                    Label = button.GetComponentInChildren<TMP_Text>(true),
                    Group = button.GetComponent<CanvasGroup>(),
                });
            }
            return rows[index];
        }
    }
}
