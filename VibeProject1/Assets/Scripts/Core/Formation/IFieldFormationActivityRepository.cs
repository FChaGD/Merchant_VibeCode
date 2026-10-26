using System;
using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>
    /// Field(상행 중)에서 진행 중인 배치/이동 행동을 관리한다(Docs/기획/20번, 설계 25번 §3). 정비창
    /// UI 패널이 닫혀 있어도 계속 진행되어야 해서, 이 저장소는 UI 컴포넌트가 아니라 Bootstrap 상주
    /// 매니저(IManagedComponent)로 등록된다.
    /// </summary>
    public interface IFieldFormationActivityRepository
    {
        IReadOnlyList<FormationActivity> ActiveActivities { get; }
        bool TryGetActivity(string unitId, out FormationActivity activity);

        // 점유 판정(경로 회피, 팔레트 잔여수 계산 공통) - 아직 FormationLayout에는 기록되지 않았지만
        // 이미 "예약된" 슬롯/유닛이라 다른 액션의 대상이 될 수 없다.
        bool IsSlotReserved(int slotIndex);
        bool IsUnitBusy(string unitId);

        void BeginAdd(string unitId, int targetSlotIndex, float requiredSeconds);
        void BeginMove(string unitId, int originSlotIndex, int targetSlotIndex, IReadOnlyList<int> pathSlotIndices, float requiredSeconds);

        // 이동 중인 유닛의 목적지를 실시간으로 바꾼다(기획 21번, 설계 26번 §3) - Cancel+BeginMove와
        // 달리 elapsedSeconds를 그대로 이어받아 이미 진행된 시간을 잃지 않는다. OriginSlotIndex는
        // 건드리지 않는다(최초 출발점 유지). partialSegmentIndex/Weight는 FormationActivity의 같은
        // 이름 프로퍼티에 그대로 전달된다(연속 좌표 접합, §2). 대상 활동이 없거나 Moving이 아니면
        // 아무 것도 하지 않는다.
        void RedirectMove(string unitId, int newTargetSlotIndex, IReadOnlyList<int> pathSlotIndices, float requiredSeconds, float elapsedSeconds, int partialSegmentIndex, float partialSegmentWeight);

        // 기획 20번 §3.4 - 진행 중 제거. 즉시 취소, 로스터 복귀는 호출자(FieldFormationPanel)가
        // OnActivityCancelled를 받아 처리한다(이 저장소는 로스터를 모른다, SRP).
        void Cancel(string unitId);

        void PauseAll();       // 인카운터 발생 시(기획 20번 §3.2/§3.3 공통)
        void ResumeAdding();   // 전투 시작 시 - Adding만 재개
        void ResumeAll();      // 전투 종료 후 승리/도주 - 나머지(Moving)도 재개
        void ForceCompleteAll(); // 전투 종료 후 패배(도주 제외) - 전부 즉시 완료 처리

        // 정상/강제 완료 공통 - 구독자가 FormationLayout에 최종 반영한다.
        event Action<FormationActivity> OnActivityCompleted;
        event Action<string> OnActivityCancelled; // unitId
    }
}
