using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Net.Http.Handlers;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using AOULauncher.LauncherStates;
using AOULauncher.Tools;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Color = System.Drawing.Color;

namespace AOULauncher.Views;

public partial class MainWindow : Window
{
    public HttpClient HttpClient { get; }

    private AbstractLauncherState _launcherState;

    public AbstractLauncherState LauncherState
    {
        get => _launcherState;
        set
        {
            _launcherState = value;
            _launcherState.EnterState();
        }
    }

    public LauncherConfig Config;

    public MainWindow()
    {
        InitializeComponent();
        // Load config
        Config = File.Exists(Constants.ConfigPath)
            ? JsonSerializer.Deserialize(File.ReadAllText(Constants.ConfigPath), LauncherConfigContext.Default.LauncherConfig)
            : new LauncherConfig();
        
        var progressHandler = new ProgressMessageHandler(new HttpClientHandler {AllowAutoRedirect = true});
        progressHandler.HttpReceiveProgress += (_, args) => {
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                ProgressBar.Value = args.ProgressPercentage;
            });
        };

        HttpClient = new HttpClient(progressHandler, true);        
        RemoveOutdatedLauncher();

        _launcherState = new InstallState(this);
        _launcherState.EnterState();
        // start downloading launcher data and load among us path to check for mod installation
        Task.Run(DownloadData);
    }

    private async Task DownloadData()
    {
        try
        {
            Config.ModPackData = await HttpClient.DownloadJson(Constants.ApiLocation, LauncherConfigContext.Default.ModPackData);

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (!string.IsNullOrEmpty(Config.ModPackData.SupportedVersion))
                {
                    VersionTextBlock.Text = $"当前模组支持的AmongUs版本：{Config.ModPackData.SupportedVersion}\n请注意你所选择的版本";
                }
            });

            await CheckLauncherUpdate();
            await Dispatcher.UIThread.InvokeAsync(LoadAmongUsPath);
        }
        catch (Exception e)
        {
            var window = new Error(e.Message);
            window.Show();
            window.Activate();
        }
    }

    private static void RemoveOutdatedLauncher()
    {
        var outdated = new FileInfo(Path.Combine(AppContext.BaseDirectory,"AOULauncher.exe.old"));

        if (outdated.Exists)
        {
            outdated.Delete();
        }
    }
    
    private async Task CheckLauncherUpdate()
    {
        if (!Version.TryParse(Config.ModPackData.LatestLauncherVersion, out var version))
        {
            return;
        }

        var assembly = Assembly.GetExecutingAssembly();
        await Console.Out.WriteLineAsync(assembly.GetName().Version +", "+version); 
        if (version <= assembly.GetName().Version)
        {
            return;
        }

        var file = new FileInfo(Path.Combine(AppContext.BaseDirectory,"AOULauncher.exe"));

        if (!file.Exists)
        {
            return;
        }

        file.MoveTo(file.FullName+".old", true);

        await HttpClient.DownloadFile("AOULauncher.zip", AppContext.BaseDirectory, Config.ModPackData.LauncherUpdateLink);
        var zipFile = ZipFile.OpenRead("AOULauncher.zip");
        zipFile.ExtractToDirectory(AppContext.BaseDirectory, true);

        Process.Start(Path.Combine(AppContext.BaseDirectory, "AOULauncher.exe"));
        Process.GetCurrentProcess().Kill();
    }

    //private string _currentVersion = "未知";
    //private bool _versionMatch = false;

    public void LoadAmongUsPath()
    {
        Console.Out.WriteLine("Loading Among Us Path");
        ProgressBar.Value = 0;
        ProgressBar.ProgressTextFormat = "正在加载...";

        if (!AmongUsLocator.VerifyAmongUsDirectory(Config.AmongUsPath))
        {
            if (AmongUsLocator.FindAmongUs() is { } path)
            {
                Config.AmongUsPath = path;
            }
            else
            {
                Console.Out.WriteLine("no among us detected");
                LauncherState = new RefreshState(this);
                return;
            }
        }


        /*_currentVersion = AmongUsLocator.ParseVersion(Config.AmongUsPath);
        CurrentVersionTextBlock.Text = $"当前AmongUs版本：{_currentVersion}";
        _versionMatch = _currentVersion == Config.ModPackData.SupportedVersion;

        if (_versionMatch)
        {
            VersionStatusTextBlock.Text = "✓ 版本匹配";
            VersionStatusTextBlock.Foreground = Brush.Parse("#00FF00");
        }
        else if (Config.ModPackData.SupportedVersion != null)
        {
            VersionStatusTextBlock.Text = "✗ 版本不匹配";
            VersionStatusTextBlock.Foreground = Brush.Parse("#FF0000");
        }
        else
        {
            VersionStatusTextBlock.Text = "⚠ 无法获取支持版本";
            VersionStatusTextBlock.Foreground = Brush.Parse("#FFBB00");
        }*/

        ProgressBar.ProgressTextFormat = "";

        var bepInExPlugins = new DirectoryInfo(Path.Combine(Constants.ModFolder, "BepInEx", "plugins"));

        if (!bepInExPlugins.Exists)
        {
            LauncherState = new InstallState(this);
            return;
        }

        var filesPresent = true;
        var updateRequired = false;

        foreach (var info in Config.ModPackData.ModList)
        {
            var pluginPath = Path.Combine(bepInExPlugins.FullName, info.Name);

            var updated = Utilities.IsPluginUpdated(pluginPath, info.Hash, out var exists);

            if (!exists)
            {
                Console.Out.WriteLine($"Missing {info.Name}");
                filesPresent = false;
            }

            if (!updated)
            {
                Console.Out.WriteLine($"Out of date: {info.Name}");
                updateRequired = true;
            }
        }

        if (filesPresent)
        {
            LauncherState = updateRequired ? new UpdateState(this) : new LaunchState(this);
        }
        else
        {
            LauncherState = new InstallState(this);
        }
    }

    public async void InstallClickHandler(object sender, RoutedEventArgs args)
    {
        /*if (!_versionMatch && InstallText.Text != "刷新")
        {
            var dialog = new Error($"Among Us版本不匹配！\n当前版本: {_currentVersion}\n需要版本: {Config.ModPackData.SupportedVersion}");
            await dialog.ShowDialog(this);
            return;
        }*/

        ProgressBar.Value = 0;
        await LauncherState.ButtonClick();
    }

    public void SetInfoToPath()
    {
        InfoIcon.IsVisible = false;
        InfoText.Foreground = Brush.Parse("#555");
        InfoText.Text = Config.AmongUsPath;
    }

    public void AmongUsOnExit()
    {
        LaunchWarning.IsVisible = false;
        WindowState = WindowState.Normal;
        Topmost = true;
        Topmost = false;
        Activate();
        Show();
        
        LauncherState = new LoadingState(this);
        Uninstall();
    }
    
    private void Uninstall()
    {
        Console.Out.WriteLine("Uninstalling");

        var doorstopBackup = new FileInfo(Path.Combine(Config.AmongUsPath, "doorstop_config.ini.bak"));
        if (doorstopBackup.Exists)
        {
            Console.Out.WriteLine("Restoring doorstop config");
            var doorstopConfig = new FileInfo(Path.Combine(Config.AmongUsPath, "doorstop_config.ini"));
            if (doorstopConfig.Exists)
            {
                doorstopConfig.Delete();
            }

            doorstopBackup.MoveTo(Path.Combine(Config.AmongUsPath, "doorstop_config.ini"));
        }
        else
        {
            Console.Out.WriteLine("No doorstop config backup found, uninstalling completely");
            foreach (var file in Constants.UninstallPaths)
            {
                var info = new FileInfo(Path.Combine(Config.AmongUsPath, file));
                if (info.Exists)
                {
                    info.Delete();
                }
            }
        }

        Console.Out.WriteLine("Uninstall complete, reloading AU path");
        LoadAmongUsPath();
    }
    
    private async void OpenDirectoryPicker(object? _, RoutedEventArgs e)
    {
        if (LauncherState is LoadingState or RunningState)
        {
            return;
        }
        
        var picked = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "启动 Among Us.exe",
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("Among Us"){Patterns = ["Among Us.exe"]}]
        });
        
        if (picked.Count <= 0)
        {
            return;
        }

        var file = new FileInfo(picked[0].Path.LocalPath);
        
        if (file.Directory is not null && AmongUsLocator.VerifyAmongUsDirectory(file.Directory.FullName))
        {
            Config.AmongUsPath = file.Directory.FullName;
            LoadAmongUsPath();
        }

    }

    private async void DiscordLinkOnClick(object? _, RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo("https://fangkuai.fun") {UseShellExecute = true});
        var clipboard = GetTopLevel(this)?.Clipboard;
        var dataObject = new DataObject();
        dataObject.Set(DataFormats.Text, "https://fangkuai.fun");
        if (clipboard is not null)
        {
            await clipboard.SetDataObjectAsync(dataObject);
        }
    }

    private void PointerDown(object? _, PointerPressedEventArgs e)
    {
        BeginMoveDrag(e);
    }
}