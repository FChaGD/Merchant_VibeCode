using UnityEditor;

namespace Game.Core.Editor
{
    // 워크북 5개(Character/Enemy/Tactics/Trip 폴더, 설계 18번 §7, 20번 §6)를 한 번에 임포트하는
    // 편의 메뉴 - TableAutoImportOnPlay가 플레이 진입마다 호출하는 것과 같은 순서를 수동으로도
    // 실행할 수 있게 한다.
    public static class TableImportAll
    {
        [MenuItem("Tools/Game/Table/Import All")]
        public static void Import()
        {
            CharacterStatsTableImporter.Import();
            EnemyStatsTableImporter.Import();
            PartyPolicyTableImporter.Import();
            RoleGroupTacticsTableImporter.Import();
            TripCityMapTableImporter.Import();
        }
    }
}
