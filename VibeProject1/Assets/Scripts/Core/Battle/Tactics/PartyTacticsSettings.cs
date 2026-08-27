namespace Game.Core
{
    /// <summary>
    /// 상행 전체(파티) 단위의 방향성 지시 3축. 항상 정확히 1개 값을 가진다(Docs/기획/12번 §2).
    /// </summary>
    public readonly struct PartyTacticsSettings
    {
        public EnemyRecognitionType RecognitionType { get; }
        public ActivityRadiusPreset RadiusPreset { get; }
        public PursuitPreset Pursuit { get; }

        public PartyTacticsSettings(EnemyRecognitionType recognitionType, ActivityRadiusPreset radiusPreset, PursuitPreset pursuit)
        {
            RecognitionType = recognitionType;
            RadiusPreset = radiusPreset;
            Pursuit = pursuit;
        }
    }
}
