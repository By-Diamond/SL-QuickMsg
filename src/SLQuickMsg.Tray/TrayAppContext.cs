using System.Diagnostics;
using System.Windows.Forms;
using SLQuickMsg.Input;
using SLQuickMsg.Wheel;

namespace SLQuickMsg.Tray;

/// <summary>托盘入口：持有键盘/鼠标钩子、轮盘覆盖层与发送逻辑。</summary>
internal sealed class TrayAppContext : ApplicationContext
{
    private readonly KeyboardHook _keyboardHook;
    private readonly RawMouse _rawMouse;
    private readonly WheelOverlay _overlay;
    private readonly WheelEngine _engine;
    private readonly NotifyIcon _icon;

    private QuickMsgConfig _config;
    private ConfigForm? _form;

    public TrayAppContext()
    {
        _config = ConfigIO.Load(ConfigPath);
        Log.Info($"启动，配置 {ConfigPath}，轮盘 {_config.Wheels.Count} 个");

        _keyboardHook = new KeyboardHook();
        _rawMouse = new RawMouse();
        _overlay = new WheelOverlay();
        _engine = new WheelEngine(_keyboardHook, _rawMouse, _overlay);
        _engine.Reload(_config);
        Log.Info("轮盘按键：" + string.Join(",", _config.Wheels.Select(w => w.Key)));
        _engine.SendText += OnSendText;

        var menu = new ContextMenuStrip();
        menu.Items.Add("打开配置", null, (_, _) => ShowConfig());
        menu.Items.Add("打开配置文件夹", null, (_, _) => OpenConfigFolder());

        var autostartItem = new ToolStripMenuItem("开机自启动")
        {
            CheckOnClick = true,
            Checked = Autostart.IsEnabled,
        };
        autostartItem.Click += (_, _) =>
        {
            var ok = Autostart.Set(autostartItem.Checked);
            if (!ok)
            {
                autostartItem.Checked = Autostart.IsEnabled;
                ShowStatus("设置开机自启动失败（注册表被拒绝）");
            }
            else
            {
                ShowStatus(autostartItem.Checked ? "已开启开机自启动" : "已关闭开机自启动");
            }
        };
        menu.Items.Add(autostartItem);

        menu.Items.Add("关于", null, (_, _) => ShowAbout());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("退出", null, (_, _) => Exit());

        _icon = new NotifyIcon
        {
            Icon = LoadAppIcon(),
            Text = BuildTooltip(),
            Visible = true,
            ContextMenuStrip = menu,
        };
        _icon.DoubleClick += (_, _) => ShowConfig();
    }

    public string ConfigPath => ConfigIO.DefaultPath;

    private string BuildTooltip()
    {
        var keys = string.Join(",", _config.Wheels.Select(w => w.Key).Where(k => !string.IsNullOrWhiteSpace(k)));
        return $"SL QuickMsg {AboutForm.VersionText} · By Diamond\n轮盘按键：{(string.IsNullOrEmpty(keys) ? "未配置" : keys)}";
    }

    private static System.Drawing.Icon LoadAppIcon()
    {
        try
        {
            if (System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath) is { } icon)
            {
                return icon;
            }
        }
        catch
        {
            // 用系统图标兜底
        }

        return System.Drawing.SystemIcons.Application;
    }

    private void ShowAbout()
    {
        using var about = new AboutForm();
        about.ShowDialog();
    }

    private void OnSendText(string fullCommand)
    {
        Task.Run(() =>
        {
            try
            {
                if (!WaitForGameForeground())
                {
                    ShowStatus("游戏不在前台，已取消发送");
                    return;
                }

                Log.Info("发送：" + fullCommand);
                GameSend.SendCommand(fullCommand);
                if (_config.NotifyOnSend)
                {
                    ShowStatus("已发送：" + fullCommand);
                }
            }
            catch (Exception ex)
            {
                Log.Error("发送失败", ex);
                ShowStatus("发送失败：" + ex.Message);
            }
        });
    }

    /// <summary>轮盘关闭后要把前台还给游戏，给 Windows 一点切换时间。</summary>
    private bool WaitForGameForeground(int timeoutMs = 600)
    {
        var started = Environment.TickCount64;
        while (Environment.TickCount64 - started < timeoutMs)
        {
            if (_engine.IsGameForeground())
            {
                return true;
            }

            Thread.Sleep(15);
        }

        return false;
    }

    private void ShowStatus(string message)
    {
        if (_icon.Visible)
        {
            _icon.ShowBalloonTip(1500, "SL QuickMsg", message, ToolTipIcon.Info);
        }
    }

    private void ShowConfig()
    {
        if (_form is { IsDisposed: false })
        {
            _form.Activate();
            return;
        }

        _form = new ConfigForm(ConfigPath);
        _form.PreviewRequested = PreviewWheel;
        _form.ConfigSaved += () =>
        {
            _config = ConfigIO.Load(ConfigPath);
            _engine.Reload(_config);
            Log.Info("配置热更新，轮盘 " + _config.Wheels.Count + " 个");
            _icon.Text = BuildTooltip();
            ShowStatus($"配置已生效，已注册 {_config.Wheels.Count} 个轮盘");
        };
        _form.FormClosed += (_, _) => _form = null;
        _form.Show();
    }

    private void PreviewWheel(IReadOnlyList<string> titles, double rotationDegrees)
    {
        var screen = Screen.PrimaryScreen!.Bounds;
        var center = new Point(screen.Left + screen.Width / 2, screen.Top + screen.Height / 2);
        _overlay.ShowWheel(center, titles, rotationDegrees);

        var timer = new System.Windows.Forms.Timer { Interval = 3000 };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            timer.Dispose();
            _overlay.HideWheel();

            // 预览时轮盘会抢前台，结束后把焦点还给配置窗口
            if (_form is { IsDisposed: false })
            {
                Input.FocusHelper.ForceForeground(_form.Handle);
            }
        };
        timer.Start();
    }

    private void OpenConfigFolder()
    {
        var dir = Path.GetDirectoryName(ConfigPath)!;
        Directory.CreateDirectory(dir);
        Process.Start(new ProcessStartInfo(dir) { UseShellExecute = true });
    }

    private void Exit()
    {
        _engine.Dispose();
        _keyboardHook.Dispose();
        _rawMouse.Dispose();
        _overlay.Dispose();
        _icon.Visible = false;
        _icon.Dispose();
        ExitThread();
    }
}
