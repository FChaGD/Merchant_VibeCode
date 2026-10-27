using System;

namespace Game.Core
{
    /// <summary>
    /// IEventEncounterScreens의 Field 구현(설계 89 §6.3). 패널 열기·닫기는 전부 UIManager를 거친다 - 이벤트 화면·선택 화면·정비창·회수 적재가 같은 모달
    /// 묶음이라, 위에 다른 화면을 열면 숨기고 닫으면 되돌리는 규칙을 그대로 얻는다. 시작·종료에 같이 바뀌는 상단 버튼 비활성과 정비창 열람 모드도 여기서
    /// 묶는다. plain C# - FieldUIWiring이 Field 로드마다 새로 만들어 진행자에 연결한다(이벤트는 뷰·패널 이벤트에 직접 붙여 따로 해제할 구독이 없다).
    /// </summary>
    public sealed class FieldEventScreens : IEventEncounterScreens
    {
        private readonly IPanelNavigator uiManager;
        private readonly FieldEventDialogueView dialogue;
        private readonly FieldEventChoiceView choice;
        private readonly ViewRootPanel dialoguePanel;
        private readonly ViewRootPanel choicePanel;
        private readonly FieldCargoRecoveryPanel cargoRecovery;   // 선택적
        private readonly IFieldTopButtons fieldUI;
        private readonly IFormationViewOnlyMode formation;          // 선택적

        public FieldEventScreens(IPanelNavigator uiManager, FieldEventDialogueView dialogue, FieldEventChoiceView choice, ViewRootPanel dialoguePanel, ViewRootPanel choicePanel,
            FieldCargoRecoveryPanel cargoRecovery, IFieldTopButtons fieldUI, IFormationViewOnlyMode formation)
        {
            this.uiManager = uiManager;
            this.dialogue = dialogue;
            this.choice = choice;
            this.dialoguePanel = dialoguePanel;
            this.choicePanel = choicePanel;
            this.cargoRecovery = cargoRecovery;
            this.fieldUI = fieldUI;
            this.formation = formation;
        }

        public bool IsAvailable => dialogue != null && choice != null;   // Unity 파괴 판정
        public IDialogueStageView DialogueStage => dialogue;

        public event Action Clicked
        {
            add => dialogue.Clicked += value;
            remove => dialogue.Clicked -= value;
        }

        public event Action FormationViewRequested
        {
            add
            {
                dialogue.FormationRequested += value;
                choice.FormationRequested += value;
            }
            remove
            {
                dialogue.FormationRequested -= value;
                choice.FormationRequested -= value;
            }
        }

        public event Action Reshown
        {
            add
            {
                dialoguePanel.Opened += value;
                choicePanel.Opened += value;
            }
            remove
            {
                dialoguePanel.Opened -= value;
                choicePanel.Opened -= value;
            }
        }

        public void ShowDialogue() => uiManager.Open(UIPanelIds.EventDialogue);
        public void HideDialogue() => uiManager.Close(UIPanelIds.EventDialogue);

        public void ShowChoice(EventChoiceContent content, Action<int> onChosen)
        {
            choice.Show(content, onChosen);
            uiManager.Open(UIPanelIds.EventChoice);
        }

        public void HideChoice() => uiManager.Close(UIPanelIds.EventChoice);

        public bool TryShowCargoRecovery(Action onDone)
        {
            if (cargoRecovery == null) return false;
            cargoRecovery.ShowVia(uiManager, onDone);
            return true;
        }

        public void OpenFormationViewer() => uiManager.Open(UIPanelIds.Formation);

        public void SetEventControls(bool eventActive)
        {
            fieldUI?.SetTopLevelButtonsInteractable(!eventActive);
            formation?.SetViewOnly(eventActive);
        }
    }
}
