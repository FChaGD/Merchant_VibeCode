using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>레이어를 숨길지 정하는 참/거짓 신호. 값이 바뀌면 Changed를 낸다.</summary>
    public interface IUIHideCondition
    {
        bool IsActive { get; }
        event Action Changed;
    }

    /// <summary>
    /// 숨김(안 보이고 못 누름) / 입력 차단(보이지만 못 누름, Docs/Refactor/2026-10-26_씬전용.md E-2 - 휴식 진행 중 재화 HUD).
    /// 같은 레이어에 둘이 함께 걸리면 숨김이 우선이다.
    /// </summary>
    public enum LayerHideMode
    {
        Hide,
        BlockInput,
    }

    /// <summary>조건이 참인 동안 숨기거나 입력을 막을 레이어 목록(Docs/Refactor/2026-10-26_공통.md B-4). 씬 배선이 규칙표로 등록한다.</summary>
    public readonly struct LayerHideRule
    {
        public IUIHideCondition Condition { get; }
        public IReadOnlyList<CanvasGroup> Layers { get; }
        public LayerHideMode Mode { get; }

        public LayerHideRule(IUIHideCondition condition, IReadOnlyList<CanvasGroup> layers, LayerHideMode mode = LayerHideMode.Hide)
        {
            Condition = condition;
            Layers = layers;
            Mode = mode;
        }
    }

    /// <summary>값을 직접 켜고 끄는 조건 - 패널이 자기 상태(예: 휴식 진행 중)를 규칙 조건으로 내보낼 때 쓴다. 값이 실제로 바뀔 때만 Changed를 낸다.</summary>
    public sealed class UIHideToggle : IUIHideCondition
    {
        public bool IsActive { get; private set; }
        public event Action Changed;

        public void Set(bool active)
        {
            if (IsActive == active) return;
            IsActive = active;
            Changed?.Invoke();
        }
    }

    /// <summary>
    /// 기존 신호를 숨김 조건으로 바꾸는 어댑터. 게이트는 신호의 출처(UI 두 축·Field 이벤트 가림)를 모르고 조건만 본다 - Hub와 Field가
    /// 같은 장치를 쓰게 하려는 것이다(S-2).
    /// </summary>
    public static class UIHideConditions
    {
        public static IUIHideCondition ModalPopupOpen(IUIVisibilitySignal signal) => new SignalCondition(() => signal.IsModalPopupOpen, h => signal.ModalPopupOpenChanged += h, h => signal.ModalPopupOpenChanged -= h);

        public static IUIHideCondition ScreenOpen(IUIVisibilitySignal signal) => new SignalCondition(() => signal.IsScreenOpen, h => signal.ScreenOpenChanged += h, h => signal.ScreenOpenChanged -= h);

        public static IUIHideCondition From(IFieldUIHideReader reader) => new ReaderCondition(reader);

        // 신호 구독은 조건의 Changed 구독자가 있을 때만 건다 - 게이트가 규칙을 교체하면 구독이 함께 풀린다.
        private sealed class SignalCondition : IUIHideCondition
        {
            private readonly Func<bool> read;
            private readonly Action<Action<bool>> subscribe;
            private readonly Action<Action<bool>> unsubscribe;
            private Action changed;

            public SignalCondition(Func<bool> read, Action<Action<bool>> subscribe, Action<Action<bool>> unsubscribe)
            {
                this.read = read;
                this.subscribe = subscribe;
                this.unsubscribe = unsubscribe;
            }

            public bool IsActive => read();

            public event Action Changed
            {
                add { if (changed == null) subscribe(Relay); changed += value; }
                remove { changed -= value; if (changed == null) unsubscribe(Relay); }
            }

            private void Relay(bool _) => changed?.Invoke();
        }

        private sealed class ReaderCondition : IUIHideCondition
        {
            private readonly IFieldUIHideReader reader;

            public ReaderCondition(IFieldUIHideReader reader) => this.reader = reader;

            public bool IsActive => reader.IsHidden;

            public event Action Changed
            {
                add => reader.Changed += value;
                remove => reader.Changed -= value;
            }
        }
    }
}
