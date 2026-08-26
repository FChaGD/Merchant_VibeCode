using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.Core
{
    /// <summary>
    /// 배치 UI의 그리드 타일 영역. 정사각형 타일이 X열 x Y행으로 틈 없이 붙어 배치되며, 열/행 수와
    /// 타일 크기는 인스펙터 또는 SetGridDimensions/SetSlotSize로 조절 가능하다. 화면 밖 타일은
    /// (Grid 루트에 붙은) ScrollRect의 드래그로 가로/세로 모두 이동해 볼 수 있다. 실제 배치 데이터
    /// (FormationLayout)는 FormationPanel이 소유하며, 이 클래스는 렌더링과 드래그/드롭 이벤트 중계만 담당한다.
    /// </summary>
    public class FormationGridView : MonoBehaviour
    {
        [SerializeField] private Transform slotContent;
        [SerializeField] private GridLayoutGroup slotLayoutGroup;
        [SerializeField] private FormationSlotView slotPrefab;
        [SerializeField] private FormationUnitIconView occupantIconPrefab;
        [SerializeField, Min(0)] private int columnCount = FormationLayout.DefaultColumnCount;
        [SerializeField, Min(0)] private int rowCount = 2;
        [SerializeField] private Vector2 slotSize = new(120f, 120f);
        [SerializeField, Min(0)] private int overscrollTileMargin = 5;

        private readonly List<FormationSlotView> slots = new();
        private ScrollRect scrollRect;

        private Action<int> onSlotDropped;
        private Action<IFormationUnit> onIconClicked;
        private Action<int, FormationUnitIconView, PointerEventData> onIconBeginDrag;
        private Action<PointerEventData> onIconDrag;
        private Action<PointerEventData> onIconEndDrag;

        public int ColumnCount => columnCount;
        public int RowCount => rowCount;
        public int SlotCount => columnCount * rowCount;
        public Vector2 SlotSize => slotSize;

        public void Initialize(
            Action<int> slotDropped,
            Action<IFormationUnit> iconClicked,
            Action<int, FormationUnitIconView, PointerEventData> iconBeginDrag,
            Action<PointerEventData> iconDrag,
            Action<PointerEventData> iconEndDrag)
        {
            onSlotDropped = slotDropped;
            onIconClicked = iconClicked;
            onIconBeginDrag = iconBeginDrag;
            onIconDrag = iconDrag;
            onIconEndDrag = iconEndDrag;

            RebuildSlots();
        }

        /// <summary>
        /// 열(X)/행(Y) 수를 런타임에 조절한다(디버깅 UI 연동 지점). 배치 데이터 재정렬은 호출자(FormationPanel)의 책임이다.
        /// </summary>
        public void SetGridDimensions(int columns, int rows)
        {
            columnCount = Mathf.Max(0, columns);
            rowCount = Mathf.Max(0, rows);
            RebuildSlots();
        }

        /// <summary>
        /// 타일 1칸의 가로/세로 크기를 런타임에 조절한다(디버깅 UI 연동 지점). 정사각형을 기본으로 하되,
        /// 값 자체는 자유롭게 지정할 수 있다.
        /// </summary>
        public void SetSlotSize(Vector2 size)
        {
            slotSize = size;
            ApplyLayoutSettings();
            CenterContentOnTiles();
        }

        // 유닛 유무와 무관하게 슬롯당 아이콘 인스턴스 하나를 계속 재사용한다(파괴 후 재생성 대신
        // Bind로 내용을 덮어쓰고 SetActive로 표시만 전환) - CLAUDE.md의 "슬롯/아이콘 렌더링은 매번
        // Destroy+Instantiate하지 않고 get-or-create로 재사용한다" 규칙을 따른다. 매번 완전히
        // 덮어쓰므로(Bind/SetHandlers) 이전에 어떤 유닛이 있었든 결과는 동일하다.
        public void RenderSlot(int index, IFormationUnit unit)
        {
            if (index < 0 || index >= slots.Count)
            {
                return;
            }

            var slot = slots[index];

            if (unit == null)
            {
                if (slot.CurrentIcon != null)
                {
                    slot.CurrentIcon.gameObject.SetActive(false);
                }
                return;
            }

            if (occupantIconPrefab == null)
            {
                Debug.LogWarning($"{nameof(FormationGridView)}에 {nameof(occupantIconPrefab)}가 지정되어 있지 않다.");
                return;
            }

            if (slot.CurrentIcon == null)
            {
                slot.SetIcon(Instantiate(occupantIconPrefab, slot.IconContainer));
            }

            var icon = slot.CurrentIcon;
            icon.gameObject.SetActive(true);
            icon.Bind(unit);

            var slotIndex = slot.SlotIndex;
            icon.SetHandlers(
                _ => onIconClicked?.Invoke(unit),
                (iconView, eventData) => onIconBeginDrag?.Invoke(slotIndex, iconView, eventData),
                eventData => onIconDrag?.Invoke(eventData),
                eventData => onIconEndDrag?.Invoke(eventData));
        }

        // 슬롯 개수(SlotCount)가 이전 빌드와 같으면 파괴 후 재생성 대신 기존 슬롯에 콜백만 다시
        // 바인딩한다 - Initialize()가 정비창을 열 때마다 호출되는데, 열 때마다 그리드 크기가 바뀌는
        // 것은 아니므로(디버그 리사이즈 때만 실제로 바뀜) 매번 파괴+재생성할 이유가 없다. 실제로
        // 개수가 바뀌는 경우(SetGridDimensions)는 기존과 동일하게 전량 재생성한다.
        private void RebuildSlots()
        {
            if (slots.Count == SlotCount)
            {
                for (var i = 0; i < slots.Count; i++)
                {
                    slots[i].Initialize(i, onSlotDropped);
                }

                ApplyLayoutSettings();
                CenterContentOnTiles();
                return;
            }

            foreach (var slot in slots)
            {
                if (slot != null)
                {
                    Destroy(slot.gameObject);
                }
            }
            slots.Clear();

            if (slotPrefab == null || slotContent == null)
            {
                Debug.LogWarning($"{nameof(FormationGridView)}에 {nameof(slotPrefab)} 또는 {nameof(slotContent)}가 지정되어 있지 않다.");
                return;
            }

            // GridLayoutGroup은 자식을 채우는 순서대로 좌→우, 위→아래로 배치한다(row-major).
            for (var row = 0; row < rowCount; row++)
            {
                for (var col = 0; col < columnCount; col++)
                {
                    var index = row * columnCount + col;
                    var slot = Instantiate(slotPrefab, slotContent);
                    slot.Initialize(index, onSlotDropped);
                    slots.Add(slot);
                }
            }

            ApplyLayoutSettings();
            CenterContentOnTiles();
        }

        /// <summary>
        /// 콘텐츠는 overscrollTileMargin만큼 타일 영역보다 훨씬 크게 만들어져 있어(ApplyLayoutSettings
        /// 참고), 기본 스크롤 위치(좌상단)로 열면 빈 여백부터 보여 타일까지 매번 드래그해야 했다.
        /// 열릴 때마다 실제 타일 영역이 뷰포트 중앙에 오도록 초기 스크롤 위치를 계산해 맞춘다.
        /// </summary>
        private void CenterContentOnTiles()
        {
            if (scrollRect == null)
            {
                scrollRect = GetComponent<ScrollRect>();
            }

            if (scrollRect == null || scrollRect.viewport == null || slotContent is not RectTransform contentRect)
            {
                return;
            }

            var viewportSize = scrollRect.viewport.rect.size;
            var marginX = slotLayoutGroup != null ? slotLayoutGroup.padding.left : 0;
            var marginY = slotLayoutGroup != null ? slotLayoutGroup.padding.top : 0;
            var tileAreaSize = new Vector2(columnCount * slotSize.x, rowCount * slotSize.y);
            var contentSize = tileAreaSize + new Vector2(marginX, marginY) * 2f;

            // content의 anchor/pivot이 좌상단(0,1) 고정이라 anchoredPosition은 "뷰포트 좌상단 대비
            // content 좌상단이 얼마나 밀렸는지"를 뜻한다 - 오른쪽/아래로 스크롤할수록 X는 음수,
            // Y는 양수가 된다(Unity UI는 Y+가 위쪽이라 아래로 스크롤 = content가 위로 밀림).
            var maxOffsetX = Mathf.Max(contentSize.x - viewportSize.x, 0f);
            var maxOffsetY = Mathf.Max(contentSize.y - viewportSize.y, 0f);
            var targetX = viewportSize.x * 0.5f - marginX - tileAreaSize.x * 0.5f;
            var targetY = marginY + tileAreaSize.y * 0.5f - viewportSize.y * 0.5f;

            contentRect.anchoredPosition = new Vector2(
                Mathf.Clamp(targetX, -maxOffsetX, 0f),
                Mathf.Clamp(targetY, 0f, maxOffsetY));
        }

        private void ApplyLayoutSettings()
        {
            if (slotLayoutGroup == null)
            {
                return;
            }

            slotLayoutGroup.cellSize = slotSize;
            slotLayoutGroup.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            slotLayoutGroup.constraintCount = Mathf.Max(1, columnCount);

            // 콘텐츠 여백을 타일 크기 x overscrollTileMargin칸만큼 키워, 타일 영역 밖으로도
            // 그만큼 드래그해서 볼 수 있게 한다(ScrollRect가 Clamped라 콘텐츠 경계까지만 드래그되므로).
            var marginX = Mathf.RoundToInt(slotSize.x * overscrollTileMargin);
            var marginY = Mathf.RoundToInt(slotSize.y * overscrollTileMargin);
            slotLayoutGroup.padding = new RectOffset(marginX, marginX, marginY, marginY);
        }
    }
}
