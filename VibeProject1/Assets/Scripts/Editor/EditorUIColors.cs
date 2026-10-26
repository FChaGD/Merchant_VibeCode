using UnityEngine;

namespace Game.Core.Editor
{
    /// <summary>
    /// 마을 시설 화면·팝업 조립이 함께 쓰는 UI 색(Docs/Refactor/2026-10-26_공통.md D-3). 같은 RGB 값이 조립 클래스마다 상수로 복사돼 있어
    /// 색을 바꾸려면 여러 곳을 고쳐야 했다 - 값은 현행 그대로다.
    /// </summary>
    internal static class EditorUIColors
    {
        public static readonly Color Panel = new(0.96f, 0.95f, 0.92f, 1f);
        public static readonly Color Header = new(0.72f, 0.66f, 0.55f, 1f);
        public static readonly Color List = new(0.9f, 0.88f, 0.84f, 1f);
        public static readonly Color Row = new(1f, 1f, 1f, 1f);
        public static readonly Color Button = new(0.85f, 0.85f, 0.85f, 1f);
        public static readonly Color SelectionOutline = new(0.1f, 0.1f, 0.1f, 1f);
        public static readonly Color Dim = new(0f, 0f, 0f, 0.5f);
    }
}
