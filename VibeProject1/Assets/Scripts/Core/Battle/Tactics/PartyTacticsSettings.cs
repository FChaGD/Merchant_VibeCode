namespace Game.Core
{
    /// <summary>
    /// 상행 전체(파티) 단위의 방향성 지시 3축. 항상 정확히 1개 값을 가진다(Docs/기획/12번 §2).
    /// </summary>
    public readonly struct PartyTacticsSettings
    {
        public string RecognitionType { get; }
        public string RadiusPreset { get; }
        public string Pursuit { get; }

        public PartyTacticsSettings(string recognitionType, string radiusPreset, string pursuit)
        {
            RecognitionType = recognitionType;
            RadiusPreset = radiusPreset;
            Pursuit = pursuit;
        }
    }
}
