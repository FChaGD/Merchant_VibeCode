#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClosedXML.Excel;
using Game.Core;
using UnityEditor;
using UnityEngine;

namespace Game.Core.DebugTools
{
    /// <summary>
    /// "저장" 버튼이 배치된 도시/경로를 엑셀 워크북으로 내보내고(Save), 임포트된 결과(TripCityMapAsset/
    /// TripCityStringsTableAsset)를 되읽는다(TryLoad/TryLoadStrings, Docs/기획/15번 §7/§8, 설계 20번).
    /// 엑셀 쓰기는 ClosedXML(Assets/Plugins/Editor/ClosedXML/)이 담당 - ExcelDataReader는 읽기 전용이라
    /// 쓰기에 못 쓴다(설계 20번 §3). "에셋 직렬화/워크북 쓰기"라는 별도 관심사를 TripMapInteractionCoordinator
    /// (이미 드래그 배선/마커·선 생성/출발·도착 연동을 지고 있음)에 얹지 않기 위해 분리했다(SRP).
    ///
    /// Save()는 더 이상 TripCityMapAsset을 직접 쓰지 않는다 - 엑셀에 쓰고, TripCityMapTableImporter
    /// (Assets/Scripts/Editor/, TableAutoImportOnPlay에 연결됨)가 그 엑셀을 읽어 에셋을 채운다. 좌표는
    /// 엑셀에 쓸 때 정규화(TripCityMapCoordinateConverter.ToNormalized)한다 - 콘텐츠 좌표(중심 기준,
    /// 음수 포함)를 사람이 그대로 보면 감을 잡기 어렵기 때문(기획 15번 §7.2).
    /// </summary>
    internal static class TripCityMapPersistence
    {
        // 워크북(사람이 편집하는 원본)과 컴파일된 SO 에셋(임포터 산출물)은 다른 데이터 테이블과 같은
        // 관례로 분리한다 - Assets/Table/ = 워크북, Assets/Prefabs/ScriptableObejct/ = 컴파일된 SO
        // (설계 20번 §4, 기획 14번 §6.3의 도메인 폴더 관례와 통일).
        private const string WorkbookRelativePath = "Trip/TripCityMap.xlsx";
        private const string AssetPath = "Assets/Prefabs/ScriptableObejct/TripCityMap.asset";
        private const string StringsAssetPath = "Assets/Prefabs/ScriptableObejct/TripCityStringsTable.asset";

        public static void Save(ITripCityReader cities, ITripRouteRepository routes)
        {
            var allCities = cities.GetAll();
            var allRoutes = routes.GetAllRoutes();
            var workbookPath = Path.Combine(Application.dataPath, "Table", WorkbookRelativePath);

            // 워크북을 열고(기존 파일 있으면 로드) 쓰는 전체 구간을 하나로 묶어 예외를 처리한다 - 이
            // 파일은 "사람이 엑셀에서 직접 편집"하는 게 기능의 전제라(기획 §7.1-1), Excel이 그 파일을
            // 열어둔 채로 저장 버튼을 누르는 상황이 실제로 자주 생긴다(IOException: Sharing violation,
            // 2026-09-03 실사용 중 발생 확인) - 데이터 테이블 임포트 때 겪은 것과 같은 종류의 파일
            // 잠금 문제이지 코드 버그가 아니다. 원인을 곧장 알 수 있게 안내 로그로 바꿔서 던진다.
            try
            {
                SaveInternal(allCities, allRoutes, workbookPath);
            }
            catch (IOException ex)
            {
                Debug.LogError($"{nameof(TripCityMapPersistence)}: '{workbookPath}' 파일에 쓸 수 없다 - 엑셀 등 다른 프로그램이 이 파일을 열어둔 상태일 가능성이 높다. 그 프로그램에서 파일을 닫고 다시 저장하라. ({ex.Message})");
                return;
            }

            AssetDatabase.Refresh(); // 다음 Import 메뉴/자동 임포트가 방금 쓴 파일을 바로 찾을 수 있게 갱신.
            Debug.Log($"{nameof(TripCityMapPersistence)}: 도시 {allCities.Count}개, 경로 {allRoutes.Count}개를 '{WorkbookRelativePath}'로 내보냈다. Tools/Game/Table/Import Trip City Map으로 지도에 반영하거나, 다음 플레이 진입 시 자동 반영된다.");
        }

