using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>이력 조회(설계 87 §4.1). 조건 판정·후보 추첨·디버그 표시는 이 계약만 받는다 - 기록 권한을 넓히지 않기 위해 쓰기와 나눴다(ISP).</summary>
    public interface IEncounterHistoryReader
    {
        bool HasFlag(string flag);
        /// <summary>
        /// 한 번이라도 설정된 플래그면 true와 그 값. false로 설정한 플래그도 "설정됨"이다 - Yarn은 저장소에 값이 없으면 선언 기본값을 쓰므로,
        /// `<<declare $x = true>>` 변수를 false로 바꾼 뒤 기본값 true로 되돌아가지 않게 하기 위해서다. 조건 판정(flag)은 HasFlag만 본다.
        /// </summary>
        bool TryGetFlag(string flag, out bool value);
        /// <summary>게임 전체에서 끝까지 진행된 적이 있는지(1회성 판정·조건 seen).</summary>
        bool HasOccurred(string contentId);
        /// <summary>이번 상행에서 끝까지 진행됐는지(상행당 1회 판정).</summary>
        bool OccurredThisTrip(string contentId);
        IReadOnlyCollection<string> ActiveFlags { get; }
        IReadOnlyCollection<string> OccurredIds { get; }
        IReadOnlyCollection<string> TripOccurredIds { get; }
    }

    /// <summary>이력 기록. 대화·이벤트 진행자(88·89), Yarn 변수 저장소, 디버그 초기화만 쓴다.</summary>
    public interface IEncounterHistoryWriter
    {
        void SetFlag(string flag, bool value);
        /// <summary>끝까지 진행된 이벤트·대화만 기록한다 - 중단은 기록하지 않는다(설계 87 §4.1).</summary>
        void RecordCompleted(string contentId);
        void ResetTrip();
        void ResetAll();
    }

    /// <summary>
    /// 스크립트 안에서만 쓰는 숫자·문자열 변수(설계 87 §4.2). 조건 판정에는 쓰지 않는다 - Yarn 변수 저장소가 참/거짓은 플래그로,
    /// 그 밖은 여기로 보낸다.
    /// </summary>
    public interface IEncounterScriptVariableStore
    {
        bool TryGetScriptValue(string name, out object value);
        void SetScriptValue(string name, object value);
        IReadOnlyDictionary<string, object> ScriptValues { get; }
    }
}
