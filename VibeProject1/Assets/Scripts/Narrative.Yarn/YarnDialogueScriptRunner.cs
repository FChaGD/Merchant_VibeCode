#nullable enable
using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using Yarn.Unity;

namespace Game.Core
{
    /// <summary>
    /// IDialogueScriptRunner·IDialogueCommandRegistry의 Yarn 구현(설계 87 §7). Bootstrap 상주. 한 번에 대본 하나만 실행한다(대화·이벤트는 동시에 돌지
    /// 않는다 - 이벤트가 뜨면 대화를 먼저 끝낸다). onFinished는 한 번만 부른다. 중단 직후 새 대본 시작 요청은 Yarn 정리가 끝난 다음 프레임에 시작한다.
    /// </summary>
    public class YarnDialogueScriptRunner : MonoBehaviour, IDialogueScriptRunner, IDialogueCommandRegistry, IManagedComponent
    {
        [SerializeField] private DialogueRunner? runner;
        [SerializeField] private YarnEncounterVariableStorage? storage;
        [SerializeField] private YarnDialoguePresenterBridge? bridge;

        // Yarn의 늦은 완료 신호·완료 후 정리와 새 실행이 겹치지 않게 순서를 맡긴다(DialogueRunSequencer 요약 참고).
        private readonly DialogueRunSequencer sequencer = new();

        public bool IsRunning => sequencer.IsBusy;

        public void RegisterSelf(IDependencyRegistrar registrar)
        {
            registrar.Register<IDialogueScriptRunner>(this);
            registrar.Register<IDialogueCommandRegistry>(this);
        }

        public void ResolveDependencies(IDependencyResolver registrar)
        {
            if (runner == null || storage == null || bridge == null)
            {
                Debug.LogWarning($"{nameof(YarnDialogueScriptRunner)}: Yarn 구성 요소가 연결되지 않았다(Tools > Game > Build Bootstrap Scene).");
                return;
            }
            registrar.TryResolve<IEncounterHistoryReader>(out var reader);
            registrar.TryResolve<IEncounterHistoryWriter>(out var writer);
            registrar.TryResolve<IEncounterScriptVariableStore>(out var scripts);
            storage.Bind(reader, writer, scripts);
            if (runner.onDialogueComplete != null)
            {
                runner.onDialogueComplete.RemoveListener(HandleComplete);
                runner.onDialogueComplete.AddListener(HandleComplete);
            }
        }

        public bool HasNode(string node) => runner != null && runner.YarnProject != null && runner.YarnProject.NodeNames != null
            && runner.YarnProject.NodeNames.Contains(node);

        /// <summary>앞 대본의 중단·완료 정리가 끝나지 않았으면 정리 직후(다음 프레임)에 시작한다 - true를 돌려주고 시작은 미뤄진다.</summary>
        public bool TryRun(string node, IDialoguePresentationTarget target, Action<bool> onFinished)
        {
            if (runner == null || bridge == null || !HasNode(node)) return false;
            return sequencer.TryBegin(
                () =>
                {
                    bridge.Target = target;
                    // StartDialogue·Stop은 async YarnTask다 - 완료는 onDialogueComplete로 받으므로 기다리지 않는다.
                    _ = runner.StartDialogue(node);
                },
                completed =>
                {
                    bridge.Target = null;
                    onFinished(completed);
                });
        }

        public void Stop()
        {
            if (runner == null) return;
            if (sequencer.Stop()) _ = runner.Stop();
        }

        public void AddCommand(string name, Delegate handler) => runner?.AddCommandHandler(name, handler);
        public void AddFunction(string name, Delegate implementation) => runner?.AddFunction(name, implementation);

        // Yarn은 이 신호 뒤에도 내부 정리를 이어 가므로, 다음 대본은 한 프레임 뒤에 시작한다.
        private void HandleComplete()
        {
            sequencer.NotifyEngineComplete();
            if (isActiveAndEnabled) StartCoroutine(SettleNextFrame());
            else sequencer.NotifySettled();
        }

        private IEnumerator SettleNextFrame()
        {
            yield return null;
            sequencer.NotifySettled();
        }
    }
}
