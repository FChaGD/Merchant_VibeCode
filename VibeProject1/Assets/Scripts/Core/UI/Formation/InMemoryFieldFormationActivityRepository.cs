using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// IFieldFormationActivityRepository의 인메모리 구현(Docs/설계/25번 §3.2). InMemoryFormationRepository와
    /// 같은 성격의 Bootstrap 상주 저장소이지만, 저것과 달리 Update()에서 매 프레임 진행률을 스스로
    /// 갱신한다 - 정비창 UI가 닫혀 있어도(Field 패널이 비활성 상태여도) 배치/이동이 계속 흐르게 하는
    /// 핵심 지점(기획 20번 §1 전제 2). unitId 기준 조회는 activitiesByUnitId(Dictionary)로 O(1) 처리하고,
    /// activities(List)는 Update()의 역순 순회/제거와 ActiveActivities 노출 순서를 위해 병행 유지한다
    /// (리팩토링 점검 2026-09-08 §2-1 - 예전엔 List만 있어 unitId 조회가 전부 LINQ 선형 탐색이었다).
    /// </summary>
    public class InMemoryFieldFormationActivityRepository : MonoBehaviour, IFieldFormationActivityRepository, IManagedComponent
    {
        private readonly List<FormationActivity> activities = new();
        private readonly Dictionary<string, FormationActivity> activitiesByUnitId = new();
        private IFormationRepository formationRepository;

        public event Action<FormationActivity> OnActivityCompleted;
        public event Action<string> OnActivityCancelled;

        public IReadOnlyList<FormationActivity> ActiveActivities => activities;

        public void RegisterSelf(IDependencyRegistrar registrar)
        {
            registrar.Register<IFieldFormationActivityRepository>(this);
        }

        public void ResolveDependencies(IDependencyResolver registrar)
        {
            registrar.TryResolve<IFormationRepository>(out formationRepository);
        }

        public bool TryGetActivity(string unitId, out FormationActivity activity) => activitiesByUnitId.TryGetValue(unitId, out activity);

        public bool IsSlotReserved(int slotIndex) => activities.Any(a => a.TargetSlotIndex == slotIndex);
        public bool IsUnitBusy(string unitId) => activitiesByUnitId.ContainsKey(unitId);

        public void BeginAdd(string unitId, int targetSlotIndex, float requiredSeconds)
        {
            Add(new FormationActivity(unitId, FormationActivityKind.Adding, targetSlotIndex, FormationActivity.NoSlot, Array.Empty<int>(), requiredSeconds));
        }

        public void BeginMove(string unitId, int originSlotIndex, int targetSlotIndex, IReadOnlyList<int> pathSlotIndices, float requiredSeconds)
        {
            Add(new FormationActivity(unitId, FormationActivityKind.Moving, targetSlotIndex, originSlotIndex, pathSlotIndices, requiredSeconds));
        }

        // Cancel+BeginMove를 쓰지 않는다 - BeginMove는 항상 ElapsedSeconds=0으로 시작해 이미 지나온
        // 구간의 경과 시간을 이어받을 수 없다(기획 21번, 설계 26번 §3). ElapsedSeconds는 internal set이라
        // 같은 어셈블리인 여기서 생성 직후 대입할 수 있어 별도 생성자 오버로드가 필요 없다.
        public void RedirectMove(string unitId, int newTargetSlotIndex, IReadOnlyList<int> pathSlotIndices, float requiredSeconds, float elapsedSeconds, int partialSegmentIndex, float partialSegmentWeight)
        {
            if (!activitiesByUnitId.TryGetValue(unitId, out var current) || current.Kind != FormationActivityKind.Moving) return;

            RemoveFromCollections(current);
            var activity = new FormationActivity(unitId, FormationActivityKind.Moving, newTargetSlotIndex, current.OriginSlotIndex, pathSlotIndices, requiredSeconds)
            {
                ElapsedSeconds = elapsedSeconds,
                PartialSegmentIndex = partialSegmentIndex,
                PartialSegmentWeight = partialSegmentWeight
            };
            Add(activity);
        }

        public void Cancel(string unitId)
        {
            if (!activitiesByUnitId.TryGetValue(unitId, out var activity)) return;

            RemoveFromCollections(activity);
            OnActivityCancelled?.Invoke(unitId);
        }

        private void Add(FormationActivity activity)
        {
            activities.Add(activity);
            activitiesByUnitId[activity.UnitId] = activity;
        }

        private void RemoveFromCollections(FormationActivity activity)
        {
            activities.Remove(activity);
            activitiesByUnitId.Remove(activity.UnitId);
        }

        public void PauseAll()
        {
            foreach (var activity in activities) activity.IsPaused = true;
        }

        public void ResumeAdding()
        {
            foreach (var activity in activities)
            {
                if (activity.Kind == FormationActivityKind.Adding) activity.IsPaused = false;
            }
        }

        public void ResumeAll()
        {
            foreach (var activity in activities) activity.IsPaused = false;
        }

        public void ForceCompleteAll()
        {
            // Complete()가 리스트에서 제거하므로 스냅샷을 먼저 떠서 순회한다.
            foreach (var activity in activities.ToList())
            {
                Complete(activity);
            }
        }

        private void Update()
        {
            // 역순 순회 - Complete()가 즉시 리스트에서 제거해도 다음 인덱스가 안 밀린다.
            for (var i = activities.Count - 1; i >= 0; i--)
            {
                var activity = activities[i];
                if (activity.IsPaused) continue;

                activity.ElapsedSeconds += Time.deltaTime;
                if (activity.ElapsedSeconds >= activity.RequiredSeconds)
                {
                    Complete(activity);
                }
            }
        }

        private void Complete(FormationActivity activity)
        {
            RemoveFromCollections(activity);
            ApplyToLayout(activity);
            OnActivityCompleted?.Invoke(activity);
        }

        // Adding: 대상 슬롯에 기록. Moving: 출발 슬롯을 비우고 대상 슬롯에 기록 - 실제 FormationLayout은
        // 이동 도중 내내 "출발 슬롯에 그대로 있음"으로 취급된다(설계 25번 §3.2 - 시각적 유령 표시는
        // 별도 오버레이일 뿐 진짜 배치 데이터가 아니다).
        private void ApplyToLayout(FormationActivity activity)
        {
            if (formationRepository == null) return;

            // Hub에서 "적용" 버튼을 한 번도 누르지 않은 상태(TryLoadCurrent 실패)에서 Field가 첫
            // 배치를 완료하는 경우 - 반영할 대상 자체가 없어 조용히 무시되던 버그(실전 확인, 2026-09-05:
            // 타이머/화면 표시는 정상인데 실제로 대열에 배치되지 않아 전투가 아군 0명으로 시작함).
            // 이 경우 최소한의 새 레이아웃을 만들어서라도 반영한다.
            if (!formationRepository.TryLoadCurrent(out var layout))
            {
                layout = new FormationLayout(FormationLayout.DefaultColumnCount, FormationLayout.DefaultRowCount);
            }

            // 출발 슬롯은 "지금도 내가 차지하고 있을 때만" 비운다 - 맞바꾸기(Swap) 이동에서 상대
            // 유닛이 먼저 완료돼 내 출발 슬롯(=상대의 도착 슬롯)을 이미 차지한 경우, 무조건 비우면
            // 방금 도착한 상대를 지워버리는 버그(실전 검증 2026-09-07)가 있었다.
            if (activity.Kind == FormationActivityKind.Moving && layout.GetUnitId(activity.OriginSlotIndex) == activity.UnitId)
            {
                layout.Clear(activity.OriginSlotIndex);
            }
            layout.SetUnitId(activity.TargetSlotIndex, activity.UnitId);
            formationRepository.Apply(layout);
        }
    }
}
