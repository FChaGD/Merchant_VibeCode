using UnityEditor;
using UnityEngine;

namespace Game.Core.Editor
{
    /// <summary>
    /// 엑셀 임포트 결과 표·카탈로그 자산을 소비자 컴포넌트의 직렬화 필드에 배선하는 공용 절차(Docs/Refactor/2026-10-26_씬전용.md H-1, SS-7).
    /// Bootstrap 인스톨러와 배틀 테스트 인스톨러가 같은 속성 이름·같은 자산 경로로 각자 복사하고 있었다 - 한쪽만 고치면 배틀 테스트만 조용히
    /// 폴백 동작했다. 표는 DI로 등록하지 않고 소비자마다 SerializeField로 배선한다(Docs/설계/17번 §1/§6). 대상은 속성 이름만 맞으면 되므로 Object로 받는다.
    /// </summary>
    internal static class EditorTableWiring
    {
        /// <summary>
        /// 전투 스탯 표 3종(캐릭터·적·적 구성) - 실전 시뮬레이션과 배틀 테스트 시뮬레이션. 에셋이 아직 임포트 전이면 null 배선을 경고 없이 허용한다 -
        /// 그 상태로 실행하면 표 조회 시점에 즉시 예외로 드러난다.
        /// </summary>
        public static void WireBattleStatTables(Object target)
        {
            var so = new SerializedObject(target);
            so.FindProperty("characterStatsTable").objectReferenceValue = AssetDatabase.LoadAssetAtPath<CharacterStatsTableAsset>(TableAssetPaths.CharacterStatsTable);
            so.FindProperty("enemyStatsTable").objectReferenceValue = AssetDatabase.LoadAssetAtPath<EnemyStatsTableAsset>(TableAssetPaths.EnemyStatsTable);
            so.FindProperty("enemyEncounterCompositionTable").objectReferenceValue = AssetDatabase.LoadAssetAtPath<EnemyEncounterCompositionTableAsset>(TableAssetPaths.EnemyEncounterCompositionTable);
            so.ApplyModifiedProperties();
        }

        /// <summary>캐릭터 스탯 표만 쓰는 소비자(유닛 상태 저장소).</summary>
        public static void WireCharacterStatsTable(Object target)
            => Set(target, "characterStatsTable", AssetDatabase.LoadAssetAtPath<CharacterStatsTableAsset>(TableAssetPaths.CharacterStatsTable));

        /// <summary>파티 방향성 정책 카탈로그 - 처음 생기는 에셋이라 소비자(전술 패널·전술 저장소) 모두 자동 배선한다(Docs/설계/17번 §10.5).</summary>
        public static void WirePartyPolicyCatalog(Object target)
            => Set(target, "partyPolicyCatalog", AssetDatabase.LoadAssetAtPath<PartyTacticsPolicyCatalogAsset>(TableAssetPaths.PartyPolicyCatalog));

        /// <summary>
        /// 역할군 방향성 카탈로그(catalog 필드) - 예전엔 수동 인스펙터 연결이었는데 재배치로 연결이 사라져 실전 크래시가 났다(2026-09-08) - 코드로 배선해
        /// 재실행에도 안전하게 한다. 에셋이 없으면 경고하고 기존 연결을 덮지 않는다.
        /// </summary>
        public static void WireRoleGroupCatalog(Object target)
        {
            var asset = AssetDatabase.LoadAssetAtPath<RoleGroupTacticsCatalogAsset>(TableAssetPaths.RoleGroupTacticsCatalog);
            if (asset == null)
            {
                Debug.LogWarning($"'{TableAssetPaths.RoleGroupTacticsCatalog}'를 찾을 수 없어 catalog를 연결하지 못했다.");
                return;
            }
            Set(target, "catalog", asset);
        }

        /// <summary>전술 라벨 문자열 표 2종 - 라벨을 그리는 전술 패널만 필요로 한다(Docs/설계/18번 §8.2).</summary>
        public static void WireTacticsStringTables(Object target)
        {
            var so = new SerializedObject(target);
            so.FindProperty("partyPolicyStrings").objectReferenceValue = AssetDatabase.LoadAssetAtPath<PartyTacticsPolicyStringsTableAsset>(TableAssetPaths.PartyPolicyStringsTable);
            so.FindProperty("roleGroupTacticsStrings").objectReferenceValue = AssetDatabase.LoadAssetAtPath<RoleGroupTacticsStringsTableAsset>(TableAssetPaths.RoleGroupTacticsStringsTable);
            so.ApplyModifiedProperties();
        }

        private static void Set(Object target, string propertyName, Object value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(propertyName).objectReferenceValue = value;
            so.ApplyModifiedProperties();
        }
    }
}
