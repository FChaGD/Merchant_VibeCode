using Game.Core;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Core.Editor
{
    /// <summary>
    /// Hub 루트 버튼 배치 기준(상행 준비 버튼 크기 비율·간격) - 상단 배치/방향성 지시 정렬(Hub 인스톨러)과 인벤토리 상시 호출 버튼 조립이 함께 쓴다
    /// (Docs/Refactor/2026-10-26_씬전용.md H-3 - 조립 클래스가 Hub 인스톨러 내부 상수를 참조하지 않게 뺐다).
    /// </summary>
    internal static class EditorHubLayout
    {
        // 상행 준비 버튼 바로 위 한 줄에 상단 배치(왼쪽) · 방향성 지시(오른쪽)를 나란히 두고, 두 버튼의
        // 좌우 끝을 상행 준비 버튼의 좌우 끝에 맞춘다(사용자 확정, 2026-09-24 - 이전엔 세로로 쌓고 오른쪽만
        // 맞췄다). 상행 준비/상단 배치 버튼은 코드가 아니라 씬에 원래 있던 요소라 좌표를 상수로 박지 않고,
        // 매 실행마다 상행 준비 버튼의 현재 앵커에서 계산한다 - 인스펙터에서 상행 준비 버튼을 옮겨도
        // 재실행하면 나머지 둘이 따라온다.
        // 폭은 (상행 준비 폭 − 간격) / 2로 정해지고, 높이는 기존 버튼(Hub.unity 실측 0.1354×0.1154)과
        // 같은 비율로 함께 줄인다 - 폭만 줄이면 버튼 가로세로 비율이 깨진다(사용자 요구). 간격은 Hub 버튼
        // 간 기존 관례(0.02)를 가로/세로 모두 그대로 쓴다.
        public const float HubActionButtonBaseWidth = 0.1354f;
        public const float HubActionButtonBaseHeight = 0.1154f;
        public const float HubActionButtonGap = 0.02f;
    }
}
