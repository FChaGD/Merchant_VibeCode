using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.Core
{
    /// <summary>
    /// 그리드 타일 1칸. 점유 유닛 아이콘을 담는 컨테이너이자 드롭 대상이다.
    /// 실제 배치 규칙 판단은 편집 정책(IFormationEditingHandler)이 담당하며, 이 클래스는 드롭 이벤트를 그대로 중계한다.
    /// 대열 칸이면 주황 타일, 대열 밖이면 민트색 판 칸으로 칠한다(Docs/기획/59번 §4.4). 격자 바탕은 판 밖 색이라 칸이 직접 칠해야 한다.
    /// </summary>
    public class FormationSlotView : MonoBehaviour, IDropHandler
    {
        private const float RejectFlashSeconds = 0.4f;
        private static readonly Color RejectFlashColor = new(0.9f, 0.15f, 0.1f, 1f);

        [SerializeField] private Transform iconContainer;

        private Action<int> onDropped;
        private Image background;
        private Color areaColor;
        private Color outsideColor = Color.clear;
        private bool inArea = true;
        private Coroutine flashRoutine;

        public int SlotIndex { get; private set; }
        public FormationUnitIconView CurrentIcon { get; private set; }
        public Transform IconContainer => iconContainer != null ? iconContainer : transform;

        private void Awake()
        {
            background = GetComponent<Image>();
            // 프리팹에 칠해 둔 주황색(FormationUIBuilder.SlotBackgroundColor)을 대열 칸 색으로 쓴다.
            if (background != null) areaColor = background.color;
        }

        public void Initialize(int slotIndex, Action<int> dropped)
        {
            SlotIndex = slotIndex;
            onDropped = dropped;
        }

        public void SetIcon(FormationUnitIconView icon)
        {
            CurrentIcon = icon;
        }

        public void SetColors(Color boardCellColor)
        {
            outsideColor = boardCellColor;
        }

        public void SetInArea(bool value)
        {
            inArea = value;
            if (flashRoutine == null) ApplyBaseColor();
        }

        // 제거·이동이 거부됐음을 알린다(설계 60번 §11-5) - 짧게 붉었다가 원래 색으로 돌아온다.
        public void FlashRejected()
        {
            if (background == null || !isActiveAndEnabled) return;
            if (flashRoutine != null) StopCoroutine(flashRoutine);
            flashRoutine = StartCoroutine(FlashRoutine());
        }

        public void OnDrop(PointerEventData eventData)
        {
            onDropped?.Invoke(SlotIndex);
        }

        private void OnDisable()
        {
            flashRoutine = null;
            ApplyBaseColor();
        }

        private IEnumerator FlashRoutine()
        {
            var elapsed = 0f;
            while (elapsed < RejectFlashSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                background.color = Color.Lerp(RejectFlashColor, BaseColor, elapsed / RejectFlashSeconds);
                yield return null;
            }
            flashRoutine = null;
            ApplyBaseColor();
        }

        private Color BaseColor => inArea ? areaColor : outsideColor;

        private void ApplyBaseColor()
        {
            if (background != null) background.color = BaseColor;
        }
    }
}
