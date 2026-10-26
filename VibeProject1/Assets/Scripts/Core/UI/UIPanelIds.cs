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
        /// <summary>개인 골드 → 골드 상자 변환 모달(Hub, 설계 83번 §6.2).</summary>
        public const string GoldConversion = "GoldConversion";
        /// <summary>이벤트 대화 화면·선택 화면(Field, 설계 89 §6) - 정비창과 같은 모달 묶음.</summary>
        public const string EventDialogue = "EventDialogue";
        public const string EventChoice = "EventChoice";
        /// <summary>숙소 휴식 진행 화면(Hub, 설계 103번 §4.4) - 숙소와 같은 모달 채널이라 열면 숙소가 숨고 닫으면 숙소로 돌아간다.</summary>
        public const string InnRestProgress = "InnRestProgress";

        /// <summary>마을 카테고리 depth(TownCategoryPanel) - 카테고리마다 별도 패널이라 Id를 조립한다.</summary>
        public static string TownCategory(string categoryId) => $"Town.{categoryId}";

        /// <summary>마을 시설 화면 - 시설 Id(TownFacilityIds)로 조립한다(Docs/설계/50번 §6.1).</summary>
        public static string Facility(string facilityId) => $"Facility.{facilityId}";
    }
}
