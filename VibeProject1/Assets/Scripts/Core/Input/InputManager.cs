using System;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 입력 하위 컴포넌트(액션 출처, 텍스트 입력 판정)와 구독 분배기를 연결만 한다 — 기능은 하위 컴포넌트가,
    /// 화면 상태 판단은 구독자가 맡는다(사용자 지시 2026-10-20, Docs/설계/99번 §4.3).
    /// 전역 액션 자산은 Play 사이에 살아 있으므로 파괴 시 처리기를 떼어 낸다.
    /// </summary>
    public class InputManager : MonoBehaviour, IInputManager, IManagedComponent
    {
        private IGameInputActionSource source;
        private InputActionDispatcher dispatcher;

        private void Awake()
        {
            source = GetComponent<IGameInputActionSource>()
                ?? throw new InvalidOperationException($"{nameof(InputManager)}: 같은 GameObject에 {nameof(IGameInputActionSource)}가 없습니다.");
            var guard = GetComponent<ITextInputFocusGuard>()
                ?? throw new InvalidOperationException($"{nameof(InputManager)}: 같은 GameObject에 {nameof(ITextInputFocusGuard)}가 없습니다.");

            dispatcher = new InputActionDispatcher(source.HasAction, () => guard.IsTextInputFocused);
            source.Connect(dispatcher.Dispatch);
        }

        private void OnEnable()
        {
            source?.SetEnabled(true);
        }

        private void OnDisable()
        {
            source?.SetEnabled(false);
        }

        private void OnDestroy()
        {
            source?.Disconnect();
        }

        public void RegisterSelf(IDependencyRegistrar registrar)
        {
            registrar.Register<IInputManager>(this);
        }

        public void ResolveDependencies(IDependencyResolver registrar)
        {
        }

        public IDisposable Subscribe(string actionId, Action onPerformed)
        {
            return dispatcher.Subscribe(actionId, onPerformed);
        }

        public string GetBindingLabel(string actionId)
        {
            return source.GetBindingLabel(actionId);
        }
    }
}
