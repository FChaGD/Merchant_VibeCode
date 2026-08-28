namespace Game.Core
{
    /// <summary>
    /// 파티 전체 축 - "이동 가능 영역"의 크기. 기준점이 값마다 다르다(Docs/기획/12번 §2.2):
    /// FormationHold(대열 유지)는 유닛 각자의 배치 슬롯 위치, TripWide(상행 전체)는 대형 전체의 중심.
    /// </summary>
    public enum ActivityRadiusPreset
    {
        // 대열 유지 - 유닛 각자의 배치 슬롯 위치 기준 반경 4m(TacticsTuning.FixedRadiusMeters).
        FormationHold,
        // 상행 전체(기본값) - 대형 중심 기준, 대형 중심→모서리 사선거리 + 5m.
        TripWide,
        // 전장 전체 - 전장 경계까지(§2.2-1에서 실제 하드 캡으로 확정).
        FieldWide,
    }
}
