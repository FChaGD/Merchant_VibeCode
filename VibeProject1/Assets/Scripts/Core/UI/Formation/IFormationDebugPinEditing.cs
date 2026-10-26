#if UNITY_EDITOR
namespace Game.Core
{
    /// <summary>
    /// 정비창 디버그 핀 편집(Docs/기획/59번 §4.5). 게임 편집 정책 계약(IFormationEditingHandler)에 섞여 있던 디버그 조작을 떼어 냈다
    /// (Docs/Refactor/2026-10-26_공통.md C-2) - 편집기는 정책이 이 계약을 구현했을 때만 디버그 패널을 쓴다. 디버그 도구를 걷어낼 때
    /// 이 파일과 구현부의 #if UNITY_EDITOR 블록을 함께 지운다.
    /// </summary>
    internal interface IFormationDebugPinEditing
    {
        // 핀 저장소가 설치돼 있는지 - 없으면 편집기가 디버그 패널을 숨긴다(2026-09-29 사용자 결정, 설계 60번 §8 개정).
        bool HasDebugPinStore { get; }
        // 추가는 판 어디든 허용, 제거는 마차 연결을 끊으면 거부.
        void HandleDebugPinAdd(int slotIndex, FormationAreaShape shape);
        bool HandleDebugPinRemove(int slotIndex);
    }
}
#endif
