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

            if (mapGuids.Length == 0 || catalogGuids.Length == 0)
            {
                Debug.LogWarning($"{nameof(TacticsCatalogValidator)}: {nameof(MercenaryRoleGroupMapAsset)} 또는 {nameof(RoleGroupTacticsCatalogAsset)} 에셋을 프로젝트에서 찾을 수 없다.");
                return;
            }

            var warningCount = 0;
            foreach (var mapGuid in mapGuids)
            {
                var map = AssetDatabase.LoadAssetAtPath<MercenaryRoleGroupMapAsset>(AssetDatabase.GUIDToAssetPath(mapGuid));
                if (map == null) continue;

                foreach (var mercenaryClass in System.Enum.GetValues(typeof(MercenaryClass)))
                {
                    if (!map.TryGetRoleGroup((MercenaryClass)mercenaryClass, out var roleGroup))
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

            Debug.Log($"{nameof(TacticsCatalogValidator)}: 검증 완료 - 경고 {warningCount}건.");
        }
    }
}
