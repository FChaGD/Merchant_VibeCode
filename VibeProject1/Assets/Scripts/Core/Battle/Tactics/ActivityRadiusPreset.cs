namespace Game.Core
{
    /// <summary>
    /// 파티 전체 축 - "이동 가능 영역"의 크기. 기준점이 값마다 다르다(Docs/기획/12번 §2.2):
    /// Fixed는 유닛 각자의 배치 슬롯 위치, Standard는 대형 전체의 중심.
    /// </summary>
    public enum ActivityRadiusPreset
    {
        // 유닛 각자의 배치 슬롯 위치 기준 반경 4m(TacticsTuning.FixedRadiusMeters).
        Fixed,
        // 대형 중심 기준, 대형 중심→모서리 사선거리 + 5m(기본값).
        Standard,
        // 전장 전체(사실상 무제한).
        Wide,
    }
}
