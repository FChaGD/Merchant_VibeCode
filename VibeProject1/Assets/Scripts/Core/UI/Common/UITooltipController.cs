using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 등록된 툴팁마다 중계 이벤트를 받아 표시 판정(TooltipVisibilityState)을 화면에 반영한다(Docs/설계/100번 §4.1).
    /// 툴팁 요소는 콘텐츠 씬에 있어 씬 언로드로 파괴될 수 있다 - 사유 변경 이벤트는 Bootstrap 상주 저장소에서 오므로
    /// 반영 전에 Unity null을 확인하고, 등록할 때마다 파괴된 항목을 정리한다.
    /// </summary>
    public sealed class UITooltipController : MonoBehaviour, ITooltipController
    {
        private readonly List<Entry> entries = new();

        public ITooltipHandle Register(PointerHoverRelay hover, PointerPressRelay press, GameObject tooltipRoot, TMP_Text text)
        {
            for (var i = entries.Count - 1; i >= 0; i--)
            {
                if (entries[i].IsDestroyed) entries[i].Dispose();
            }

            var entry = new Entry(this, hover, press, tooltipRoot, text);
            entries.Add(entry);
            return entry;
        }

        private void OnDestroy()
        {
            for (var i = entries.Count - 1; i >= 0; i--) entries[i].Dispose();
        }

        private sealed class Entry : ITooltipHandle
        {
            private readonly UITooltipController owner;
            private readonly PointerHoverRelay hover;
            private readonly PointerPressRelay press;
            private readonly GameObject root;
            private readonly TMP_Text text;
            private readonly TooltipVisibilityState state = new();
            private bool disposed;

            public Entry(UITooltipController owner, PointerHoverRelay hover, PointerPressRelay press, GameObject root, TMP_Text text)
            {
                this.owner = owner;
                this.hover = hover;
                this.press = press;
                this.root = root;
                this.text = text;

                if (hover != null)
                {
                    hover.PointerEntered += HandleEntered;
                    hover.PointerExited += HandleExited;
                }
                if (press != null)
                {
                    press.LongPressStarted += HandleLongPressStarted;
                    press.LongPressEnded += HandleLongPressEnded;
                }
                if (root != null) root.SetActive(false);
            }

            public bool IsDestroyed => root == null || text == null;

            public void SetText(string value)
            {
                if (!disposed && state.SetText(value)) Apply();
            }

            public void Dispose()
            {
                if (disposed) return;
                disposed = true;

                if (hover != null)
                {
                    hover.PointerEntered -= HandleEntered;
                    hover.PointerExited -= HandleExited;
                }
                if (press != null)
                {
                    press.LongPressStarted -= HandleLongPressStarted;
                    press.LongPressEnded -= HandleLongPressEnded;
                }
                if (root != null) root.SetActive(false);
                owner.entries.Remove(this);
            }

            private void HandleEntered() { if (state.SetHovering(true)) Apply(); }
            private void HandleExited() { if (state.SetHovering(false)) Apply(); }
            private void HandleLongPressStarted() { if (state.SetLongPressing(true)) Apply(); }
            private void HandleLongPressEnded() { if (state.SetLongPressing(false)) Apply(); }

            private void Apply()
            {
                if (IsDestroyed) return;
                if (state.IsVisible) text.text = state.Text;
                root.SetActive(state.IsVisible);
            }
        }
    }
}
