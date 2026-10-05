namespace Game.Core
{
    /// <summary>
    /// 회수 적재 패널 화면 요소 ID(설계 79번 §7). 편집 본문 요소는 InventoryPopupUIElementIds에 InventoryPrefix를 붙여 찾는다.
    /// Root는 InventoryPopupUIElementIds.Root(InventoryPrefix)와 같은 문자열이다 - 패널 루트가 곧 편집 본문 루트(한 오브젝트)라는
    /// 뜻이므로, 두 마커를 서로 다른 오브젝트에 붙이면 SceneUIRoot에서 중복 Id로 하나가 무시된다.
    /// </summary>
    public static class FieldCargoRecoveryUIElementIds
    {
        public const string Root = "Field.CargoRecovery.Root";
        public const string InventoryPrefix = "Field.CargoRecovery";
        public const string DoneButton = "Field.CargoRecovery.DoneButton";
        public const string ConfirmDialog = "Field.CargoRecovery.ConfirmDialog";
    }
}
