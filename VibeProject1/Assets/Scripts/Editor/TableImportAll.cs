using UnityEditor;

namespace Game.Core.Editor
{
    // 워크북(Character/Enemy/Tactics/Trip/Item/Caravan 폴더, 설계 18번 §7, 20번 §6, 35번 §5, 56번 §2.3)을 한 번에
    // 임포트하는 편의 메뉴 - TableAutoImportOnPlay가 플레이 진입마다 호출하는 것과 같은 순서를
    // 수동으로도 실행할 수 있게 한다. 아이템 카테고리 4종은 ItemTableImporter가 신설되면서
    // (2026-09-23) 이 목록에서 처음엔 누락됐었다 - 개별 메뉴로만 실행 가능해 "Import All"을 눌러도
    // 갱신되지 않는 상태였다. 아이템은 이후 공용 워크북과 조인하는 일괄 임포트 1개로 통합됐다(설계 50번 §4).
    public static class TableImportAll
    {
        [MenuItem("Tools/Game/Table/Import All")]
        public static void Import()
        {
            CharacterStatsTableImporter.Import();
            EnemyStatsTableImporter.Import();
            PartyPolicyTableImporter.Import();
            RoleGroupTacticsTableImporter.Import();
            CityTableImporter.Import();
            ItemTableImporter.Import();
            CaravanAssetTableImporter.Import();
        }
    }
}
