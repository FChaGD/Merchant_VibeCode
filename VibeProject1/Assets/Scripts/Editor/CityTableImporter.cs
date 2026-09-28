using System.Collections.Generic;
using System.IO;
using Game.Core;
using UnityEditor;
using UnityEngine;

namespace Game.Core.Editor
{
    /// <summary>
    /// Assets/Table/City/City.xlsx(도시 테이블 3종: CityData/CityStrings/CityDetailData, Docs/기획/57번 §4.2)를 읽어
    /// TripCityMapAsset/TripCityStringsTableAsset을 덮어쓴다(Docs/설계/58번 §3). 디버그 도시 배치 "저장"(TripCityMapPersistence)이
    /// 이 워크북의 좌표·경로를 쓰고, 이 임포터가 그 결과를 SO로 반영한다. 런타임 자산 이름은 기존(TripCity*)을 유지한다(설계 58번 §9).
    /// 이름·경로 행이 없는 도시 Id를 가리키면 경고하고 그 행을 버린다 - 도시를 지웠을 때 남은 행을 드러내기 위해서다(기획 57번 §4.3).
    /// 규모 Id의 유효성은 검사하지 않는다 - 규모 정의가 Bootstrap 인스펙터 데이터라 에디터 임포터가 알 수 없어 런타임 판정이 경고한다.
    /// </summary>
    public static class CityTableImporter
    {
        private const string WorkbookRelativePath = "City/City.xlsx";

        [MenuItem("Tools/Game/Table/Import City Table")]
        public static void Import()
        {
            var workbookPath = Path.Combine(Application.dataPath, "Table", WorkbookRelativePath);
            if (!File.Exists(workbookPath))
            {
                Debug.LogError($"{nameof(CityTableImporter)}: 워크북을 찾을 수 없다 - '{workbookPath}'.");
                return;
            }

            var cityIds = ImportCityData(workbookPath);
            ImportStrings(workbookPath, cityIds);
            AssetDatabase.SaveAssets();
        }

        private static HashSet<int> ImportCityData(string workbookPath)
        {
            var cityRows = EditorTableReader.ReadSheet(workbookPath, "CityData");
            var routeRows = EditorTableReader.ReadSheet(workbookPath, "CityDetailData");
            var asset = EditorTableReader.GetOrCreateAsset<TripCityMapAsset>(TableAssetPaths.TripCityMap);
            var so = new SerializedObject(asset);

            var cityIds = new HashSet<int>();
            var citiesProp = so.FindProperty("cities");
            citiesProp.arraySize = cityRows.Count;
            for (var i = 0; i < cityRows.Count; i++)
            {
                var cityId = EditorTableReader.ParseInt(cityRows[i], "Id");
                if (!cityIds.Add(cityId))
                {
                    throw new System.FormatException($"CityData의 도시 Id {cityId}가 중복됐다.");
                }

                var scale = cityRows[i]["Scale"];
                if (string.IsNullOrWhiteSpace(scale))
                {
                    Debug.LogWarning($"{nameof(CityTableImporter)}: 도시 {cityId}의 규모(Scale)가 비어 있다 - 가장 작은 규모로 취급된다.");
                }

                var normalized = new Vector2(EditorTableReader.ParseFloat(cityRows[i], "X"), EditorTableReader.ParseFloat(cityRows[i], "Y"));
                var element = citiesProp.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("CityId").intValue = cityId;
                element.FindPropertyRelative("MapPosition").vector2Value = TripCityMapCoordinateConverter.ToContentSpace(normalized);
                element.FindPropertyRelative("Scale").stringValue = scale?.Trim() ?? string.Empty;
            }

            var routes = new List<(int CityIdA, int CityIdB)>(routeRows.Count);
            foreach (var row in routeRows)
            {
                var cityIdA = EditorTableReader.ParseInt(row, "CityIdA");
                var cityIdB = EditorTableReader.ParseInt(row, "CityIdB");
                if (!cityIds.Contains(cityIdA) || !cityIds.Contains(cityIdB))
                {
                    Debug.LogWarning($"{nameof(CityTableImporter)}: 경로 {cityIdA}-{cityIdB}가 CityData에 없는 도시를 가리켜 무시한다.");
                    continue;
                }
                routes.Add((cityIdA, cityIdB));
            }

            var routesProp = so.FindProperty("routes");
            routesProp.arraySize = routes.Count;
            for (var i = 0; i < routes.Count; i++)
            {
                var element = routesProp.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("CityIdA").intValue = routes[i].CityIdA;
                element.FindPropertyRelative("CityIdB").intValue = routes[i].CityIdB;
            }

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(asset);
            Debug.Log($"{nameof(CityTableImporter)}: 도시 {cityRows.Count}개, 경로 {routes.Count}개 임포트 완료.");
            return cityIds;
        }

        // 사람이 엑셀에서 직접 채우는 시트라(입력 UI 없음, 기획 15번 §8.1) 비어 있을 수 있다 - 0행도 정상이다.
        private static void ImportStrings(string workbookPath, HashSet<int> cityIds)
        {
            var rows = EditorTableReader.ReadSheet(workbookPath, "CityStrings");
            var kept = new List<IReadOnlyDictionary<string, string>>(rows.Count);
            foreach (var row in rows)
            {
                var cityId = EditorTableReader.ParseInt(row, "Id");
                if (!cityIds.Contains(cityId))
                {
                    Debug.LogWarning($"{nameof(CityTableImporter)}: CityStrings의 도시 {cityId}가 CityData에 없어 무시한다.");
                    continue;
                }
                kept.Add(row);
            }

            var asset = EditorTableReader.GetOrCreateAsset<TripCityStringsTableAsset>(TableAssetPaths.TripCityStringsTable);
            var so = new SerializedObject(asset);
            var entriesProp = so.FindProperty("entries");
            entriesProp.arraySize = kept.Count;
            for (var i = 0; i < kept.Count; i++)
            {
                var element = entriesProp.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("Id").intValue = EditorTableReader.ParseInt(kept[i], "Id");
                element.FindPropertyRelative("Name").stringValue = kept[i]["Name"];
                element.FindPropertyRelative("Description").stringValue = kept[i]["Description"];
            }
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(asset);
            Debug.Log($"{nameof(CityTableImporter)}: 도시 이름/설명 {kept.Count}개 임포트 완료.");
        }
    }
}
