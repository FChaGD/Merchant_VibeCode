using System;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 화면 루트를 켜고 끄기만 하는 IUIPanel(이벤트 대화·선택 화면, 설계 89 §6.3). 모달 묶음이 정비창·회수 적재를 열면 숨겼다가 닫히면 다시 여는데, 그
    /// "다시 열림"을 진행자가 알아야 대사 일시정지를 풀 수 있어 Opened를 낸다. plain C# - FieldUIWiring이 Field 로드마다 새로 만든다.
    /// </summary>
    public sealed class ViewRootPanel : IUIPanel
    {
        private readonly GameObject root;

        public ViewRootPanel(string panelId, GameObject root)
        {
            PanelId = panelId;
            this.root = root;
            root.SetActive(false);
        }

        public string PanelId { get; }
        public event Action Opened;

        public void Open()
        {
            root.SetActive(true);
            Opened?.Invoke();
        }

        // Close()는 표시/숨김만 한다. 이벤트를 끝내 화면을 닫는 것은 진행자가 화면 묶음(FieldEventScreens)을 거쳐 UIManager.Close(PanelId)로 한다.
        public void Close() => root.SetActive(false);
    }
}
