using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Core
{
    /// <summary>
    /// 프로젝트 전역 액션(InputSystem.actions)에서 GameInputActionIds 액션을 찾아 performed를 Id로 넘긴다.
    /// 전역 액션 자산은 Play 종료 후에도 살아 있으므로 붙인 처리기를 Disconnect에서 반드시 떼어 낸다 —
    /// 남기면 다음 Play에서 키 1회에 처리가 두 번 일어난다.
    /// 전역 액션이 비었거나 Id가 없으면 경고를 한 번만 내고 해당 액션은 무동작으로 둔다.
    /// </summary>
    public sealed class GameInputActionSource : MonoBehaviour, IGameInputActionSource
    {
        private const string RegisterMenuHint = "Tools > Game > Input > Register Game Input을 실행하세요.";

        private readonly Dictionary<string, InputAction> actionsById = new Dictionary<string, InputAction>();
        private readonly Dictionary<InputAction, Action<InputAction.CallbackContext>> handlers =
            new Dictionary<InputAction, Action<InputAction.CallbackContext>>();
        private bool cached;

        public bool HasAction(string actionId)
        {
            EnsureCached();
            return actionId != null && actionsById.ContainsKey(actionId);
        }

        public void Connect(Action<string> onPerformed)
        {
            if (onPerformed == null)
                return;
            EnsureCached();
            Disconnect();
            foreach (var pair in actionsById)
            {
                string id = pair.Key;
                Action<InputAction.CallbackContext> handler = _ => onPerformed(id);
                pair.Value.performed += handler;
                handlers.Add(pair.Value, handler);
            }
        }

        public void Disconnect()
        {
            foreach (var pair in handlers)
                pair.Key.performed -= pair.Value;
            handlers.Clear();
        }

        public void SetEnabled(bool enabled)
        {
            EnsureCached();
            foreach (var action in actionsById.Values)
            {
                var map = action.actionMap;
                if (map == null)
                    continue;
                if (enabled)
                    map.Enable();
                else
                    map.Disable();
            }
        }

        public string GetBindingLabel(string actionId)
        {
            EnsureCached();
            return actionId != null && actionsById.TryGetValue(actionId, out var action)
                ? action.GetBindingDisplayString()
                : string.Empty;
        }

        private void EnsureCached()
        {
            if (cached)
                return;
            cached = true;

            var asset = InputSystem.actions;
            if (asset == null)
            {
                Debug.LogWarning($"[GameInputActionSource] 전역 입력 액션이 지정되지 않았습니다. {RegisterMenuHint}");
                return;
            }

            List<string> missing = null;
            foreach (string id in GameInputActionIds.All)
            {
                var action = asset.FindAction(id);
                if (action != null)
                    actionsById.Add(id, action);
                else
                    (missing ??= new List<string>()).Add(id);
            }
            if (missing != null)
                Debug.LogWarning($"[GameInputActionSource] 전역 입력 액션 '{asset.name}'에 없는 액션: {string.Join(", ", missing)}. {RegisterMenuHint}");
        }
    }
}
