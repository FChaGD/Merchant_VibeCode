using System;
using System.Collections.Generic;
using System.IO;
using Game.Core;
using UnityEditor;
using UnityEngine;

namespace Game.Core.Editor
{
    /// <summary>
    /// Assets/Table/City/City.xlsx를 읽어 지도 자산(TripCityMapAsset)과 도시·지역 스트링 자산을 덮어쓴다(Docs/설계/69번 §3).
    /// 시트: RegionData·RegionStrings·CityData(RegionId 포함)·CityStrings·GateData·RoadData(끝점 C/G 표기, 예전 CityDetailData 대체).
    /// 디버그 지도 "저장"(TripCityMapPersistence)이 이 워크북을 쓰고, 이 임포터가 그 결과를 자산으로 반영한다.
    /// 무결성(지역 참조·관문 짝·도로 끝점·같은 지역 도로)은 WorldMapDataValidator로 검사하고, 어기면 임포트를 중단한다 - 틀린 지도가
    /// 조용히 들어가 도달 판정이 어긋나는 것보다 Play 시점에 바로 드러나는 편이 낫다(설계 69번 §3.2).
    /// 규모 Id의 유효성은 검사하지 않는다 - 규모 정의가 Bootstrap 인스펙터 데이터라 런타임 판정이 경고한다.
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

            var regionRows = EditorTableReader.ReadSheet(workbookPath, "RegionData");
            var cityRows = EditorTableReader.ReadSheet(workbookPath, "CityData");
            var gateRows = EditorTableReader.ReadSheet(workbookPath, "GateData");
            var roadRows = EditorTableReader.ReadSheet(workbookPath, "RoadData");

            var regions = new List<(int Id, Vector2 Size)>();
            foreach (var row in regionRows)
            {
                regions.Add((EditorTableReader.ParseInt(row, "Id"), new Vector2(EditorTableReader.ParseFloat(row, "Width"), EditorTableReader.ParseFloat(row, "Height"))));
            }
            var sizeByRegion = new Dictionary<int, Vector2>();
            foreach (var region in regions) sizeByRegion[region.Id] = region.Size;

            var cityChecks = new List<WorldMapDataValidator.CityRow>();
            foreach (var row in cityRows) cityChecks.Add(new WorldMapDataValidator.CityRow(EditorTableReader.ParseInt(row, "Id"), EditorTableReader.ParseInt(row, "RegionId")));
            var gateChecks = new List<WorldMapDataValidator.GateRow>();
            foreach (var row in gateRows) gateChecks.Add(new WorldMapDataValidator.GateRow(EditorTableReader.ParseInt(row, "Id"), EditorTableReader.ParseInt(row, "RegionId"), EditorTableReader.ParseInt(row, "PairGateId")));
            var roads = new List<(string A, string B)>();
            foreach (var row in roadRows) roads.Add((row["NodeA"], row["NodeB"]));

            var errors = WorldMapDataValidator.Validate(sizeByRegion.Keys, cityChecks, gateChecks, roads);
            var roadDifficulties = ReadRoadDifficulties(roadRows, errors);
            if (errors.Count > 0)
            {
                throw new FormatException($"{nameof(CityTableImporter)}: City.xlsx 지도 데이터 오류 {errors.Count}건\n- " + string.Join("\n- ", errors));
            }

