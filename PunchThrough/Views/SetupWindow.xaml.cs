using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using PunchThrough.Services;
using File = System.IO.File;

namespace PunchThrough.Views;

public partial class SetupWindow : Window
{
    public bool SetupCompleted { get; private set; }

    private readonly TextBlock[] _stepIcons;
    private double _progressBarMaxWidth;

    public SetupWindow()
    {
        InitializeComponent();
        _stepIcons = new[] { Step1Icon, Step2Icon, Step3Icon, Step4Icon };
        Loaded += async (_, _) =>
        {
            _progressBarMaxWidth = ProgressFill.Parent is Border parent ? parent.ActualWidth : 400;
            await RunSetup();
        };
    }

    private void SetStep(int step, string icon)
    {
        Dispatcher.Invoke(() =>
        {
            _stepIcons[step].Text = icon;
            _stepIcons[step].Foreground = icon == "\u2714"
                ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x4C, 0xAF, 0x50))
                : icon == "\u2716"
                    ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Red)
                    : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x4C, 0xAF, 0x50));
        });
    }

    private void SetProgress(int value)
    {
        Dispatcher.Invoke(() =>
        {
            ProgressFill.Width = _progressBarMaxWidth * value / 100.0;
        });
    }

    private async Task RunSetup()
    {
        try
        {
            var installDir = InstallService.InstallDirectory;
            var installedExe = InstallService.InstalledExePath;

            // Step 1: Copy exe
            SetStep(0, "\u25B8");
            SetProgress(10);
            await Task.Delay(300);

            Directory.CreateDirectory(installDir);
            var currentExe = Environment.ProcessPath!;
            File.Copy(currentExe, installedExe, overwrite: true);

            SetStep(0, "\u2714");
            SetProgress(25);

            // Step 2: Download SpoofDPI
            SetStep(1, "\u25B8");

            if (!SpoofDPIManager.IsInstalled())
            {
                var progress = new Progress<int>(p =>
                    SetProgress(25 + (int)(p * 0.35)));

                var (ok, msg) = await SpoofDPIManager.DownloadAsync(progress);
                if (!ok)
                {
                    SetStep(1, "\u2716");
                    TxtSubtitle.Text = $"Download failed: {msg}";
                    TxtSubtitle.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Red);
                    BtnClose.Content = "Close";
                    BtnClose.Visibility = Visibility.Visible;
                    return;
                }
            }

            SetStep(1, "\u2714");
            SetProgress(60);

            // Step 3: Desktop shortcut
            SetStep(2, "\u25B8");
            await Task.Delay(200);

            CreateDesktopShortcut(installedExe);

            SetStep(2, "\u2714");
            SetProgress(80);

            // Step 4: Startup + auto-connect
            SetStep(3, "\u25B8");
            await Task.Delay(200);

            StartupService.SetEnabled(true, installedExe);

            var settings = Models.Settings.Load();
            settings.LaunchAtStartup = true;
            settings.AutoConnect = true;
            settings.Save();

            SetStep(3, "\u2714");
            SetProgress(100);

            // Done
            TxtSubtitle.Text = "Ready to go!";
            BtnClose.Visibility = Visibility.Visible;
            SetupCompleted = true;
        }
        catch (Exception ex)
        {
            TxtSubtitle.Text = $"Error: {ex.Message}";
            TxtSubtitle.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Red);
            BtnClose.Content = "Close";
            BtnClose.Visibility = Visibility.Visible;
        }
    }

    private static void CreateDesktopShortcut(string targetExe)
    {
        try
        {
            var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            var shortcutPath = Path.Combine(desktopPath, "PunchThrough.lnk");

            var link = (IShellLink)new ShellLink();
            link.SetPath(targetExe);
            link.SetWorkingDirectory(Path.GetDirectoryName(targetExe)!);
            link.SetDescription("PunchThrough - DPI Bypass");

            var file = (IPersistFile)link;
            file.Save(shortcutPath, false);
        }
        catch { }
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        if (SetupCompleted)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = InstallService.InstalledExePath,
                UseShellExecute = true
            });
        }

        Application.Current.Shutdown();
    }

    private void OnLinkClick(object sender, RequestNavigateEventArgs e)
    {
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }

    protected override void OnMouseLeftButtonDown(System.Windows.Input.MouseButtonEventArgs e)
    {
        DragMove();
    }

    [ComImport, Guid("00021401-0000-0000-C000-000000000046")]
    private class ShellLink { }

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("000214F9-0000-0000-C000-000000000046")]
    private interface IShellLink
    {
        void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFile, int cch, IntPtr pfd, int fFlags);
        void GetIDList(out IntPtr ppidl);
        void SetIDList(IntPtr pidl);
        void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszName, int cch);
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
        void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszDir, int cch);
        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
        void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszArgs, int cch);
        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
        void GetHotkey(out short pwHotkey);
        void SetHotkey(short wHotkey);
        void GetShowCmd(out int piShowCmd);
        void SetShowCmd(int iShowCmd);
        void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszIconPath, int cch, out int piIcon);
        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, int dwReserved);
        void Resolve(IntPtr hwnd, int fFlags);
        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
    }
}
