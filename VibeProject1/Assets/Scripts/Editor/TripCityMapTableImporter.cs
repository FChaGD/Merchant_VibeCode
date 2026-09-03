using System.IO;
using Game.Core;
using UnityEditor;
using UnityEngine;

namespace Game.Core.Editor
{
    /// <summary>
    /// Assets/Table/Trip/TripCityMap.xlsx(시트 3개: TripCities/TripRoutes/TripCityStrings)를 읽어
    /// TripCityMapAsset/TripCityStringsTableAsset을 덮어쓴다(Docs/설계/20번 §5/§9). "저장" 버튼
    /// (TripCityMapPersistence, Core/Debug/Trip/)이 이 워크북에 쓰고, 이 임포터가 그 결과를 컴파일된
    /// SO로 반영한다 - 데이터 테이블 임포터들과 완전히 같은 자리·패턴이라 EditorTableReader.
    /// GetOrCreateAsset&lt;T&gt;를 그대로 재사용한다(TripCityMapPersistence는 다른 어셈블리라 못 썼던 것과
    /// 대조적 - 이 임포터는 애초에 같은 어셈블리다).
    /// Id/CityIdA/CityIdB는 정수(기획 15번 §8.2, 설계 20번 §9)라 EditorTableReader.ParseInt로 읽는다.
    /// TripRoutes가 참조하는 도시 Id가 TripCities에 실제로 존재하는지, TripCityStrings의 Id가
    /// TripCities에 존재하는지는 검증하지 않는다(데이터 테이블 v2의 교차 파일 FK 미검증 결정과 같은
    /// 판단, 설계 18번 §7).
    /// </summary>
    public static class TripCityMapTableImporter
    {
        private const string WorkbookRelativePath = "Trip/TripCityMap.xlsx";
        private const string AssetPath = "Assets/Prefabs/ScriptableObejct/TripCityMap.asset";
        private const string StringsAssetPath = "Assets/Prefabs/ScriptableObejct/TripCityStringsTable.asset";

        [MenuItem("Tools/Game/Table/Import Trip City Map")]
        public static void Import()
        {
            var workbookPath = Path.Combine(Application.dataPath, "Table", WorkbookRelativePath);
            if (!File.Exists(workbookPath))
            {
                Debug.LogError($"{nameof(TripCityMapTableImporter)}: 워크북을 찾을 수 없다 - '{workbookPath}'.");
                return;
            }

            ImportCityMap(workbookPath);
            ImportStrings(workbookPath);

            AssetDatabase.SaveAssets();
        }

        private static void ImportCityMap(string workbookPath)
        {
            var cityRows = EditorTableReader.ReadSheet(workbookPath, "TripCities");
            var routeRows = EditorTableReader.ReadSheet(workbookPath, "TripRoutes");

            var asset = EditorTableReader.GetOrCreateAsset<TripCityMapAsset>(AssetPath);
            var so = new SerializedObject(asset);

            var citiesProp = so.FindProperty("cities");
            citiesProp.arraySize = cityRows.Count;
            for (var i = 0; i < cityRows.Count; i++)
            {
                var normalized = new Vector2(EditorTableReader.ParseFloat(cityRows[i], "X"), EditorTableReader.ParseFloat(cityRows[i], "Y"));
                var contentPosition = TripCityMapCoordinateConverter.ToContentSpace(normalized);

                var element = citiesProp.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("CityId").intValue = EditorTableReader.ParseInt(cityRows[i], "Id");
                element.FindPropertyRelative("MapPosition").vector2Value = contentPosition;
            }

            var routesProp = so.FindProperty("routes");
            routesProp.arraySize = routeRows.Count;
            for (var i = 0; i < routeRows.Count; i++)
            {
                var element = routesProp.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("CityIdA").intValue = EditorTableReader.ParseInt(routeRows[i], "CityIdA");
                element.FindPropertyRelative("CityIdB").intValue = EditorTableReader.ParseInt(routeRows[i], "CityIdB");
            }

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(asset);
            Debug.Log($"{nameof(TripCityMapTableImporter)}: 도시 {cityRows.Count}개, 경로 {routeRows.Count}개 임포트 완료.");
        }

        // 사람이 엑셀에서 직접 채우는 시트라(입력 UI 없음, 기획 §8.1) 비어 있을 수 있다 - 그래도
        // 정상 동작해야 한다(0행 임포트는 에러가 아니다).
        private static void ImportStrings(string workbookPath)
        {
            var rows = EditorTableReader.ReadSheet(workbookPath, "TripCityStrings");
            var asset = EditorTableReader.GetOrCreateAsset<TripCityStringsTableAsset>(StringsAssetPath);
            var so = new SerializedObject(asset);

            var entriesProp = so.FindProperty("entries");
            entriesProp.arraySize = rows.Count;
            for (var i = 0; i < rows.Count; i++)
            {
                var element = entriesProp.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("Id").intValue = EditorTableReader.ParseInt(rows[i], "Id");
                element.FindPropertyRelative("Name").stringValue = rows[i]["Name"];
                element.FindPropertyRelative("Description").stringValue = rows[i]["Description"];
            }

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(asset);
            Debug.Log($"{nameof(TripCityMapTableImporter)}: 도시 이름/설명 {rows.Count}개 임포트 완료.");
        }
    }
}
