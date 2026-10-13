using System;

namespace Game.Core
{
    /// <summary>
    /// 발생 조건 1개(설계 87 §5.1). 엑셀 조건 열을 임포트할 때 한 번 해석해 자산에 직렬화한다 - 실행 중 문자열 파싱을 반복하지 않기 위해서다.
    /// </summary>
    [Serializable]
    public struct EncounterCondition
    {
        public string Key;
        public bool Negated;
        public string Argument;

        public EncounterCondition(string key, bool negated, string argument)
        {
            Key = key;
            Negated = negated;
            Argument = argument;
        }
    }
}
