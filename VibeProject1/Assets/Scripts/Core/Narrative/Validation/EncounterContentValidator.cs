using System;
using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>
    /// 콘텐츠 정합성 검사(설계 87 §9). 테이블: YarnNode 존재. 대본: 화자 Id 존재, 선택지 묶음마다 `#timeout` 0~1개. 선택지 묶음 = 같은 들여쓰기의 연속
    /// 선택지(사이의 더 깊은 줄은 선택지 본문). 선택지 앞 질문 줄의 `#ask`는 요구하지 않는다 - Yarn이 그 줄에 lastline을 자동으로 붙인다.
    /// 좌우 자리 규칙(88)·명령·#choice 규칙(89)은 각 설계 제작 때 여기에 더한다.
    /// </summary>
    public static class EncounterContentValidator
    {
        public static List<string> ValidateTable(IReadOnlyList<EventEntry> events, IReadOnlyList<DialogueEntry> dialogues, Func<string, bool> nodeExists)
        {
            var errors = new List<string>();
            foreach (var e in events)
            {
                if (!nodeExists(e.YarnNode)) errors.Add($"이벤트 '{e.Id}': Yarn 노드 '{e.YarnNode}'가 없다.");
            }
            foreach (var d in dialogues)
            {
                if (!nodeExists(d.YarnNode)) errors.Add($"대화 '{d.Id}': Yarn 노드 '{d.YarnNode}'가 없다.");
            }
            return errors;
        }

        public static List<string> ValidateScripts(IReadOnlyList<YarnScriptNode> nodes, Func<string, bool> speakerExists)
        {
            var errors = new List<string>();
            foreach (var node in nodes)
            {
                var lines = node.Lines;
                for (var i = 0; i < lines.Count; i++)
                {
                    var line = lines[i];
                    if (!line.IsOption && !line.IsCommand && line.SpeakerId.Length > 0 && !speakerExists(line.SpeakerId))
                    {
                        errors.Add($"{node.Title}:{line.LineNumber} 화자 '{line.SpeakerId}'가 캐릭터·외부 인물에 없다.");
                    }

                    if (!line.IsOption || IsContinuationOfGroup(lines, i)) continue;

                    var timeouts = 0;
                    foreach (var option in OptionGroup(lines, i)) if (option.Tags.Contains(DialogueTags.Timeout)) timeouts++;
                    if (timeouts > 1) errors.Add($"{node.Title}:{line.LineNumber} 선택지 묶음에 #timeout이 {timeouts}개 - 0~1개여야 한다.");
                }
            }
            return errors;
        }

        // 앞쪽에 같은 들여쓰기의 선택지가 있고 그 사이가 전부 더 깊은 줄(선택지 본문)이면 같은 묶음의 이어지는 선택지다.
        private static bool IsContinuationOfGroup(List<YarnScriptLine> lines, int index)
        {
            var indent = lines[index].Indent;
            for (var j = index - 1; j >= 0; j--)
            {
                if (lines[j].Indent > indent) continue;
                return lines[j].Indent == indent && lines[j].IsOption;
            }
            return false;
        }

        private static IEnumerable<YarnScriptLine> OptionGroup(List<YarnScriptLine> lines, int start)
        {
            var indent = lines[start].Indent;
            for (var j = start; j < lines.Count; j++)
            {
                if (lines[j].Indent > indent) continue;
                if (lines[j].Indent < indent || !lines[j].IsOption) yield break;
                yield return lines[j];
            }
        }
    }
}
