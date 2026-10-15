using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Core
{
    /// <summary>
    /// "대화" 버튼 + 상단원 목록(설계 88 §6). 표시 전용 - 모달이 아니어서 열려 있어도 상행은 계속된다. 버튼 활성 = 상단 버튼 조건(화면 공개·인카운터, FieldUIController)
    /// × 대화 조건(대화 중·상행 정지 아님, 진행자). 버튼이 꺼지면 목록도 닫는다. 목록 줄은 템플릿에서 처음 필요할 때만 만들고 재사용한다.
    /// </summary>
    public class FieldManualTalkView : MonoBehaviour, IManualTalkView
    {
        private const float NoTalkAlpha = 0.5f;
        private const string NoTalkSuffix = " - 할 말 없음";

        [SerializeField] private Button talkButton;
        [SerializeField] private GameObject memberList;
        [SerializeField] private Button memberTemplate;

        private sealed class MemberRow
        {
            public Button Button;
            public TMP_Text Label;
            public CanvasGroup Group;
        }

        private readonly List<MemberRow> rows = new();
        private bool topLevelAllowed = true;
        private bool talkAllowed;

        public event Action OpenRequested;
        public event Action<string> MemberChosen;

        private void Awake()
        {
            talkButton.onClick.AddListener(HandleTalkClicked);
            memberList.SetActive(false);
            Apply();
        }

        public void SetTopLevelAllowed(bool allowed)
        {
            topLevelAllowed = allowed;
            Apply();
        }

        public void SetTalkAllowed(bool allowed)
        {
            talkAllowed = allowed;
            Apply();
        }

        public void ShowMembers(IReadOnlyList<ManualTalkMember> members)
        {
            for (var i = 0; i < members.Count; i++)
            {
                var row = GetRow(i);
                var member = members[i];
                row.Button.gameObject.SetActive(true);
                row.Button.interactable = member.HasTalk;
                row.Group.alpha = member.HasTalk ? 1f : NoTalkAlpha;
                row.Label.text = member.HasTalk ? member.Name : member.Name + NoTalkSuffix;
                row.Button.onClick.RemoveAllListeners();
                var id = member.CharacterId;
                row.Button.onClick.AddListener(() => MemberChosen?.Invoke(id));
            }
            for (var i = members.Count; i < rows.Count; i++) rows[i].Button.gameObject.SetActive(false);
            memberList.SetActive(true);
        }

        public void HideMembers() => memberList.SetActive(false);

        private void HandleTalkClicked()
        {
            if (memberList.activeSelf) HideMembers();
            else OpenRequested?.Invoke();
        }

        private void Apply()
        {
            var on = topLevelAllowed && talkAllowed;
            talkButton.interactable = on;
            if (!on) HideMembers();
        }

        private MemberRow GetRow(int index)
        {
            while (rows.Count <= index)
            {
                var button = Instantiate(memberTemplate, memberTemplate.transform.parent);
                rows.Add(new MemberRow
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
