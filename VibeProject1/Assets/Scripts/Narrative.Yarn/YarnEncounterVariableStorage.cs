#nullable enable
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Yarn.Unity;

namespace Game.Core
{
    /// <summary>
    /// Yarn 변수 저장소를 이력 위에 구현한다(설계 87 §4.2). 참/거짓 변수(`$helped_cart`)는 플래그(`helped_cart`)와 같은 값이다 - 조건 열의 flag와
    /// 대본의 변수가 한 원천을 보게 하기 위해서다. 숫자·문자열은 스크립트 변수로만 둔다. 스마트 변수는 기반 클래스가 처리한다.
    /// </summary>
    public class YarnEncounterVariableStorage : VariableStorageBehaviour
    {
        private IEncounterHistoryReader? reader;
        private IEncounterHistoryWriter? writer;
        private IEncounterScriptVariableStore? scripts;

        public void Bind(IEncounterHistoryReader? reader, IEncounterHistoryWriter? writer, IEncounterScriptVariableStore? scripts)
        {
            this.reader = reader;
            this.writer = writer;
            this.scripts = scripts;
        }

        private static string FlagOf(string variableName) => variableName.StartsWith("$") ? variableName.Substring(1) : variableName;

        public override bool TryGetValue<T>(string variableName, [NotNullWhen(true)] out T? result) where T : default
        {
            // false로 바꾼 플래그도 값으로 돌려준다 - 없다고 하면 Yarn이 선언 기본값(true일 수 있음)을 쓴다.
            if (typeof(T) == typeof(bool) || typeof(T) == typeof(object))
            {
                if (reader != null && reader.TryGetFlag(FlagOf(variableName), out var flag))
                {
                    result = (T)(object)flag;
                    return true;
                }
            }
            if (scripts != null && scripts.TryGetScriptValue(variableName, out var value) && value is T typed)
            {
                result = typed;
                return true;
            }
            result = default;
            return false;
        }

        public override void SetValue(string variableName, string stringValue) => scripts?.SetScriptValue(variableName, stringValue);
        public override void SetValue(string variableName, float floatValue) => scripts?.SetScriptValue(variableName, floatValue);
        public override void SetValue(string variableName, bool boolValue) => writer?.SetFlag(FlagOf(variableName), boolValue);

        public override void Clear() => writer?.ResetAll();

        public override bool Contains(string variableName)
            => (reader != null && reader.TryGetFlag(FlagOf(variableName), out _)) || (scripts != null && scripts.TryGetScriptValue(variableName, out _));

        public override void SetAllVariables(Dictionary<string, float> floats, Dictionary<string, string> strings, Dictionary<string, bool> bools, bool clear = true)
        {
            if (clear) Clear();
            foreach (var kv in floats) SetValue(kv.Key, kv.Value);
            foreach (var kv in strings) SetValue(kv.Key, kv.Value);
            foreach (var kv in bools) SetValue(kv.Key, kv.Value);
        }

        public override (Dictionary<string, float> FloatVariables, Dictionary<string, string> StringVariables, Dictionary<string, bool> BoolVariables) GetAllVariables()
        {
            var floats = new Dictionary<string, float>();
            var strings = new Dictionary<string, string>();
            var bools = new Dictionary<string, bool>();
            if (scripts != null)
            {
                foreach (var kv in scripts.ScriptValues)
                {
                    if (kv.Value is float f) floats[kv.Key] = f;
                    else if (kv.Value is string s) strings[kv.Key] = s;
                }
            }
            if (reader != null) foreach (var flag in reader.ActiveFlags) bools["$" + flag] = true;
            return (floats, strings, bools);
        }
    }
}
