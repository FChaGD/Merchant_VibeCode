using System;

namespace Game.Core
{
    /// <summary>Field 화면 가림 원인(설계 89 §13) - 지금은 이벤트 하나. 다른 전체화면 연출이 생기면 원인을 더한다(소비자는 바뀌지 않는다).</summary>
    public enum FieldUIHideReason { Event }

    /// <summary>가림 걸기·풀기 - 이벤트 진행자만 쓴다. 읽기 계약과 나눈 이유: 읽기만 하는 소비자(가림 적용·디버그)에게 조작을 노출하지 않기 위해서다(ISP).</summary>
    public interface IFieldUIHideControl
    {
        void Acquire(FieldUIHideReason reason);
        void Release(FieldUIHideReason reason);
    }

    /// <summary>가림 여부 조회 - 이동 뷰 가림 규칙(FieldUIWiring → LayerHideGate)과 Field 디버그 창이 쓴다. Changed = 가림 여부가 바뀌었다.</summary>
    public interface IFieldUIHideReader
    {
        bool IsHidden { get; }
        event Action Changed;
    }
}
