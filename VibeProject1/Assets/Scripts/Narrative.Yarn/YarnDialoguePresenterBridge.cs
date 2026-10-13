#nullable enable
using System.Collections.Generic;
using System.Threading.Tasks;
using Yarn.Unity;

namespace Game.Core
{
    /// <summary>
    /// Yarn 줄·선택지를 Yarn을 모르는 표시 계약(IDialoguePresentationTarget)으로 넘긴다(설계 87 §7.1). 진행 타이밍(타자기·대기·제한시간)은 전부 상대편
    /// (DialoguePlaybackController)이 정하고, 여기서는 완료 콜백이 올 때까지 기다리기만 한다.
    /// </summary>
    public class YarnDialoguePresenterBridge : DialoguePresenterBase
    {
        public IDialoguePresentationTarget? Target { get; set; }

#pragma warning disable CS1998 // 기다릴 작업이 없는 시작·종료 처리 - async로 YarnTask를 만든다.
        public override async YarnTask OnDialogueStartedAsync() { }

        public override async YarnTask OnDialogueCompleteAsync()
        {
            Target?.EndPresentation();
        }
#pragma warning restore CS1998

        public override async YarnTask RunLineAsync(LocalizedLine line, LineCancellationToken token)
        {
            if (Target == null) return;
            var done = new TaskCompletionSource<bool>();
            var data = new DialogueLineData(line.CharacterName, line.TextWithoutCharacterName.Text, line.Metadata);
            Target.PresentLine(data, () => done.TrySetResult(true));
            using (token.NextLineToken.Register(() => done.TrySetResult(false)))
            {
                await done.Task;
            }
        }

        // CancellationToken 오버로드는 obsolete - LineCancellationToken 오버로드를 쓴다.
        public override async YarnTask<DialogueOption?> RunOptionsAsync(DialogueOption[] dialogueOptions, LineCancellationToken cancellationToken)
        {
            if (Target == null) return await DialogueRunner.NoOptionSelected;
            var list = new List<DialogueOptionData>(dialogueOptions.Length);
            for (var i = 0; i < dialogueOptions.Length; i++)
            {
                var option = dialogueOptions[i];
                list.Add(new DialogueOptionData(i, option.Line.TextWithoutCharacterName.Text, option.IsAvailable, option.Line.Metadata));
            }

            var chosen = new TaskCompletionSource<int>();
            Target.PresentOptions(list, index => chosen.TrySetResult(index));
            using (cancellationToken.NextLineToken.Register(() => chosen.TrySetResult(-1)))
            {
                var index = await chosen.Task;
                if (index >= 0 && index < dialogueOptions.Length) return dialogueOptions[index];
                return await DialogueRunner.NoOptionSelected;   // 선택 없음 - Yarn이 선택지 묶음을 건너뛴다
            }
        }
    }
}
