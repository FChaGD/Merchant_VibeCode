using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Core
{
    /// <summary>
    /// 상인조합 의뢰 목록 한 줄(Docs/설계/103번 §4.5) - 제목 | 의뢰자 | 상태("수락함"). 구매 행 전용(아이콘·가격) RosterShopCandidateRowView와
    /// 칸 구성이 달라 따로 둔다. 순수 표시 담당 - 클릭 처리는 주입된 델리게이트에 위임한다.
    /// </summary>
    public class GuildQuestRowView : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private TMP_Text titleLabel;
        [SerializeField] private TMP_Text requesterLabel;
        [SerializeField] private TMP_Text statusLabel;
        [SerializeField] private Outline selectionOutline;

        public RectTransform RectTransform => (RectTransform)transform;

        public void Bind(string title, string requester, string status, Action onClicked)
        {
            titleLabel.text = title;
            requesterLabel.text = requester;
            statusLabel.text = status;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => onClicked?.Invoke());
        }

        public void SetSelected(bool selected)
        {
            if (selectionOutline != null) selectionOutline.enabled = selected;
        }
    }
}
