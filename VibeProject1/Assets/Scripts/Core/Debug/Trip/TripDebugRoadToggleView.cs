#if UNITY_EDITOR
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Core.DebugTools
{
    /// <summary>
    /// 지도 위 도시 드래그의 의미를 "이동"과 "선 긋기" 사이에서 전환하는 on/off 토글. 실제 상태 보관은
    /// TripDebugRoadModeController(TripMapInteractionCoordinator 소유)가 하고, 이 컴포넌트는 클릭 중계와
    /// 시각 표시만 담당한다.
    /// </summary>
    public class TripDebugRoadToggleView : MonoBehaviour
    {
        [SerializeField] private Button toggleButton;
        [SerializeField] private TMP_Text label;

        public void Initialize(Action toggleClicked)
        {
            if (toggleButton == null)
            {
                return;
            }

            toggleButton.onClick.RemoveAllListeners();
            toggleButton.onClick.AddListener(() => toggleClicked?.Invoke());
        }

        public void SetActiveVisual(bool isActive)
        {
            if (label != null)
            {
                label.text = isActive ? "경로 연결: ON" : "경로 연결: OFF";
            }
        }
    }
}
#endif
