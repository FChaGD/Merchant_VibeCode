using System;
using System.Collections.Generic;

namespace Game.Core
{
    public sealed class YarnScriptLine
    {
        public int LineNumber;
        /// <summary>앞쪽 공백 수 - 선택지 묶음(같은 들여쓰기의 연속 선택지)과 선택지 본문(더 깊은 줄)을 가른다.</summary>
        public int Indent;
        public string SpeakerId = string.Empty;
        public string Text = string.Empty;
        public List<string> Tags = new();
        public bool IsOption;
        public bool IsCommand;
        public string CommandName = string.Empty;
    }

    public sealed class YarnScriptNode
    {
        public string Title = string.Empty;
        public List<YarnScriptLine> Lines = new();
    }

    /// <summary>
    /// .yarn 텍스트를 노드·줄 구조로 읽는 가벼운 스캐너(설계 87 §9). Yarn 컴파일러에 의존하지 않아 EditMode 콘텐츠 검증(화자·태그·명령 규칙)을
    /// Game.Core 테스트에서 돌릴 수 있다. 문법 전체가 아니라 검증에 필요한 것만 본다: 노드 제목, 들여쓰기, 화자 접두(`id:`), `#태그`, 선택지(`->`),
    /// 명령(`<<x>>`). 선택지 줄의 `<<if ...>>`는 조건이라 명령으로 세지 않는다.
    /// </summary>
    public static class YarnSourceScanner
    {
        public static List<YarnScriptNode> Scan(string source)
        {
            var nodes = new List<YarnScriptNode>();
            if (string.IsNullOrEmpty(source)) return nodes;

            YarnScriptNode current = null;
            var inBody = false;
            var rawLines = source.Replace("\r\n", "\n").Split('\n');
            for (var i = 0; i < rawLines.Length; i++)
            {
                var raw = rawLines[i];
                var line = raw.Trim();
                if (!inBody)
                {
                    if (line.StartsWith("title:", StringComparison.Ordinal))
                    {
                        current = new YarnScriptNode { Title = line.Substring(6).Trim() };
                    }
                    else if (line == "---" && current != null)
                    {
                        inBody = true;
                    }
                    continue;
                }

                if (line == "===")
                {
                    nodes.Add(current);
                    current = null;
                    inBody = false;
                    continue;
                }
                if (line.Length == 0 || line.StartsWith("//", StringComparison.Ordinal)) continue;

                var parsed = ParseLine(line, i + 1);
                parsed.Indent = CountIndent(raw);
                current.Lines.Add(parsed);
            }
            return nodes;
        }

        // 탭은 공백 4칸으로 센다(Yarn 편집기 기본).
        private static int CountIndent(string raw)
        {
            var indent = 0;
            foreach (var c in raw)
            {
                if (c == ' ') indent++;
                else if (c == '\t') indent += 4;
                else break;
            }
            return indent;
        }

        private static YarnScriptLine ParseLine(string line, int lineNumber)
        {
            var result = new YarnScriptLine { LineNumber = lineNumber };

            if (line.StartsWith("->", StringComparison.Ordinal))
            {
                result.IsOption = true;
                line = line.Substring(2).Trim();
            }
            else if (line.StartsWith("<<", StringComparison.Ordinal))
            {
                result.IsCommand = true;
                var end = line.IndexOf(">>", StringComparison.Ordinal);
                var body = (end > 2 ? line.Substring(2, end - 2) : line.Substring(2)).Trim();
                var space = body.IndexOf(' ');
                result.CommandName = space > 0 ? body.Substring(0, space) : body;
                return result;
            }

            var hash = line.IndexOf('#');
            var content = hash >= 0 ? line.Substring(0, hash) : line;
            if (hash >= 0)
            {
                foreach (var part in line.Substring(hash).Split('#'))
                {
                    var tag = part.Trim();
                    if (tag.Length > 0) result.Tags.Add(tag);
                }
            }

            // 선택지의 조건(`<<if ...>>`)은 본문에서 뗀다.
            var condition = content.IndexOf("<<", StringComparison.Ordinal);
            if (condition >= 0) content = content.Substring(0, condition);
            content = content.Trim();

            if (!result.IsOption)
            {
                var colon = content.IndexOf(':');
                if (colon > 0 && content.Substring(0, colon).IndexOf(' ') < 0)
                {
                    result.SpeakerId = content.Substring(0, colon).Trim();
                    content = content.Substring(colon + 1).Trim();
                }
            }
            result.Text = content;
            return result;
        }
    }
}
