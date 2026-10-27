using Game.Core;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Core.Editor
{
    /// <summary>
    /// 상행 준비 화면 배치 상수 - 게임 화면 조립(TripUIBuilder)과 지도 디버그 편집 UI(TripMapEditorInstaller)가 같은 상단 줄을 나눠 쓴다
    /// (Docs/Refactor/2026-10-26_씬전용.md H-2·H-3).
    /// </summary>
    internal static class TripUILayout
    {
        // 지도 저장(디버그)과 닫기 버튼은 상단 줄의 같은 구간(0.64~0.79)을 좌/우로 양분한다 - 예전엔
        // 두 버튼이 0.70~0.78에서 겹쳐 저장 버튼이 닫기 버튼을 가렸다. 간격은 지도(~0.62)와 정보
        // 패널(0.64~) 사이 간격(0.02)을 그대로 따른다.
        public const float TripTopSharedMinX = 0.64f;
        public const float TripTopSharedMaxX = 0.79f;
        public const float TripTopSharedGap = 0.02f;
        public const float TripTopSharedButtonWidth = (TripTopSharedMaxX - TripTopSharedMinX - TripTopSharedGap) / 2f;

        // 상단 버튼 줄(디버그 지도 컨트롤/닫기/배치)의 세로 범위. 우상단 재화 HUD(상단 마진 32px +
        // 높이 약 40px = 화면 위 72px까지, ConstantPixelSize)가 예전 0.90~0.97 줄을 가려서 아래로
        // 내렸다 - 1080p 기준 줄 윗변은 화면 위에서 약 130px로 HUD 하단과 여유가 있다. 본문(지도/
        // 정보 패널) 윗변은 이 줄에서 기존 간격 0.02를 둔 TripContentTopY로 함께 압축했다.
        public const float TripTopRowMinY = 0.81f;
        public const float TripTopRowMaxY = 0.88f;
        public const float TripContentTopY = 0.79f;
    }
}
