using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    public enum FormationActivityKind { Adding, Moving }

    /// <summary>
    /// Field에서 진행 중인 배치/이동 행동 하나(Docs/설계/25번 §3.1). Adding은 OriginSlotIndex(-1)/
    /// PathSlotIndices(빈 리스트)가 의미 없다. RequiredSeconds는 Adding=소요시간 그대로, Moving=경로
    /// 길이/이동 속도로 환산한 값 - 두 종류를 같은 ElapsedSeconds/RequiredSeconds 진행률로 다뤄
    /// 일시정지/재개/강제완료 로직을 하나로 공유한다(기획 20번 §3.2/§3.3 대칭 구조).
    /// </summary>
    public sealed class FormationActivity
    {
        public const int NoSlot = -1;

        public string UnitId { get; }
        public FormationActivityKind Kind { get; }
        public int TargetSlotIndex { get; }
        public int OriginSlotIndex { get; }
        public IReadOnlyList<int> PathSlotIndices { get; }
        public float RequiredSeconds { get; }
        public float ElapsedSeconds { get; internal set; }
        public bool IsPaused { get; internal set; }

        // 도착 고스트로 목적지를 중도 수정한 직후에만 유효(그 외에는 기본값 -1/1 = "해당 없음",
        // 기획 21번, 설계 26번 §2). PathSlotIndices[PartialSegmentIndex]→[+1] 구간만 일반 구간(가중치
        // 1) 대비 PartialSegmentWeight만큼만 소요시간을 차지한다 - 리다이렉트 순간 이미 그 구간을
        // 일부 지나온 상태이기 때문. 이 구간의 실제 시작 좌표(그리드 노드가 아닌 연속 좌표)는
        // 좌표계를 모르는 이 클래스가 갖지 않고, 소비자가 FormationPathInterpolation.ApplyPartialSegment로
        // 직접 계산한다.
        public int PartialSegmentIndex { get; internal set; } = -1;
        public float PartialSegmentWeight { get; internal set; } = 1f;

        public float Progress01 => RequiredSeconds <= 0f ? 1f : Mathf.Clamp01(ElapsedSeconds / RequiredSeconds);

        internal FormationActivity(string unitId, FormationActivityKind kind, int targetSlotIndex, int originSlotIndex, IReadOnlyList<int> pathSlotIndices, float requiredSeconds)
        {
            UnitId = unitId;
            Kind = kind;
            TargetSlotIndex = targetSlotIndex;
            OriginSlotIndex = originSlotIndex;
            PathSlotIndices = pathSlotIndices;
            RequiredSeconds = requiredSeconds;
        }
    }
}