        // TripCityStrings는 사람이 엑셀에서 직접 채우는 시트라(입력 UI 없음, 기획 §8.1) 저장 버튼이
        // 기존에 입력된 이름/설명을 알지 못한다 - 기존 파일이 있으면 열어서 TripCities/TripRoutes만
        // 새로 쓰고, TripCityStrings의 기존 행은 손대지 않는다(설계 20번 §9.5). 다만 드래그로 새로
        // 배치한 도시(String 시트에 아직 행이 없는 Id)는 자리표시자 행을 추가해준다 - 안 그러면
        // 새 도시의 이름을 어디서 채워야 하는지 사람이 알 방법이 없다(2026-09-03 사용자 피드백).
        private static void SaveInternal(IReadOnlyList<TripCity> allCities, IReadOnlyCollection<(int CityIdA, int CityIdB)> allRoutes, string workbookPath)
        {
            using var workbook = File.Exists(workbookPath) ? new XLWorkbook(workbookPath) : new XLWorkbook();

            if (workbook.Worksheets.TryGetWorksheet("TripCities", out var oldCitySheet))
            {
                oldCitySheet.Delete();
            }
            if (workbook.Worksheets.TryGetWorksheet("TripRoutes", out var oldRouteSheet))
            {
                oldRouteSheet.Delete();
            }

            AppendPlaceholderStringRowsForNewCities(workbook, allCities);

            var citySheet = workbook.Worksheets.Add("TripCities");
            citySheet.Cell(1, 1).Value = "Id";
            citySheet.Cell(1, 2).Value = "X";
            citySheet.Cell(1, 3).Value = "Y";
            for (var i = 0; i < allCities.Count; i++)
            {
                var normalized = TripCityMapCoordinateConverter.ToNormalized(allCities[i].MapPosition);
                citySheet.Cell(i + 2, 1).Value = allCities[i].Id;
                // ClosedXML의 Cell.Value(XLCellValue)는 float용 암시적 변환이 따로 없다 - double로
                // 명시 캐스팅해서 float→double(표준 변환)→XLCellValue(사용자 정의 변환) 경로를 확실히 탄다.
                citySheet.Cell(i + 2, 2).Value = (double)normalized.x;
                citySheet.Cell(i + 2, 3).Value = (double)normalized.y;
            }

            var routeSheet = workbook.Worksheets.Add("TripRoutes");
            routeSheet.Cell(1, 1).Value = "CityIdA";
            routeSheet.Cell(1, 2).Value = "CityIdB";
            var row = 0;
            foreach (var route in allRoutes)
            {
                routeSheet.Cell(row + 2, 1).Value = route.CityIdA;
                routeSheet.Cell(row + 2, 2).Value = route.CityIdB;
                row++;
            }

            workbook.SaveAs(workbookPath);
        }

        // 드래그로 배치된 도시 중 TripCityStrings에 아직 행이 없는 Id에는 자리표시자 행을 추가한다 -
        // 시트 자체가 없으면 헤더까지 새로 만든다. 이미 있는 행(사람이 입력했을 수 있는 값)은 절대
        // 건드리지 않는다 - 그래서 "Id가 이미 있는지"만 보고 없는 것만 덧붙인다.
        private static void AppendPlaceholderStringRowsForNewCities(XLWorkbook workbook, IReadOnlyList<TripCity> allCities)
        {
            var stringsSheet = workbook.Worksheets.TryGetWorksheet("TripCityStrings", out var existing) ? existing : null;
            if (stringsSheet == null)
            {
                stringsSheet = workbook.Worksheets.Add("TripCityStrings");
                stringsSheet.Cell(1, 1).Value = "Id";
                stringsSheet.Cell(1, 2).Value = "Name";
                stringsSheet.Cell(1, 3).Value = "Description";
            }

            var knownIds = new HashSet<int>();
            foreach (var usedRow in stringsSheet.RowsUsed().Skip(1)) // 1행은 헤더 - 건너뛴다.
            {
                var idCell = usedRow.Cell(1);
                if (!idCell.IsEmpty())
                {
                    knownIds.Add(idCell.GetValue<int>());
                }
            }

            var nextRow = (stringsSheet.LastRowUsed()?.RowNumber() ?? 1) + 1;
            foreach (var city in allCities)
            {
                if (knownIds.Contains(city.Id))
                {
                    continue;
                }

                // 기존 BuildLocationInfo의 자동 생성 문구와 동일하게 맞춘다(TripMapInteractionCoordinator
                // 참고) - 사람이 엑셀에서 이 값을 보고 바로 고쳐 쓸 수 있게, 창작하지 않고 같은 자리표시자를 쓴다.
                stringsSheet.Cell(nextRow, 1).Value = city.Id;
                stringsSheet.Cell(nextRow, 2).Value = $"디버그 도시 {city.Id}";
                stringsSheet.Cell(nextRow, 3).Value = "값 없음";
                nextRow++;
            }
        }

        public static bool TryLoad(out TripCityMapAsset asset)
        {
            asset = AssetDatabase.LoadAssetAtPath<TripCityMapAsset>(AssetPath);
            return asset != null;
        }

        public static bool TryLoadStrings(out TripCityStringsTableAsset asset)
        {
            asset = AssetDatabase.LoadAssetAtPath<TripCityStringsTableAsset>(StringsAssetPath);
            return asset != null;
        }
    }
}
#endif
