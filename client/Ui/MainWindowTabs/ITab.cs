namespace OrangeGuidanceTomestone.Ui.MainWindowTabs;

public interface ITab {
    public string Name { get; }
    public void Draw();
}
