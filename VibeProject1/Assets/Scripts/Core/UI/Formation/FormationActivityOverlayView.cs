using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.Core
{
    /// <summary>
    /// 정비창 슬롯 위에 표시하는 배치/이동 진행 표시(Docs/기획/20번 §3.2/§3.3, 설계 25번 §5.1) -
    /// 반투명 유닛 아이콘 + 잔여 초 텍스트. Field 배치 UI에서만 쓰인다(Hub는 진행 활동이 없어 항상
    /// 비활성). FormationSlotView의 자식이 아니라 FormationGridView가 오버레이 컨테이너 아래 별도
    /// 풀(get-or-create)로 관리한다 - 렌더 순서(슬롯 배경 < 경로선 < 이 마크 < 이동 아이콘, 실전
    /// 확인)를 종류별 컨테이너의 형제 순서로 고정하므로 슬롯 자식으로 둘 수 없다.
    /// 이동 중인 활동의 "도착" 마크에 한해 드래그로 목적지를 바꿀 수 있다(기획 21번, 설계 26번
    /// §5.2) - FormationUnitIconView와 동일하게 실제 판단은 하지 않고 델리게이트에 그대로
    /// 위임한다. 리다이렉트 불가능한 마크(출발/배치)는 Bind에서 델리게이트가 null로 들어와
    /// 드래그 입력을 그냥 무시한다(raycastTarget도 꺼서 아래 슬롯 아이콘의 입력을 가리지 않는다).
    /// </summary>
    public class FormationActivityOverlayView : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [SerializeField] private Image iconImage;
        [SerializeField] private TMP_Text remainingSecondsText;

        private Action<string, PointerEventData> onBeginDrag;
        private Action<PointerEventData> onDrag;
        private Action<PointerEventData> onEndDrag;
        private string redirectUnitId;
        private int lastShownSeconds = -1;

        public void Bind(Sprite icon, float remainingSeconds, bool isRedirectable = false, string unitId = null,
            Action<string, PointerEventData> beginDragHandler = null, Action<PointerEventData> dragHandler = null, Action<PointerEventData> endDragHandler = null,
            bool showRemainingSeconds = true)
        {
            if (iconImage != null)
            {
                iconImage.sprite = icon;
                iconImage.enabled = icon != null;
                iconImage.raycastTarget = isRedirectable;
            }

            if (remainingSecondsText != null)
            {
                // 이동 중인 활동은 출발/도착 마크 둘 다 같은 활동의 잔여시간을 공유하므로, 도착
                // 쪽에만 표시한다(showRemainingSeconds, 2026-09-07 사용자 확정 - 중복 표기 정리).
                remainingSecondsText.gameObject.SetActive(showRemainingSeconds);
                // 매 프레임 불리므로 표시 정수가 바뀔 때만 문구를 만든다(Docs/Refactor/2026-10-26_공통.md O-4). 다른 활동에 재사용돼도 같은 숫자면 같은 문구다.
                var shown = ToDisplaySeconds(remainingSeconds);
                if (showRemainingSeconds && shown != lastShownSeconds)
                {
                    remainingSecondsText.text = FormatDisplaySeconds(shown);
                    lastShownSeconds = shown;
                }
            }

            redirectUnitId = isRedirectable ? unitId : null;
            onBeginDrag = isRedirectable ? beginDragHandler : null;
            onDrag = isRedirectable ? dragHandler : null;
            onEndDrag = isRedirectable ? endDragHandler : null;
        }

        // 1분 미만은 ss, 1분 이상은 mm:ss(사용자 확정, 2026-09-07) - 초 단위 접미사("초") 없이
        // 시계 표기 그대로 보여준다.
        public static int ToDisplaySeconds(float remainingSeconds) => Mathf.CeilToInt(Mathf.Max(0f, remainingSeconds));

        public static string FormatDisplaySeconds(int totalSeconds)
        {
            if (totalSeconds < 60)
            {
                return totalSeconds.ToString("00");
            }
            var minutes = totalSeconds / 60;
            var seconds = totalSeconds % 60;
            return $"{minutes}:{seconds:00}";
        }

        public void OnBeginDrag(PointerEventData eventData) => onBeginDrag?.Invoke(redirectUnitId, eventData);

        public void OnDrag(PointerEventData eventData) => onDrag?.Invoke(eventData);

        public void OnEndDrag(PointerEventData eventData) => onEndDrag?.Invoke(eventData);
    }
}
