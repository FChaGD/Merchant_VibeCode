using System;
using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>
    /// 이벤트 대본의 표시 분기(설계 89 §4.1 ④·§5.4). `#choice` 줄은 이벤트 대화 화면에 내보내지 않고 선택 화면 설명문으로 쌓아 둔 뒤 즉시 넘긴다(타자기 없음).
    /// 선택지는 선택 화면으로 보내고, 고르면 선택 화면을 닫아 대화 화면으로 돌아간다(모달 묶음이 되돌림). 효과 결과 줄은 화자 없는 서술 줄로 같은 대사 실행기를
    /// 탄다 - 클릭 넘김·출력 규칙이 대사와 같아야 하기 때문이다. 보이는 선택지가 전부 잠기면 선택 없음(-1)으로 넘긴다 - 대본이 멈추지 않게.
    /// 도주·패배로 이벤트가 끝나면(Abort) 남은 대본을 화면 없이 흘려보낸다 - 줄은 바로 넘기고 선택지는 건너뛴다. Yarn을 Stop으로 멈추지 않는 이유: 정리가 끝난
    /// 뒤 늦게 완료된 명령 코루틴이 끝난 대화를 이어가려 해 예외가 나고, 그 사이 다른 대화가 시작돼 있으면 그 대화를 넘겨 버린다.
    /// </summary>
    public sealed class EventPresentationTarget : IDialoguePresentationTarget
    {
        private readonly DialoguePlaybackController playback;
        private readonly IEventEncounterScreens screens;
        private readonly SpeakerPresentation speakers;
        private readonly List<ChoicePerson> people = new();
        private string description;
        private bool aborted;
        private Action pendingAdvance;      // 대화 화면에 떠 있는 줄의 넘김 - Abort가 대신 넘긴다
        private Action<int> pendingChoice;  // 선택 화면에서 기다리는 선택 - Abort가 선택 없음으로 넘긴다

        public EventPresentationTarget(DialoguePlaybackController playback, IEventEncounterScreens screens, SpeakerPresentation speakers)
        {
            this.playback = playback;
            this.screens = screens;
            this.speakers = speakers;
        }

        /// <summary>이벤트가 끝났다 - 이후 대본은 화면 없이 흘려보낸다.</summary>
        /// <summary>
        /// 이벤트가 끝났다 - 이후 대본은 화면 없이 흘려보낸다. 떠 있던 대사·선택지가 있으면(상행 종료로 끝난 경우) 그 줄을 넘겨야 Yarn이 다음 줄로 간다 -
        /// 대사 실행기를 멈추기만 하면 넘김 콜백이 버려져 대본이 그 자리에 멈춘다.
        /// </summary>
        public void Abort()
        {
            if (aborted) return;
            aborted = true;
            var advance = pendingAdvance;
            var choose = pendingChoice;
            pendingAdvance = null;
            pendingChoice = null;
            playback.Stop();
            if (choose != null)
            {
                screens.HideChoice();
                choose(-1);
            }
            advance?.Invoke();
        }

        public void PresentLine(DialogueLineData line, Action onAdvance)
        {
            if (aborted)
            {
                onAdvance();
                return;
            }
            if (!line.HasTag(DialogueTags.Choice))
            {
                pendingAdvance = onAdvance;
                playback.PresentLine(line, () =>
                {
                    pendingAdvance = null;
                    onAdvance();
                });
                return;
            }

            description = line.Text;
            people.Clear();
            if (line.TryGetTagValue(DialogueTags.People, out var list))
            {
                foreach (var raw in list.Split(','))
                {
                    var id = raw.Trim();
                    if (id.Length > 0) people.Add(new ChoicePerson(speakers.NameOf(id), SpeakerPresentation.ColorOf(id)));
                }
            }
            onAdvance();
        }

        public void PresentOptions(IReadOnlyList<DialogueOptionData> options, Action<int> onChosen)
        {
            if (aborted)
            {
                onChosen(-1);
                return;
            }
            var visible = new List<DialogueOptionData>();
            var anyAvailable = false;
            foreach (var option in options)
            {
                if (option.IsTimeout) continue; // 이벤트엔 제한시간이 없다
                visible.Add(option);
                anyAvailable |= option.IsAvailable;
            }

            var content = new EventChoiceContent(description, new List<ChoicePerson>(people), visible);
            description = null;
            people.Clear();
            if (!anyAvailable)
            {
                onChosen(-1);
                return;
            }

            var done = false;
            pendingChoice = onChosen;
            screens.ShowChoice(content, index =>
            {
                if (done || aborted || !IsAvailable(visible, index)) return;
                done = true;
                pendingChoice = null;
                screens.HideChoice();
                onChosen(index);
            });
        }

        public void EndPresentation()
        {
            if (!aborted) playback.EndPresentation();   // 끝난 이벤트의 화면은 진행자가 이미 닫았다
        }

        /// <summary>효과 결과 줄(설계 89 §2 - 효과 실행마다 한 줄, 클릭 넘김).</summary>
        public void PresentResult(string text, Action onAdvance) => playback.PresentLine(new DialogueLineData(string.Empty, text, null), onAdvance);

        private static bool IsAvailable(List<DialogueOptionData> visible, int index)
        {
            foreach (var o in visible) if (o.Index == index) return o.IsAvailable;
            return false;
        }
    }
}
