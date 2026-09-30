using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>
    /// FormationGridEditor가 드롭/드래그/제거 이벤트를 감지한 뒤 "실제로 무엇을 반영할지"를 위임하는
    /// 정책 인터페이스 - Hub(로컬 편집+Apply 버튼)와 Field(즉시 반영+배치 시간)가 이 인터페이스의
    /// 구현체만 다르다(Docs/기획/20번 §3.1, 설계 25번 §2.2). "배경 진행 활동"(배치/이동 중) 관련
    /// 멤버는 Field에만 있는 개념이라 IFormationActivityHandler로 분리했다(ISP,
    /// Docs/Refactor/2026-09-08_공통.md §6.3 수정 G) - Hub(HubFormationPanel)는 이 인터페이스만
    /// 구현하고 IFormationActivityHandler는 구현하지 않는다.
    /// 편집 메서드는 반영 여부를 돌려준다 - 마차 중심 대열 규칙(Docs/기획/59번, FormationAreaRules)에 막히면 false이고, 편집기가 해당 칸을
    /// 붉게 깜빡인다(설계 60번 §11-5).
    /// </summary>
    internal interface IFormationEditingHandler
    {
        // FormationGridEditor가 렌더링에 쓸 "현재 실제 반영 상태" 스냅샷을 요청한다 - Hub는 로컬
        // 편집 중인 사본을, Field는 IFormationRepository의 현재 값을 그대로 돌려준다.
        FormationLayout GetDisplayLayout();

        // 대열 영역 계산에 더할 디버그 핀(Docs/기획/59번 §4.5). 없으면 null.
        IReadOnlyList<FormationAreaPin> GetAreaPins();

        // 격자 표시 범위의 여백(대열 경계 상자 바깥 칸 수, 설계 60번 §15.3). 마을은 마차 자유 배치라 연결 가능한 칸(가장 큰 마차 반경 + 1)까지
        // 보여야 하고, 상행 중은 대열 안에만 놓으므로 2칸.
        int GetVisibleMarginCells();

        // 팔레트에 이 유닛을 보일지 - 상행 중에는 마차를 새로 놓을 수 없어 팔레트에서 뺀다(2026-09-29 사용자 결정).
        bool ShowsInPalette(IFormationUnit unit);

        // 팔레트에서 빈/점유 슬롯으로 드롭(신규 배치 시도).
        bool HandlePaletteDrop(IFormationUnit unit, int targetSlotIndex);

        // 그리드 내 슬롯→슬롯 이동(드래그).
        bool HandleGridMove(string unitId, int originSlotIndex, int targetSlotIndex);

        // 그리드 밖으로 드래그해 배치 취소, 혹은 명시적 제거.
        bool HandleRemove(string unitId, int slotIndex);

#if UNITY_EDITOR
        // 디버그 핀(Docs/기획/59번 §4.5). 추가는 판 어디든 허용, 제거는 마차 연결을 끊으면 거부. 디버그 도구를 걷어낼 때 이 블록과 구현부의
        // #if UNITY_EDITOR 블록을 함께 지운다.
        // 핀 저장소가 설치돼 있는지 - 없으면 편집기가 디버그 패널을 숨긴다(2026-09-29 사용자 결정, 설계 60번 §8 개정).
        bool HasDebugPinStore { get; }
        void HandleDebugPinAdd(int slotIndex, FormationAreaShape shape);
        bool HandleDebugPinRemove(int slotIndex);
#endif
    }
}
