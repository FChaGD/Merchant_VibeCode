using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 파티 축 3개 + 역할군 축 3개 + 배치 슬롯 좌표를 유닛 1기 기준으로 해석/고정한 결과
    /// (Docs/설계/12번 §2). IUnitTacticsProfileResolver가 전투 시작 시 한 번만 만들어 각 아군
    /// BattleCharacterUnit 생성자에 주입한다 - 전투 도중 방향성 지시가 바뀌어도 이미 시작된
    /// 전투엔 반영되지 않는다(대형 크기 기반 fleeTravelDistance와 같은 이유로 의도된 설계).
    /// </summary>
    public readonly struct UnitTacticsProfile
    {
        public EnemyRecognitionType RecognitionType { get; }
        public ActivityRadiusPreset RadiusPreset { get; }
        public PursuitPreset Pursuit { get; }
        public TargetPriority TargetPriority { get; }
        public LocalPositioning Positioning { get; }
        public SelfPreservation SelfPreservation { get; }
        public Vector2 HomePosition { get; }

        public UnitTacticsProfile(
            EnemyRecognitionType recognitionType, ActivityRadiusPreset radiusPreset, PursuitPreset pursuit,
            TargetPriority targetPriority, LocalPositioning positioning, SelfPreservation selfPreservation,
            Vector2 homePosition)
        {
            RecognitionType = recognitionType;
            RadiusPreset = radiusPreset;
            Pursuit = pursuit;
            TargetPriority = targetPriority;
            Positioning = positioning;
            SelfPreservation = selfPreservation;
            HomePosition = homePosition;
        }
    }
}