            WriteMap(regions, cityRows, gateRows, roads, roadDifficulties, sizeByRegion);
            var cityIds = new HashSet<int>();
            foreach (var city in cityChecks) cityIds.Add(city.Id);
            ImportCityStrings(workbookPath, cityIds);
            ImportRegionStrings(workbookPath, sizeByRegion);
            AssetDatabase.SaveAssets();
        }

        // 도로 난이도 기본값(Docs/설계/76번 §3.1) - 열이 없거나 빈 칸이면 50, 1~100 밖이면 임포트 오류.
        private static List<int> ReadRoadDifficulties(IReadOnlyList<IReadOnlyDictionary<string, string>> roadRows, List<string> errors)
        {
            var result = new List<int>(roadRows.Count);
            for (var i = 0; i < roadRows.Count; i++)
            {
                var row = roadRows[i];
                if (!row.TryGetValue("Difficulty", out var text) || string.IsNullOrWhiteSpace(text))
                {
                    result.Add(TripTravelSettings.DefaultRoadDifficulty);
                    continue;
                }
                if (!int.TryParse(text.Trim(), out var value) || value < TripTravelRules.MinDifficulty || value > TripTravelRules.MaxDifficulty)
                {
                    errors.Add($"RoadData {i + 2}행: Difficulty '{text}'는 {TripTravelRules.MinDifficulty}~{TripTravelRules.MaxDifficulty} 정수여야 한다.");
                    result.Add(TripTravelSettings.DefaultRoadDifficulty);
                    continue;
                }
                result.Add(value);
            }
            return result;
        }

        private static void WriteMap(List<(int Id, Vector2 Size)> regions, IReadOnlyList<IReadOnlyDictionary<string, string>> cityRows, IReadOnlyList<IReadOnlyDictionary<string, string>> gateRows, List<(string A, string B)> roads, List<int> roadDifficulties, Dictionary<int, Vector2> sizeByRegion)
        {
            var asset = EditorTableReader.GetOrCreateAsset<TripCityMapAsset>(TableAssetPaths.TripCityMap);
            var so = new SerializedObject(asset);

            var regionsProp = so.FindProperty("regions");
            regionsProp.arraySize = regions.Count;
            for (var i = 0; i < regions.Count; i++)
            {
                var element = regionsProp.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("RegionId").intValue = regions[i].Id;
                element.FindPropertyRelative("Size").vector2Value = regions[i].Size;
            }

            var citiesProp = so.FindProperty("cities");
            citiesProp.arraySize = cityRows.Count;
            for (var i = 0; i < cityRows.Count; i++)
            {
                var row = cityRows[i];
                var cityId = EditorTableReader.ParseInt(row, "Id");
                var regionId = EditorTableReader.ParseInt(row, "RegionId");
                var scale = row["Scale"];
                if (string.IsNullOrWhiteSpace(scale))
                {
                    Debug.LogWarning($"{nameof(CityTableImporter)}: 도시 {cityId}의 규모(Scale)가 비어 있다 - 가장 작은 규모로 취급된다.");
                }

                var element = citiesProp.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("CityId").intValue = cityId;
                element.FindPropertyRelative("RegionId").intValue = regionId;
                element.FindPropertyRelative("MapPosition").vector2Value = ToContent(row, sizeByRegion[regionId]);
                element.FindPropertyRelative("Scale").stringValue = scale?.Trim() ?? string.Empty;
            }

            var gatesProp = so.FindProperty("gates");
            gatesProp.arraySize = gateRows.Count;
            for (var i = 0; i < gateRows.Count; i++)
            {
                var row = gateRows[i];
                var regionId = EditorTableReader.ParseInt(row, "RegionId");
                var element = gatesProp.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("GateId").intValue = EditorTableReader.ParseInt(row, "Id");
                element.FindPropertyRelative("RegionId").intValue = regionId;
                element.FindPropertyRelative("MapPosition").vector2Value = ToContent(row, sizeByRegion[regionId]);
                element.FindPropertyRelative("PairGateId").intValue = EditorTableReader.ParseInt(row, "PairGateId");
            }

            var roadsProp = so.FindProperty("roads");
            roadsProp.arraySize = roads.Count;
            for (var i = 0; i < roads.Count; i++)
            {
                MapNodeId.TryParse(roads[i].A, out var a);
                MapNodeId.TryParse(roads[i].B, out var b);
                var element = roadsProp.GetArrayElementAtIndex(i);
                WriteNode(element.FindPropertyRelative("A"), a);
                WriteNode(element.FindPropertyRelative("B"), b);
                element.FindPropertyRelative("Difficulty").intValue = roadDifficulties[i];
            }

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(asset);
            Debug.Log($"{nameof(CityTableImporter)}: 지역 {regions.Count}개, 도시 {cityRows.Count}개, 관문 {gateRows.Count}개, 도로 {roads.Count}개 임포트 완료.");
        }

        private static Vector2 ToContent(IReadOnlyDictionary<string, string> row, Vector2 mapSize)
            => TripCityMapCoordinateConverter.ToContentSpace(new Vector2(EditorTableReader.ParseFloat(row, "X"), EditorTableReader.ParseFloat(row, "Y")), mapSize);

        private static void WriteNode(SerializedProperty property, MapNodeId node)
        {
            property.FindPropertyRelative("Kind").enumValueIndex = (int)node.Kind;
            property.FindPropertyRelative("Id").intValue = node.Id;
        }

        // 사람이 엑셀에서 직접 채우는 시트라(입력 UI 없음, 기획 15번 §8.1) 비어 있을 수 있다 - 0행도 정상이다.
        private static void ImportCityStrings(string workbookPath, HashSet<int> cityIds)
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
        }

        private static void ImportRegionStrings(string workbookPath, Dictionary<int, Vector2> sizeByRegion)
        {
            var rows = EditorTableReader.ReadSheet(workbookPath, "RegionStrings");
            var kept = new List<(int Id, string Name)>();
            foreach (var row in rows)
            {
                var regionId = EditorTableReader.ParseInt(row, "Id");
                if (!sizeByRegion.ContainsKey(regionId))
                {
                    Debug.LogWarning($"{nameof(CityTableImporter)}: RegionStrings의 지역 {regionId}이(가) RegionData에 없어 무시한다.");
                    continue;
                }
                kept.Add((regionId, row["Name"]));
            }

            var asset = EditorTableReader.GetOrCreateAsset<TripRegionStringsTableAsset>(TableAssetPaths.TripRegionStringsTable);
            var so = new SerializedObject(asset);
            var entriesProp = so.FindProperty("entries");
            entriesProp.arraySize = kept.Count;
            for (var i = 0; i < kept.Count; i++)
            {
                var element = entriesProp.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("Id").intValue = kept[i].Id;
                element.FindPropertyRelative("Name").stringValue = kept[i].Name;
            }
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(asset);
        }
    }
}
