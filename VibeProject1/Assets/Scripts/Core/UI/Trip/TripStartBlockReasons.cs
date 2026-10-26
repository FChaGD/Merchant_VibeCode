using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>
    /// "상행 시작"을 막는 사유 목록. 버튼 활성 판정과 툴팁 문구가 같은 계산에서 나와야 어긋나지 않으므로
    /// 둘 다 이 목록만 본다 - 비어 있으면 출발 가능(Docs/설계/100번 §4.2). 순서는 기획 97 4-2(B → C → A).
    /// </summary>
    public static class TripStartBlockReasons
    {
        public const string NoDestination = "도착지를 지정하세요.";
        public const string Disconnected = "대열이 끊어져 있습니다. 정비창에서 마차를 이어 붙이세요.";
        public const string SceneNotRevealed = "화면 전환 중입니다.";

        public static IReadOnlyList<string> Collect(bool sceneRevealed, bool hasDestination, bool formationConnected)
        {
            var reasons = new List<string>(3);
            if (!hasDestination) reasons.Add(NoDestination);
            if (!formationConnected) reasons.Add(Disconnected);
            if (!sceneRevealed) reasons.Add(SceneNotRevealed);
            return reasons;
        }
    }
}
