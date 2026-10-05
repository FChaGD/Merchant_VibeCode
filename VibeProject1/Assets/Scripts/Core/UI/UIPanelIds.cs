namespace Game.Core
{
    /// <summary>
    /// UIManager.Open/Close에 전달하는 패널 식별자 상수.
    /// </summary>
    public static class UIPanelIds
    {
        public const string Formation = "Formation";
        public const string Trip = "Trip";
        public const string Tactics = "Tactics";
        /// <summary>전투 후 회수 적재 패널(Field, 설계 79번 §7).</summary>
        public const string CargoRecovery = "CargoRecovery";

        /// <summary>마을 카테고리 depth(TownCategoryPanel) - 카테고리마다 별도 패널이라 Id를 조립한다.</summary>
        public static string TownCategory(string categoryId) => $"Town.{categoryId}";

        /// <summary>마을 시설 화면 - 시설 Id(TownFacilityIds)로 조립한다(Docs/설계/50번 §6.1).</summary>
        public static string Facility(string facilityId) => $"Facility.{facilityId}";
    }
}
