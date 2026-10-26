using System;
using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>
    /// 편집 본문의 버리기(쓰레기통 드롭 버리기 + 선택 버리기, Docs/설계/102번 §4.3). 편집 컨트롤러가 이미 렌더링·드래그·정렬·섹션 전환을
    /// 맡고 있어 선택 상태·경고 대화상자는 이 협력 객체로 뺐다(SRP). 편집 컨트롤러는 선택 상태인지 묻고, 아이템 클릭·쓰레기통 드롭을 넘기고,
    /// 상태가 바뀌면 콜백으로 다시 그린다.
    /// 선택은 Id로 기억해 마차를 바꿔도 유지되고, 저장소가 바뀌면 사라진 아이템을 걸러 낸다. 경고가 떠 있는 사이 아이템이 다른 경로로
    /// 사라지면 저장 계약이 아무것도 지우지 않고 실패하므로 선택을 비우고 다시 고르게 한다.
    /// </summary>
    internal sealed class InventoryDiscardController : IDisposable
    {
        public delegate bool ItemFinder(string instanceId, out InventoryItemInstance item);

        private const string SelectingText = "버릴 아이템을 선택하세요 ({0}개 선택)";
        private const string FailedText = "버리지 못했습니다. 다시 선택하세요.";
        private const string ConfirmText = "확인";
        private const string CancelText = "취소";

        private readonly InventoryArrangementElements elements;
        private readonly IInventoryArrangement arrangement;
        private readonly IGoldItemInfo goldInfo; // 상단 물류품 화면만 - 없으면 골드 상자 줄을 적지 않는다
        private readonly ItemFinder findItem;
        private readonly Action onStateChanged;
        private readonly InventoryDiscardSelection selection = new();
        private string failedMessage;

        public InventoryDiscardController(InventoryArrangementElements elements, IInventoryArrangement arrangement, IGoldItemInfo goldInfo, ItemFinder findItem, Action onStateChanged)
        {
            this.elements = elements;
            this.arrangement = arrangement;
            this.goldInfo = goldInfo;
            this.findItem = findItem;
            this.onStateChanged = onStateChanged;

            elements.DiscardButton.onClick.RemoveAllListeners();
            elements.DiscardButton.onClick.AddListener(ToggleSelecting);
            elements.DiscardConfirmButton.onClick.RemoveAllListeners();
            elements.DiscardConfirmButton.onClick.AddListener(ConfirmSelection);
            elements.DiscardCancelButton.onClick.RemoveAllListeners();
            elements.DiscardCancelButton.onClick.AddListener(End);
            elements.DiscardDialog.Hide();
            UpdateButtons();
        }

        public bool IsSelecting { get; private set; }

        /// <summary>정보 줄에 우선 표시할 문구. 없으면 null - 편집 컨트롤러가 평소 문구를 쓴다.</summary>
        public string InfoText => failedMessage ?? (IsSelecting ? string.Format(SelectingText, selection.Count) : null);

        public bool IsMarked(string instanceId) => IsSelecting && selection.Contains(instanceId);

        public void HandleItemClicked(string instanceId)
        {
            if (!IsSelecting) return;

            failedMessage = null;
            selection.Toggle(instanceId);
            Notify();
        }

        /// <summary>쓰레기통에 놓은 아이템 1개 - 확인하면 버리고, 취소하면 아무것도 하지 않는다(아이템은 이미 원위치).</summary>
        public void RequestDropDiscard(string instanceId)
        {
            if (!findItem(instanceId, out var item)) return;

            failedMessage = null;
            ShowWarning(new[] { item }, () => Discard(new[] { instanceId }));
        }

        /// <summary>저장소가 바뀌었을 때 - 사라진 아이템을 선택에서 뺀다. 편집 컨트롤러가 곧 다시 그리므로 알리지 않는다.</summary>
        public void Prune()
        {
            selection.Prune(id => findItem(id, out _));
            UpdateButtons();
        }

        /// <summary>선택 상태를 끝내고 대화상자를 닫는다 - 취소·쓰레기통 재클릭·버린 뒤·본문 숨김.</summary>
        public void End()
        {
            var changed = IsSelecting || failedMessage != null;
            IsSelecting = false;
            failedMessage = null;
            selection.Clear();
            if (elements.DiscardDialog != null) elements.DiscardDialog.Hide();
            UpdateButtons();
            if (changed) Notify();
        }

        public void Dispose()
        {
            if (elements.DiscardButton != null) elements.DiscardButton.onClick.RemoveListener(ToggleSelecting);
            if (elements.DiscardConfirmButton != null) elements.DiscardConfirmButton.onClick.RemoveListener(ConfirmSelection);
            if (elements.DiscardCancelButton != null) elements.DiscardCancelButton.onClick.RemoveListener(End);
        }

        private void ToggleSelecting()
        {
            if (IsSelecting)
            {
                End();
                return;
            }

            IsSelecting = true;
            failedMessage = null;
            selection.Clear();
            UpdateButtons();
            Notify();
        }

        private void ConfirmSelection()
        {
            if (!IsSelecting || selection.Count == 0) return;

            var items = new List<InventoryItemInstance>(selection.Count);
            foreach (var id in selection.Ids)
            {
                if (findItem(id, out var item)) items.Add(item);
            }
            var ids = new List<string>(selection.Ids);
            // 취소하면 선택을 그대로 둔 채 선택 상태로 돌아간다(기획 95).
            ShowWarning(items, () => Discard(ids));
        }

        private void ShowWarning(IReadOnlyList<InventoryItemInstance> items, Action onConfirm)
        {
            elements.DiscardDialog.Show(InventoryDiscardWarning.Compose(items, goldInfo), ConfirmText, CancelText, onConfirm, null);
        }

        private void Discard(IReadOnlyCollection<string> ids)
        {
            if (arrangement.TryDiscard(ids))
            {
                End(); // OnChanged로 이미 다시 그려졌어도 선택 표시를 지우려면 한 번 더 알린다.
                return;
            }

            selection.Clear();
            failedMessage = FailedText;
            UpdateButtons();
            Notify();
        }

        // 확인/취소는 선택 상태에서만 보이고, 확인은 1개 이상 골랐을 때만 누를 수 있다.
        private void UpdateButtons()
        {
            if (elements.DiscardConfirmButton == null || elements.DiscardCancelButton == null) return;

            elements.DiscardConfirmButton.gameObject.SetActive(IsSelecting);
            elements.DiscardCancelButton.gameObject.SetActive(IsSelecting);
            elements.DiscardConfirmButton.interactable = selection.Count > 0;
        }

        private void Notify()
        {
            UpdateButtons();
            onStateChanged?.Invoke();
        }
    }
}
