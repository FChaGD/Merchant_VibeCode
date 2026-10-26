using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>
    /// 입력 액션 자산(GameInput.inputactions)의 "맵/액션" 경로 상수. 자산 이름이 바뀌면 여기만 고친다.
    /// 텍스트 입력 중 차단은 액션 단위가 아니라 맵 단위로 판정한다 — 키보드 문자 키를 쓰는 맵만 막고
    /// 포인터 맵은 막지 않는다(Docs/설계/99번 §4.1).
    /// </summary>
    public static class GameInputActionIds
    {
        public const string OpenTradeGoods = "Shortcut/OpenTradeGoods";
        public const string OpenEquipment = "Shortcut/OpenEquipment";
        public const string OpenConsumable = "Shortcut/OpenConsumable";
        public const string OpenPersonalItem = "Shortcut/OpenPersonalItem";
        public const string RotateHeldItem = "Inventory/RotateHeldItem";
        public const string PointerPress = "Pointer/Press";

        private const string ShortcutMap = "Shortcut";
        private const string InventoryMap = "Inventory";

        public static IReadOnlyList<string> All { get; } = new[]
        {
            OpenTradeGoods, OpenEquipment, OpenConsumable, OpenPersonalItem, RotateHeldItem, PointerPress,
        };

        public static bool IsBlockedDuringTextInput(string actionId)
        {
            if (string.IsNullOrEmpty(actionId))
                return false;
            int slash = actionId.IndexOf('/');
            if (slash <= 0)
                return false;
            string map = actionId.Substring(0, slash);
            return map == ShortcutMap || map == InventoryMap;
        }
    }
}
