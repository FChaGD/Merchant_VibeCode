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

        /// <summary>마을 카테고리 depth(TownCategoryPanel) - 카테고리마다 별도 패널이라 Id를 조립한다.</summary>
        public static string TownCategory(string categoryId) => $"Town.{categoryId}";
    }
}
