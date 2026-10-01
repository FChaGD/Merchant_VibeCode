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
    /// 지도 디버그 "저장" 버튼이 월드 지도 모델을 엑셀 워크북(City.xlsx)으로 내보낸다(Docs/설계/69번 §6, 기획 15번 §7). 엑셀 쓰기는
    /// ClosedXML이 담당한다 - ExcelDataReader는 읽기 전용이라 쓸 수 없다(설계 20번 §3). 쓴 뒤에는 CityTableImporter(Play 시 자동
    /// 임포트)가 자산으로 반영한다.
    ///
    /// 다시 쓰는 시트: RegionData·CityData·GateData·RoadData(옛 CityDetailData는 지운다). 사람이 채우는 값은 보존한다 - CityData의
    /// 규모(Scale)는 옛 시트에서 도시 Id별로 읽어 되돌려 쓰고, CityStrings·RegionStrings는 기존 행을 건드리지 않고 새 도시·지역에만
    /// 자리표시자 행을 덧붙인다(설계 20번 §9.5, 2026-09-03 사용자 피드백). 좌표는 지역 지도 크기 기준으로 정규화해 쓴다.
    /// </summary>
    internal static class TripCityMapPersistence
    {
        private const string WorkbookRelativePath = "City/City.xlsx";
        private const string RegionSheetName = "RegionData";
        private const string RegionStringsSheetName = "RegionStrings";
        private const string CitySheetName = "CityData";
        private const string CityStringsSheetName = "CityStrings";
        private const string GateSheetName = "GateData";
        private const string RoadSheetName = "RoadData";
        private const string LegacyRoadSheetName = "CityDetailData";

        public static void Save(IWorldMapReader map)
        {
            var workbookPath = Path.Combine(Application.dataPath, "Table", WorkbookRelativePath);

            // Excel이 파일을 열어 둔 채 저장 버튼을 누르는 경우가 실제로 잦다(2026-09-03 확인) - 원인을 바로 알 수 있게 안내한다.
            try
            {
                SaveInternal(map, workbookPath);
            }
            catch (IOException ex)
            {
                Debug.LogError($"{nameof(TripCityMapPersistence)}: '{workbookPath}' 파일에 쓸 수 없다 - 엑셀 등 다른 프로그램이 이 파일을 열어둔 상태일 가능성이 높다. 그 프로그램에서 파일을 닫고 다시 저장하라. ({ex.Message})");
                return;
            }

            AssetDatabase.Refresh();
            Debug.Log($"{nameof(TripCityMapPersistence)}: 지역 {map.Regions.Count}개, 도시 {map.AllCities.Count()}개, 관문 {map.AllGates.Count()}개, 도로 {map.AllRoads.Count()}개를 '{WorkbookRelativePath}'로 내보냈다. 다음 플레이 진입 시 자동 반영된다.");
        }

        private static void SaveInternal(IWorldMapReader map, string workbookPath)
        {
            using var workbook = File.Exists(workbookPath) ? new XLWorkbook(workbookPath) : new XLWorkbook();

            var scaleByCityId = new Dictionary<int, string>();
            if (workbook.Worksheets.TryGetWorksheet(CitySheetName, out var oldCitySheet)) ReadScales(oldCitySheet, scaleByCityId);
            foreach (var name in new[] { RegionSheetName, CitySheetName, GateSheetName, RoadSheetName, LegacyRoadSheetName })
            {
                if (workbook.Worksheets.TryGetWorksheet(name, out var old)) old.Delete();
            }

            var regionSheet = AddSheet(workbook, RegionSheetName, "Id", "Width", "Height");
            var row = 2;
            foreach (var region in map.Regions)
            {
                regionSheet.Cell(row, 1).Value = region.Id;
                regionSheet.Cell(row, 2).Value = (double)region.Size.x;
                regionSheet.Cell(row, 3).Value = (double)region.Size.y;
                row++;
            }

            var citySheet = AddSheet(workbook, CitySheetName, "Id", "RegionId", "X", "Y", "Scale");
            row = 2;
            foreach (var city in map.AllCities.OrderBy(city => city.Id))
            {
                var normalized = Normalize(map, city.RegionId, city.Position);
                citySheet.Cell(row, 1).Value = city.Id;
                citySheet.Cell(row, 2).Value = city.RegionId;
                // ClosedXML의 Cell.Value는 float용 암시적 변환이 없다 - double로 명시 캐스팅한다.
                citySheet.Cell(row, 3).Value = (double)normalized.x;
                citySheet.Cell(row, 4).Value = (double)normalized.y;
                citySheet.Cell(row, 5).Value = scaleByCityId.TryGetValue(city.Id, out var scale) ? scale : city.Scale;
                row++;
            }

            var gateSheet = AddSheet(workbook, GateSheetName, "Id", "RegionId", "X", "Y", "PairGateId");
            row = 2;
            foreach (var gate in map.AllGates.OrderBy(gate => gate.Id))
            {
                var normalized = Normalize(map, gate.RegionId, gate.Position);
                gateSheet.Cell(row, 1).Value = gate.Id;
                gateSheet.Cell(row, 2).Value = gate.RegionId;
                gateSheet.Cell(row, 3).Value = (double)normalized.x;
                gateSheet.Cell(row, 4).Value = (double)normalized.y;
                gateSheet.Cell(row, 5).Value = gate.PairGateId;
                row++;
            }

            var roadSheet = AddSheet(workbook, RoadSheetName, "NodeA", "NodeB");
            row = 2;
            foreach (var (a, b) in map.AllRoads)
            {
                roadSheet.Cell(row, 1).Value = a.ToString();
                roadSheet.Cell(row, 2).Value = b.ToString();
                row++;
            }

            AppendMissingRows(workbook, CityStringsSheetName, new[] { "Id", "Name", "Description" }, map.AllCities.Select(city => city.Id), id => new object[] { id, $"디버그 도시 {id}", "값 없음" });
            AppendMissingRows(workbook, RegionStringsSheetName, new[] { "Id", "Name" }, map.Regions.Select(region => region.Id), id => new object[] { id, map.TryGetRegion(id, out var region) ? region.Name : $"지역{id}" });

            workbook.SaveAs(workbookPath);
        }

        private static Vector2 Normalize(IWorldMapReader map, int regionId, Vector2 position)
            => TripCityMapCoordinateConverter.ToNormalized(position, map.TryGetRegion(regionId, out var region) ? region.Size : WorldMap.DefaultRegionSize);

        private static IXLWorksheet AddSheet(XLWorkbook workbook, string name, params string[] headers)
        {
            var sheet = workbook.Worksheets.Add(name);
            for (var i = 0; i < headers.Length; i++) sheet.Cell(1, i + 1).Value = headers[i];
            return sheet;
        }

        // 머리글에서 Id·Scale 열 위치를 찾아 읽는다 - 사람이 열 순서를 바꿔도 값이 어긋나지 않게 한다.
        private static void ReadScales(IXLWorksheet sheet, Dictionary<int, string> scaleByCityId)
        {
            int idColumn = 0, scaleColumn = 0;
            foreach (var cell in sheet.Row(1).CellsUsed())
            {
                var name = cell.GetString();
                if (name == "Id") idColumn = cell.Address.ColumnNumber;
                else if (name == "Scale") scaleColumn = cell.Address.ColumnNumber;
            }
            if (idColumn == 0 || scaleColumn == 0) return;

            foreach (var usedRow in sheet.RowsUsed().Skip(1))
            {
                var idCell = usedRow.Cell(idColumn);
                if (idCell.IsEmpty()) continue;
                scaleByCityId[idCell.GetValue<int>()] = usedRow.Cell(scaleColumn).GetString();
            }
        }

        // 사람이 입력한 스트링 행은 절대 건드리지 않고, 시트에 없는 Id에만 자리표시자 행을 덧붙인다.
        private static void AppendMissingRows(XLWorkbook workbook, string sheetName, string[] headers, IEnumerable<int> ids, System.Func<int, object[]> buildRow)
        {
            var sheet = workbook.Worksheets.TryGetWorksheet(sheetName, out var existing) ? existing : AddSheet(workbook, sheetName, headers);

            var knownIds = new HashSet<int>();
            foreach (var usedRow in sheet.RowsUsed().Skip(1))
            {
                var idCell = usedRow.Cell(1);
                if (!idCell.IsEmpty()) knownIds.Add(idCell.GetValue<int>());
            }

            var nextRow = (sheet.LastRowUsed()?.RowNumber() ?? 1) + 1;
            foreach (var id in ids)
            {
                if (knownIds.Contains(id)) continue;
                var values = buildRow(id);
                for (var i = 0; i < values.Length; i++)
                {
                    sheet.Cell(nextRow, i + 1).Value = XLCellValue.FromObject(values[i]);
                }
                nextRow++;
            }
        }
    }
}
#endif
