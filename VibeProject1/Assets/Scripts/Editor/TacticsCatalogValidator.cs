using Game.Core;
using UnityEditor;
using UnityEngine;

namespace Game.Core.Editor
{
    /// <summary>
    /// "직업은 역할군에 매핑됐는데 그 역할군의 후보 목록이 비어 있음"을 조기에 드러낸다
    /// (Docs/설계/12번 §5.7) - override 초기값 규칙("후보 첫 항목")이 빈 목록에서는 성립하지
    /// 않아, 실행 중 조용히 default로 새는 대신 에디터에서 미리 잡아준다.
    /// </summary>
    public static class TacticsCatalogValidator
    {
        [MenuItem("Tools/Game/Validate Tactics Catalog")]
        public static void Validate()
        {
            var mapGuids = AssetDatabase.FindAssets($"t:{nameof(MercenaryRoleGroupMapAsset)}");
            var catalogGuids = AssetDatabase.FindAssets($"t:{nameof(RoleGroupTacticsCatalogAsset)}");
            var statsGuids = AssetDatabase.FindAssets($"t:{nameof(CharacterStatsTableAsset)}");

            if (mapGuids.Length == 0 || catalogGuids.Length == 0 || statsGuids.Length == 0)
            {
                Debug.LogWarning($"{nameof(TacticsCatalogValidator)}: {nameof(MercenaryRoleGroupMapAsset)}, {nameof(RoleGroupTacticsCatalogAsset)} 또는 {nameof(CharacterStatsTableAsset)} 에셋을 프로젝트에서 찾을 수 없다.");
                return;
            }

            // enum이 사라져 System.Enum.GetValues를 못 쓴다 - "존재하는 모든 캐릭터 Id" 출처를
            // CharacterStatsTableAsset(실제 캐릭터 목록)으로 바꿨다(Docs/설계/36번 - 아이템 테이블의
            // TableEnemyTypeCompositionProvider.allTypes와 같은 판단, 엑셀이 단일 진실 소스).
            var warningCount = 0;
            foreach (var mapGuid in mapGuids)
            {
                var map = AssetDatabase.LoadAssetAtPath<MercenaryRoleGroupMapAsset>(AssetDatabase.GUIDToAssetPath(mapGuid));
                if (map == null) continue;

                foreach (var statsGuid in statsGuids)
                {
                    var statsTable = AssetDatabase.LoadAssetAtPath<CharacterStatsTableAsset>(AssetDatabase.GUIDToAssetPath(statsGuid));
                    if (statsTable == null) continue;

                    foreach (var statsEntry in statsTable.Entries)
                    {
                        var mercenaryClass = statsEntry.MercenaryClass;
                        if (!map.TryGetRoleGroup(mercenaryClass, out var roleGroup))
                        {
                            Debug.LogWarning($"{nameof(TacticsCatalogValidator)}: 직업 '{mercenaryClass}'가 '{map.name}'에 역할군으로 매핑되어 있지 않다 - 실행 중 {nameof(UnitTacticsProfileResolver)}가 Frontline 기본값으로 조용히 대체한다.");
                            warningCount++;
                            continue;
                        }

                        foreach (var catalogGuid in catalogGuids)
                        {
                            var catalog = AssetDatabase.LoadAssetAtPath<RoleGroupTacticsCatalogAsset>(AssetDatabase.GUIDToAssetPath(catalogGuid));
                            if (catalog == null) continue;

                            var hasEntry = catalog.TryGetEntry(roleGroup, out var entry)
                                && entry.TargetPriorityOptions is { Count: > 0 }
                                && entry.PositioningOptions is { Count: > 0 }
                                && entry.SelfPreservationOptions is { Count: > 0 };

                            if (!hasEntry)
                            {
                                Debug.LogWarning($"{nameof(TacticsCatalogValidator)}: 직업 '{mercenaryClass}'가 역할군 '{roleGroup}'에 매핑되어 있지만 '{catalog.name}'에 그 역할군의 후보(또는 일부 축)가 비어 있다.");
                                warningCount++;
                            }
                        }
                    }
                }
            }

            Debug.Log($"{nameof(TacticsCatalogValidator)}: 검증 완료 - 경고 {warningCount}건.");
        }
    }
}
