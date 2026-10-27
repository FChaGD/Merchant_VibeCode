namespace Game.Core
{
    /// <summary>
    /// 상행 종료(마을 도착·궤주 귀환) - 사망 캐릭터 영구 제거 + 전원 상태 초기화 + 종료 신호(Docs/설계/81번 §6.4). 결과 정리 마무리 단계가 궤주 귀환 때 부른다.
    /// 예전엔 이동 화면 조율자 계약에 들어 있어 소비자가 화면 등록 메서드까지 받았다(Docs/Refactor/2026-10-26_씬전용.md SS-2).
    /// </summary>
    public interface ITripFinisher
    {
        void FinishTrip();
    }
}
