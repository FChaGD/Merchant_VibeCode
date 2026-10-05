using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 디버그 핀 한 개(판 칸 + 영역 모양 - 마차와 같은 규칙, Docs/기획/59번 §4.5·65번 §3.3). 영역 계산의 선택적 입력이다.
    /// </summary>
    public readonly struct FormationAreaPin
    {
        public int SlotIndex { get; }
        public FormationAreaShape Shape { get; }

        public FormationAreaPin(int slotIndex, FormationAreaShape shape)
        {
            SlotIndex = slotIndex;
            Shape = shape ?? FormationAreaShape.Single;
        }
    }

    /// <summary>
    /// 외곽 판 위의 대열 영역(Docs/기획/59번, 설계 60번 §3). 배치가 바뀔 때마다 새로 계산하는 불변 스냅샷이다 - 판 2,500칸·유닛 수십 개
    /// 수준이라 편집 동작마다 다시 계산해도 충분하고, 캐시를 두면 배치와 어긋날 위험만 생긴다.
    /// - 대열 칸: 마차가 없으면 기준 칸(판 중앙) + 핀 영역, 있으면 마차·시설·핀의 영역 모양(기준 칸을 유닛 칸에 맞춘 마스크)의 합집합(판 밖은
    ///   잘림). 반경 → 상하좌우 직사각형(기획 61번) → 마스크 모양(기획 65번)으로 바뀌었다. 떨어진 조각도 대열·연결 칸이다.
    /// - 시설 자리(자리 칸): 마차·핀 영역 안만. 시설이 넓힌 칸까지 허용하면 시설을 자기 영역 끝으로 반복 이동해 대열에서 끝없이 떨어져 나갈 수
    ///   있다(2026-09-29 실전 확인, 사용자 결정).
    /// - 연결 판정(연결 칸): 자리 칸 + 자리 칸 위 시설의 영역(2026-09-29 개정, 기획 59번 §3.3 - 시설도 마차를 잇는다). 4방향으로 맞닿은 칸을
    ///   한 덩어리로 보고, 모든 마차가 한 덩어리에 있으면 연결.
    /// - 덩어리 라벨링(설계 79번 §5.5): 연결 여부(bool)만으로는 "끊어진 대열을 더 끊지만 않으면 허용"(ConnectivityRule.NoNewSplit)과
    ///   "가장 큰 덩어리 외 붉은 표시"를 판정할 수 없어, 연결 칸 전체를 덩어리 번호로 나눈다. 번호는 칸 번호가 작은 칸부터 매겨 같은 배치면
    ///   항상 같은 번호가 나온다(가장 큰 덩어리 동률 판정이 실행마다 달라지지 않게).
    /// - 자리 칸 밖 시설은 대열·연결 어디에도 기여하지 않는다(설계 60번 §14.2) - 곧 해제될 시설이 계산상 연결을 이어 주면 판정 순서에 따라
    ///   결과가 달라진다. 그런 시설은 FormationAreaRules.Validate가 해제한다.
    /// </summary>
    public sealed class FormationArea
    {
        private readonly HashSet<int> cells = new();
        private readonly HashSet<int> pinCells = new();
        // 마차·핀 영역 - 시설을 놓을 수 있는 칸.
        private readonly HashSet<int> hostCells = new();
        // 자리 칸 + 자리 칸 위 시설의 영역 - 마차 연결 판정.
        private readonly HashSet<int> connectCells = new();
        // 마차만 / 시설만의 영역 합집합 - 판정에는 쓰지 않고 디버그 외곽선 표시용으로만 노출한다.
        private readonly HashSet<int> wagonCells = new();
        private readonly HashSet<int> facilityCells = new();
        private readonly List<int> wagonSlots = new();
        // 연결 칸 → 덩어리 번호. 마차가 없는 덩어리(핀·시설 영역만)도 번호를 받는다.
        private readonly Dictionary<int, int> componentBySlot = new();

        public int ColumnCount { get; }
        public int RowCount { get; }
        public int AnchorSlotIndex { get; }
        public IReadOnlyCollection<int> Cells => cells;
        public int WagonCount => wagonSlots.Count;
        public bool WagonsConnected => ComponentCount <= 1;
        /// <summary>마차가 속한 덩어리 수. 마차가 0~1대면 1(끊어질 대상이 없음).</summary>
        public int ComponentCount { get; }
        /// <summary>마차가 가장 많은 덩어리(동률이면 칸이 많은 쪽, 그래도 같으면 번호가 작은 쪽). 마차가 없으면 -1.</summary>
        public int LargestComponentId { get; }
        public RectInt Bounds { get; }
        /// <summary>마차 칸 열·행의 평균(마차가 없으면 기준 칸) - 전투 대형 중심(설계 60번 §11-4).</summary>
        public Vector2 WagonCenter { get; }

        private FormationArea(FormationLayout layout, Func<string, IFormationUnit> lookup, IReadOnlyList<FormationAreaPin> pins)
        {
            ColumnCount = layout.ColumnCount;
            RowCount = layout.RowCount;
            AnchorSlotIndex = layout.AnchorSlotIndex;

            var facilityAnchors = new List<(int slot, FormationAreaShape shape)>();
            var wagonAnchors = new List<(int slot, FormationAreaShape shape)>();
            for (var slot = 0; slot < layout.SlotCount; slot++)
            {
                var id = layout.GetUnitId(slot);
                if (string.IsNullOrEmpty(id)) continue;

                var unit = lookup?.Invoke(id);
                if (unit == null || unit.Kind == FormationUnitKind.Character) continue;

                var shape = unit is IAreaAnchorUnit anchor ? anchor.AreaShape : FormationAreaShape.Single;
                if (unit.Kind == FormationUnitKind.Wagon)
                {
                    wagonSlots.Add(slot);
                    wagonAnchors.Add((slot, shape));
                }
                else
                {
                    facilityAnchors.Add((slot, shape));
                }
            }

            if (pins != null)
            {
                foreach (var pin in pins)
                {
                    if (!IsOnBoard(pin.SlotIndex)) continue;
                    AddShape(pin.SlotIndex, pin.Shape, pinCells);
                    AddShape(pin.SlotIndex, pin.Shape, hostCells);
                }
            }

            if (wagonSlots.Count == 0)
            {
                cells.Add(AnchorSlotIndex);
            }
            else
            {
                foreach (var (slot, shape) in wagonAnchors)
                {
                    AddShape(slot, shape, hostCells);
                    AddShape(slot, shape, wagonCells);
                }
                // 자리 칸이 모두 정해진 뒤에 판별한다 - 시설끼리 서로를 자리로 삼지 못하게.
                foreach (var (slot, shape) in facilityAnchors)
                {
                    if (hostCells.Contains(slot)) AddShape(slot, shape, facilityCells);
                }
                cells.UnionWith(wagonCells);
                cells.UnionWith(facilityCells);
            }
            cells.UnionWith(pinCells);
            connectCells.UnionWith(hostCells);
            connectCells.UnionWith(facilityCells);

            var componentCellCounts = LabelComponents();
            var (componentCount, largestComponentId) = SummarizeWagonComponents(componentCellCounts);
            ComponentCount = componentCount;
            LargestComponentId = largestComponentId;
            Bounds = ComputeBounds();
            WagonCenter = ComputeWagonCenter();
        }

        public static FormationArea Compute(FormationLayout layout, Func<string, IFormationUnit> lookup, IReadOnlyList<FormationAreaPin> pins = null)
            => new(layout, lookup, pins);

        public bool Contains(int slotIndex) => cells.Contains(slotIndex);

        public bool IsPinCell(int slotIndex) => pinCells.Contains(slotIndex);

        public bool IsWagonCell(int slotIndex) => wagonCells.Contains(slotIndex);

        public bool IsFacilityCell(int slotIndex) => facilityCells.Contains(slotIndex);

        /// <summary>이 칸이 속한 덩어리 번호. 연결 칸이 아니면 -1.</summary>
        public int GetComponentOf(int slotIndex) => componentBySlot.TryGetValue(slotIndex, out var id) ? id : -1;

        /// <summary>이 유닛이 이 칸에 있을 수 있는지 - 시설은 자리 칸(마차·핀 영역) 안만, 나머지는 대열 칸 전체.</summary>
        public bool CanHost(IFormationUnit unit, int slotIndex)
            => unit != null && unit.Kind == FormationUnitKind.Facility ? hostCells.Contains(slotIndex) : cells.Contains(slotIndex);

        /// <summary>
        /// 전투로 옮길 대열 범위(설계 60번 §7.2, §11-4): 중심 = 마차 중심값, 반폭 = 중심에서 대열 경계 상자 가장자리까지의 최대 거리.
        /// </summary>
        public FormationExtent ToExtent()
        {
            var halfColumns = Mathf.Max(WagonCenter.x - Bounds.xMin, Bounds.xMax - 1 - WagonCenter.x);
            var halfRows = Mathf.Max(WagonCenter.y - Bounds.yMin, Bounds.yMax - 1 - WagonCenter.y);
            return new FormationExtent(WagonCenter.x, WagonCenter.y, Mathf.Max(0f, halfColumns), Mathf.Max(0f, halfRows));
        }

        private bool IsOnBoard(int slotIndex) => slotIndex >= 0 && slotIndex < ColumnCount * RowCount;

        // 기준 칸을 유닛 칸에 맞춰 모양의 대열 칸을 더한다. 격자 화면이 행을 위→아래로 놓아 행 차 음수 = 위쪽.
        private void AddShape(int slotIndex, FormationAreaShape shape, HashSet<int> target)
        {
            var column = slotIndex % ColumnCount;
            var row = slotIndex / ColumnCount;
            foreach (var offset in shape.Offsets)
            {
                TryAdd(column + offset.x, row + offset.y, target);
            }
        }

        private void TryAdd(int column, int row, HashSet<int> target)
        {
            if (column < 0 || column >= ColumnCount || row < 0 || row >= RowCount) return;
            target.Add(row * ColumnCount + column);
        }

        // 연결 칸을 4방향 BFS로 덩어리 번호를 매긴다. 칸 번호 순으로 시작 칸을 골라 번호가 배치에 대해 결정적이다. 반환: 덩어리별 칸 수.
        private List<int> LabelComponents()
        {
            var cellCounts = new List<int>();
            var queue = new Queue<int>();
            for (var start = 0; start < ColumnCount * RowCount; start++)
            {
                if (!connectCells.Contains(start) || componentBySlot.ContainsKey(start)) continue;

                var id = cellCounts.Count;
                var count = 0;
                componentBySlot[start] = id;
                queue.Enqueue(start);
                while (queue.Count > 0)
                {
                    var current = queue.Dequeue();
                    count++;
                    var column = current % ColumnCount;
                    var row = current / ColumnCount;
                    Visit(column - 1, row, id);
                    Visit(column + 1, row, id);
                    Visit(column, row - 1, id);
                    Visit(column, row + 1, id);
                }
                cellCounts.Add(count);
            }
            return cellCounts;

            void Visit(int column, int row, int id)
            {
                if (column < 0 || column >= ColumnCount || row < 0 || row >= RowCount) return;
                var index = row * ColumnCount + column;
                if (connectCells.Contains(index) && !componentBySlot.ContainsKey(index))
                {
                    componentBySlot[index] = id;
                    queue.Enqueue(index);
                }
            }
        }

        // 마차 칸의 덩어리 번호로 덩어리 수·가장 큰 덩어리를 구한다. 마차 칸은 자기 모양의 기준 칸이라 항상 연결 칸이지만, 혹시 빠지면
        // 연결되지 않은 것으로 본다(이전 bool 판정과 같은 결과) - 그 마차 하나만의 덩어리로 센다.
        private (int count, int largest) SummarizeWagonComponents(List<int> cellCounts)
        {
            if (wagonSlots.Count == 0) return (1, -1);

            var wagonsByComponent = new Dictionary<int, int>();
            var nextIsolatedId = cellCounts.Count;
            foreach (var slot in wagonSlots)
            {
                var id = componentBySlot.TryGetValue(slot, out var labeled) ? labeled : nextIsolatedId++;
                wagonsByComponent[id] = wagonsByComponent.TryGetValue(id, out var wagons) ? wagons + 1 : 1;
            }

            var largest = -1;
            foreach (var pair in wagonsByComponent)
            {
                var id = pair.Key;
                var wagons = pair.Value;
                if (largest < 0) { largest = id; continue; }

                var largestWagons = wagonsByComponent[largest];
                var cellsOfId = id < cellCounts.Count ? cellCounts[id] : 1;
                var cellsOfLargest = largest < cellCounts.Count ? cellCounts[largest] : 1;
                if (wagons > largestWagons
                    || (wagons == largestWagons && (cellsOfId > cellsOfLargest || (cellsOfId == cellsOfLargest && id < largest))))
                {
                    largest = id;
                }
            }
            return (wagonsByComponent.Count, largest);
        }

        private RectInt ComputeBounds()
        {
            int minColumn = int.MaxValue, minRow = int.MaxValue, maxColumn = int.MinValue, maxRow = int.MinValue;
            foreach (var cell in cells)
            {
                var column = cell % ColumnCount;
                var row = cell / ColumnCount;
                minColumn = Mathf.Min(minColumn, column);
                maxColumn = Mathf.Max(maxColumn, column);
                minRow = Mathf.Min(minRow, row);
                maxRow = Mathf.Max(maxRow, row);
            }
            return new RectInt(minColumn, minRow, maxColumn - minColumn + 1, maxRow - minRow + 1);
        }

        private Vector2 ComputeWagonCenter()
        {
            if (wagonSlots.Count == 0) return new Vector2(AnchorSlotIndex % ColumnCount, AnchorSlotIndex / ColumnCount);

            var sum = Vector2.zero;
            foreach (var slot in wagonSlots) sum += new Vector2(slot % ColumnCount, slot / ColumnCount);
            return sum / wagonSlots.Count;
        }
    }
}
