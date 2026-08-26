namespace Game.Core
{
    /// <summary>
    /// UIManager.Open/Close(panelId)로 제어되는 UI 패널의 공통 계약.
    /// </summary>
    public interface IUIPanel
    {
        string PanelId { get; }
        void Open();
        void Close();
    }
}
