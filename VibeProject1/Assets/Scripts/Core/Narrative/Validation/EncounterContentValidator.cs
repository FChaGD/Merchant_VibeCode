using System;
using System.Globalization;
using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>
    /// 콘텐츠 정합성 검사(설계 87 §9). 테이블: YarnNode 존재. 대본: 화자 Id 존재, 선택지 묶음마다 `#timeout` 0~1개. 선택지 묶음 = 같은 들여쓰기의 연속
    /// 선택지(사이의 더 깊은 줄은 선택지 본문). 선택지 앞 질문 줄의 `#ask`는 요구하지 않는다 - Yarn이 그 줄에 lastline을 자동으로 붙인다.
    /// 좌우 자리(88 §5.3): 화자마다 첫 대사에 `#left`/`#right`, 한 노드에서 같은 자리를 두 화자가 쓰지 않는다.
    /// 이벤트 대본(89 §5.4): 명령 이름·인자, 품목·캐릭터·인물 Id, 선택지 묶음마다 `#choice` 줄.
    /// </summary>
    public static class EncounterContentValidator
    {
        // Yarn 내장 명령 - 이벤트 효과 명령이 아니어도 허용한다.
        private static readonly HashSet<string> YarnBuiltIns = new() { "set", "declare", "if", "elseif", "else", "endif", "jump", "detour", "return", "wait", "stop", "once", "endonce" };

        public static List<string> ValidateTable(IReadOnlyList<EventEntry> events, IReadOnlyList<DialogueEntry> dialogues, Func<string, bool> nodeExists)
        {
            var errors = new List<string>();
            foreach (var e in events)
            {
                if (!nodeExists(e.YarnNode)) errors.Add($"이벤트 '{e.Id}': Yarn 노드 '{e.YarnNode}'가 없다.");
                // 도로 난이도 사건 Id(`이벤트Id-MMDD-순번`, 설계 89 §5.2)가 겹치지 않게 - 구분자와 예약 접두를 막는다. 향후 Id로 항목 하나를 지울 때 Id가 하나뿐이어야 한다.
                if (e.Id != null && e.Id.Contains('-')) errors.Add($"이벤트 '{e.Id}': 이벤트 Id에 '-'를 쓸 수 없다(사건 Id 구분자).");
                else if (CityPairDifficultyIds.IsReserved(e.Id)) errors.Add($"이벤트 '{e.Id}': 예약된 사건 Id 접두라 이벤트 Id로 쓸 수 없다.");
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
                ValidateSides(node, errors);
            }
            return errors;
        }

        // 화자 자리(설계 88 §5.3). 줄 순서(파일 순서)로 첫 대사를 본다 - 선택지 본문 안의 첫 대사도 같다.
        private static void ValidateSides(YarnScriptNode node, List<string> errors)
        {
            var sideOf = new Dictionary<string, string>();
            foreach (var line in node.Lines)
            {
                if (line.IsOption || line.IsCommand || line.SpeakerId.Length == 0 || sideOf.ContainsKey(line.SpeakerId)) continue;

                var side = line.Tags.Contains(DialogueTags.Left) ? DialogueTags.Left
                    : line.Tags.Contains(DialogueTags.Right) ? DialogueTags.Right
                    : null;
                if (side == null)
                {
                    errors.Add($"{node.Title}:{line.LineNumber} 화자 '{line.SpeakerId}'의 첫 대사에 #left 또는 #right가 없다.");
                    sideOf[line.SpeakerId] = string.Empty;
                    continue;
                }

                foreach (var pair in sideOf)
                {
                    if (pair.Value != side) continue;
                    errors.Add($"{node.Title}:{line.LineNumber} 화자 '{line.SpeakerId}'가 '{pair.Key}'와 같은 자리(#{side})를 쓴다.");
                    break;
                }
                sideOf[line.SpeakerId] = side;
            }
        }

        /// <summary>
        /// 이벤트 대본 검사(설계 89 §5.1·§5.4). 이벤트 노드(테이블 YarnNode)만 본다. 명령 이름·인자 개수·숫자 형식, 품목·캐릭터 Id 존재, `#people` 인물 존재,
        /// 선택지 묶음마다 바로 앞(명령 줄 건너뜀)에 `#choice` 줄. 효과 실행기의 "건너뛰고 계속" 규칙과 별개로, 작성 실수를 Play 전에 잡기 위해서다.
        /// </summary>
        public static List<string> ValidateEventScripts(IReadOnlyList<YarnScriptNode> nodes, ICollection<string> eventNodeTitles, EventScriptRules rules)
        {
            var errors = new List<string>();
            foreach (var node in nodes)
            {
                if (!eventNodeTitles.Contains(node.Title)) continue;
                var lines = node.Lines;
                for (var i = 0; i < lines.Count; i++)
                {
                    var line = lines[i];
                    var at = $"{node.Title}:{line.LineNumber}";
                    if (line.IsCommand) ValidateCommand(line, at, rules, errors);
                    if (line.Tags.Count > 0) ValidatePeople(line, at, rules, errors);
                    if (line.IsOption && !IsContinuationOfGroup(lines, i) && !HasChoiceLineBefore(lines, i))
                    {
                        errors.Add($"{at} 선택지 묶음 바로 앞에 #choice 줄(선택 화면 설명문)이 없다.");
                    }
                }
            }
            return errors;
        }

        private static void ValidateCommand(YarnScriptLine line, string at, EventScriptRules rules, List<string> errors)
        {
            var name = line.CommandName;
            var args = line.CommandArgs;
            if (YarnBuiltIns.Contains(name)) return;
            // 이름·인자 종류는 명세 표(EventCommandSpecs, 설계 89 §5.6)를 따른다 - 명령 한 줄에 오류는 최대 1개.
            if (!EventCommandSpecs.TryGetCommand(name, out var spec))
            {
                errors.Add($"{at} 알 수 없는 명령 <<{name}>>.");
                return;
            }
            var exact = name == EventCommandSpecs.Difficulty && args.Count >= 2 ? EventCommandSpecs.DifficultyArgCount(args[1]) : -1;
            var countOk = args.Count >= spec.MinArgs && args.Count <= spec.Args.Count && (exact < 0 || args.Count == exact);
            if (!countOk || !ArgsValid(spec, args, rules)) errors.Add($"{at} {spec.Usage} 형식이 아니다(인자 개수·형식·Id 확인).");
        }

        private static bool ArgsValid(EventCommandSpec spec, List<string> args, EventScriptRules rules)
        {
            for (var i = 0; i < args.Count; i++)
            {
                if (!ArgValid(spec.Args[i], args[i], rules)) return false;
            }
            return true;
        }

        private static bool ArgValid(EventArgKind kind, string value, EventScriptRules rules) => kind switch
        {
            EventArgKind.Int => int.TryParse(value, out _),
            EventArgKind.PositiveInt => PositiveInt(value),
            EventArgKind.Seconds => float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out _),
            EventArgKind.ItemId => rules.ItemExists(value),
            EventArgKind.ItemIdOrRandom => value == EventCargoGoods.RandomTarget || rules.ItemExists(value),
            EventArgKind.CharacterId => rules.CharacterExists(value),
            EventArgKind.DifficultyMode => EventRoadDifficultyEffects.IsMode(value),
            _ => false,
        };

        private static void ValidatePeople(YarnScriptLine line, string at, EventScriptRules rules, List<string> errors)
        {
            if (!TagValue.TryGet(line.Tags, DialogueTags.People, out var list)) return;
            foreach (var raw in list.Split(','))
            {
                var id = raw.Trim();
                if (id.Length > 0 && !rules.SpeakerExists(id)) errors.Add($"{at} #people의 '{id}'가 캐릭터·외부 인물에 없다.");
            }
        }

        // 선택지 묶음 첫 줄 앞으로 명령 줄을 건너뛰고 처음 만나는 줄이 #choice 대사·서술 줄이어야 한다.
        private static bool HasChoiceLineBefore(List<YarnScriptLine> lines, int index)
        {
            for (var j = index - 1; j >= 0; j--)
            {
                if (lines[j].IsCommand) continue;
                return !lines[j].IsOption && lines[j].Tags.Contains(DialogueTags.Choice);
            }
            return false;
        }

        private static bool PositiveInt(string s) => int.TryParse(s, out var n) && n > 0;

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

namespace Game.Core
{
    /// <summary>이벤트 대본 검사에 필요한 Id 조회(설계 89 §5.4) - 테이블 자산에서 채운다.</summary>
    public sealed class EventScriptRules
    {
        public Func<string, bool> ItemExists = _ => false;
        public Func<string, bool> CharacterExists = _ => false;
        public Func<string, bool> SpeakerExists = _ => false;
    }
}
