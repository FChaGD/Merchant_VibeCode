using System;
using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>
    /// 액션 Id별 구독 목록과 분배. Unity 입력에 의존하지 않아 EditMode로 검증한다.
    /// 텍스트 입력 중 차단은 맵 단위로 판정한다(Docs/설계/99번 §4.1).
    /// 콜백이 다른 구독을 해제할 수 있으므로(키로 팝업을 열면 다른 화면이 정리됨) 분배는 목록 복사본으로 한다.
    /// 알 수 없는 Id는 무동작 핸들을 돌려준다 — 미등록 경고는 액션 출처(GameInputActionSource)가 한 번만 낸다.
    /// </summary>
    public sealed class InputActionDispatcher
    {
        private readonly Predicate<string> isKnownAction;
        private readonly Func<bool> isTextInputFocused;
        private readonly Dictionary<string, List<Action>> subscribers = new Dictionary<string, List<Action>>();

        public InputActionDispatcher(Predicate<string> isKnownAction, Func<bool> isTextInputFocused)
        {
            this.isKnownAction = isKnownAction ?? throw new ArgumentNullException(nameof(isKnownAction));
            this.isTextInputFocused = isTextInputFocused ?? throw new ArgumentNullException(nameof(isTextInputFocused));
        }

        public IDisposable Subscribe(string actionId, Action onPerformed)
        {
            if (onPerformed == null || string.IsNullOrEmpty(actionId) || !isKnownAction(actionId))
                return Subscription.Empty;

            if (!subscribers.TryGetValue(actionId, out var list))
            {
                list = new List<Action>();
                subscribers.Add(actionId, list);
            }
            list.Add(onPerformed);
            return new Subscription(() => list.Remove(onPerformed));
        }

        public void Dispatch(string actionId)
        {
            if (string.IsNullOrEmpty(actionId) || !subscribers.TryGetValue(actionId, out var list) || list.Count == 0)
                return;
            if (GameInputActionIds.IsBlockedDuringTextInput(actionId) && isTextInputFocused())
                return;

            foreach (var callback in list.ToArray())
                callback();
        }

        private sealed class Subscription : IDisposable
        {
            public static readonly IDisposable Empty = new Subscription(null);

            private Action release;

            public Subscription(Action release)
            {
                this.release = release;
            }

            public void Dispose()
            {
                var action = release;
                release = null;
                action?.Invoke();
            }
        }
    }
}
