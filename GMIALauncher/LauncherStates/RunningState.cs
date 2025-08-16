using AOULauncher.Views;

namespace AOULauncher.LauncherStates;

public class RunningState(MainWindow window) : LoadingState(window)
{
    public override void EnterState()
    {
        base.EnterState();
        Window.InstallText.Text = "正在运行";
        Window.ProgressBar.ProgressTextFormat = "正在运行...";
    }
}