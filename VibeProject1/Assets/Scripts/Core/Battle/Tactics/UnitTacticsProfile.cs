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
        // 방진 형성 로직(Docs/설계/12번 §12.3)이 "이 아군이 보호대상 후보군(RangedDealer/Supporter)에
        // 속하는지" 판정해야 해서 추가 - 역할군 자체는 이미 override 조회에 쓰였지만 결과 값이
        // 프로필 밖으로 나가지 않았었다.
        public RoleGroup RoleGroup { get; }

        public UnitTacticsProfile(
            EnemyRecognitionType recognitionType, ActivityRadiusPreset radiusPreset, PursuitPreset pursuit,
            TargetPriority targetPriority, LocalPositioning positioning, SelfPreservation selfPreservation,
            Vector2 homePosition, RoleGroup roleGroup)
        {
            RecognitionType = recognitionType;
            RadiusPreset = radiusPreset;
            Pursuit = pursuit;
            TargetPriority = targetPriority;
            Positioning = positioning;
            SelfPreservation = selfPreservation;
            HomePosition = homePosition;
            RoleGroup = roleGroup;
        }
    }
}
