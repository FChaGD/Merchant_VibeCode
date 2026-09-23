#if UNITY_EDITOR
namespace Game.Core
{
    /// <summary>
    /// ScriptableObject 테이블 자산 경로의 단일 소스. 각 임포터(Tools/Game/Table/Import ...)가 만드는
    /// 자산과, 그 자산을 배선하는 인스톨러(ManagerHierarchyInstaller, BattleTestSceneInstaller)가
    /// 각자 같은 경로 문자열을 최대 3중으로 중복 선언하던 문제를 해소한다
    /// (Docs/Refactor/2026-09-08_공통.md 확장성 문제점 1). 폴더명의 "ScriptableObejct" 오타는 자산
    /// 리네임(GUID/씬 참조 영향)이 필요한 별도 작업이라 이번엔 그대로 옮겨왔다.
    ///
    /// 소비처가 전부 Editor 전용 API(AssetDatabase.LoadAssetAtPath 등)뿐이라 원래 Game.Core.Editor
    /// 어셈블리에 있었으나, 런타임 어셈블리(Game.Core)의 TripCityMapPersistence(Core/Debug/Trip/)도
    /// 같은 경로를 참조해야 해서 Game.Core로 내려왔다 - asmdef 참조가 Game.Core.Editor→Game.Core
    /// 단방향이라 역방향 참조는 불가능하기 때문(Docs/Refactor/2026-08-26-리팩토링_점검_컨벤션.md 다음
    /// 라운드 후보). #if UNITY_EDITOR로 감싸 플레이어 빌드에는 포함되지 않게 한다.
    /// </summary>
    public static class TableAssetPaths
    {
        private const string Folder = "Assets/Prefabs/ScriptableObejct";

        public const string CharacterStatsTable = Folder + "/CharacterStatsTable.asset";
        public const string CharacterStringsTable = Folder + "/CharacterStringsTable.asset";
        public const string EnemyStatsTable = Folder + "/EnemyStatsTable.asset";
        public const string EnemyEncounterCompositionTable = Folder + "/EnemyEncounterCompositionTable.asset";
        public const string EnemyStringsTable = Folder + "/EnemyStringsTable.asset";
        public const string PartyPolicyCatalog = Folder + "/PartyTacticsPolicyCatalog.asset";
        public const string PartyPolicyStringsTable = Folder + "/PartyTacticsPolicyStringsTable.asset";
        public const string RoleGroupTacticsCatalog = Folder + "/RoleGroupTacticsCatalog.asset";
        public const string RoleGroupTacticsStringsTable = Folder + "/RoleGroupTacticsStringsTable.asset";
        public const string MercenaryRoleGroupMap = Folder + "/MercenaryRoleGroupMap.asset";
        public const string TripCityMap = Folder + "/TripCityMap.asset";
        public const string TripCityStringsTable = Folder + "/TripCityStringsTable.asset";

        // 아이템 카테고리 4종(Docs/설계/35번 §4/§8) - ItemDefinitionTableAsset/ItemStringTableAsset
        // 클래스는 카테고리 4개가 공유하고 경로만 따로 둔다.
        public const string TradeGoodsItemTable = Folder + "/TradeGoodsItemTable.asset";
        public const string TradeGoodsItemStrings = Folder + "/TradeGoodsItemStrings.asset";
        public const string EquipmentItemTable = Folder + "/EquipmentItemTable.asset";
        public const string EquipmentItemStrings = Folder + "/EquipmentItemStrings.asset";
        public const string ConsumableItemTable = Folder + "/ConsumableItemTable.asset";
        public const string ConsumableItemStrings = Folder + "/ConsumableItemStrings.asset";
        public const string PersonalItemItemTable = Folder + "/PersonalItemItemTable.asset";
        public const string PersonalItemItemStrings = Folder + "/PersonalItemItemStrings.asset";
    }
}
#endif
