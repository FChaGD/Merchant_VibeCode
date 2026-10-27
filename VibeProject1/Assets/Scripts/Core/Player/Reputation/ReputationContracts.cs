using System;

namespace Game.Core
{
    /// <summary>평판 조회(기획 23 §3.2, Docs/설계/107번 §10) - HUD·게시판 수락 조건용. 증감 권한은 주지 않는다(ISP).</summary>
    public interface IReputationReader
    {
        int Value { get; }
        int Min { get; }
        int Max { get; }
        event Action Changed;
    }

    /// <summary>평판 증감 - 퀘스트 보상·불이익용. 범위로 자른 뒤 실제 변화량을 돌려준다.</summary>
    public interface IReputationAdjuster : IReputationReader
    {
        int Adjust(int delta);
    }
}
