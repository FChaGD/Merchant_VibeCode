namespace Game.Core
{
    /// <summary>
    /// 상단에서 사망으로 빠진 캐릭터 기록 읽기(Docs/설계/81번 §6.2). 고용 후보 제공자만 쓴다 - 로스터(현재 구성원)와 수명·소비자가 달라 분리했다.
    /// </summary>
    public interface IDeceasedCharacterReader
    {
        bool IsDeceased(string characterId);
    }

    /// <summary>사망 기록 추가 전용(ISP). 상행 종료 처리만 쓴다.</summary>
    public interface IDeceasedCharacterRecorder
    {
        void Record(string characterId);
    }
}
