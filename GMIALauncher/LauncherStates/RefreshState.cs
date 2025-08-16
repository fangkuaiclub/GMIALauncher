using System.Threading.Tasks;
using AOULauncher.Views;
using Avalonia.Controls.Documents;
using Avalonia.Media;

namespace AOULauncher.LauncherStates;

public class RefreshState(MainWindow window) : AbstractLauncherState(window)
{
    public override Task ButtonClick()
    {
        Window.LauncherState = new LoadingState(Window);
        Window.LoadAmongUsPath();
        return Task.CompletedTask;
    }

    public override void EnterState()
    {
        Window.InstallButton.IsEnabled = true;
        Window.InstallText.Text = "刷新";
        Window.InfoIcon.IsVisible = true;
        Window.InfoText.Foreground = Brush.Parse("#FFBB00");
        Window.InfoText.Text = "";
        Window.InfoText.Inlines?.Clear();
        Window.InfoText.Inlines?.Add("已找到Among Us但版本不匹配或状态异常\n请点击");
        Window.InfoText.Inlines?.Add(new Run("此处") { FontWeight = FontWeight.SemiBold });
        Window.InfoText.Inlines?.Add("手动选择或刷新");
    }
}
