using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Core
{
    /// <summary>
    /// 이벤트 대화 화면(설계 89 §6.1) - 전체화면 반투명 배경 위 말풍선(일러1·2 사이)·좌우 스탠딩. 표시 전용 - 자리·밝기·이름은 StagedDialogueView가 정한다.
    /// 꼬리는 밝게 표시된(말하는) 자리를 가리키고, 서술·결과 줄(양쪽 어두움)은 숨긴다. 선택지는 선택 화면이 맡아 여기서는 그리지 않는다.
    /// 배경 전체가 클릭 받기라 화면 아무 곳이나 누르면 넘어간다. 루트 켜기·끄기는 패널(ViewRootPanel)이 한다 - 루트가 꺼지면 구독도 OnDisable에서 풀린다.
    /// </summary>
    public class FieldEventDialogueView : MonoBehaviour, IDialogueStageView
    {
        private const float DimFactor = 0.45f;

        [SerializeField] private GameObject body;
        [SerializeField] private FieldDialogueClickCatcher clickCatcher;
        [SerializeField] private TMP_Text speakerNameText;
        [SerializeField] private TMP_Text lineText;
        [SerializeField] private GameObject continueMarker;
        [SerializeField] private GameObject leftTail;
        [SerializeField] private GameObject rightTail;
        [SerializeField] private GameObject leftRoot;
        [SerializeField] private Image leftFigure;
        [SerializeField] private TMP_Text leftName;
        [SerializeField] private GameObject rightRoot;
        [SerializeField] private Image rightFigure;
        [SerializeField] private TMP_Text rightName;
        [SerializeField] private Button formationButton;

        private bool leftActive;
        private bool rightActive;

        public event Action Clicked;
        public event Action FormationRequested;

        private void OnEnable()
        {
            if (clickCatcher != null) clickCatcher.Clicked += RaiseClicked;
            if (formationButton != null) formationButton.onClick.AddListener(RaiseFormation);
        }

        private void OnDisable()
        {
            if (clickCatcher != null) clickCatcher.Clicked -= RaiseClicked;
            if (formationButton != null) formationButton.onClick.RemoveListener(RaiseFormation);
        }

        private void RaiseClicked() => Clicked?.Invoke();
        private void RaiseFormation() => FormationRequested?.Invoke();

        public void SetVisible(bool visible) => body.SetActive(visible);

        public void SetBubble(string speakerName, string text)
        {
            speakerNameText.text = speakerName;
            lineText.text = text;
            lineText.maxVisibleCharacters = 0;
        }

        public void SetVisibleCharacters(int count) => lineText.maxVisibleCharacters = count;

        public void SetContinueMarker(bool shown) => continueMarker.SetActive(shown);

        public void SetStanding(DialogueSide side, StandingState state)
        {
            var root = side == DialogueSide.Left ? leftRoot : rightRoot;
            var figure = side == DialogueSide.Left ? leftFigure : rightFigure;
            var nameText = side == DialogueSide.Left ? leftName : rightName;
            var speaking = state.Visible && state.Highlighted;
            if (side == DialogueSide.Left) leftActive = speaking;
            else rightActive = speaking;
            leftTail.SetActive(leftActive);
            rightTail.SetActive(rightActive);

            root.SetActive(state.Visible);
            if (!state.Visible) return;
            var color = state.Highlighted ? state.Color : state.Color * DimFactor;
            color.a = 1f;
            figure.color = color;
            nameText.text = state.Name;
        }

        // 선택지는 선택 화면(FieldEventChoiceView)이 그린다 - EventPresentationTarget이 이 화면으로 보내지 않는다.
        public void ShowOptions(IReadOnlyList<DialogueOptionData> options, Action<int> onChosen)
            => Debug.LogWarning($"{nameof(FieldEventDialogueView)}: 선택지는 선택 화면이 그린다.");

        public void SetOptionTimeRemaining(float fraction01) { }

        public void HideOptions() { }
    }
}
