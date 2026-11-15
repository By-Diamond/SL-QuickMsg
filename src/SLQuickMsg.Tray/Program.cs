using System.Windows.Forms;
using SLQuickMsg.Wheel;

namespace SLQuickMsg.Tray;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();

        if (args.Contains("--preview", StringComparer.OrdinalIgnoreCase))
        {
            RunPreview();
            return;
        }

        if (args.Length >= 2 && args[0].Equals("--render", StringComparison.OrdinalIgnoreCase))
        {
            RenderToFile(args[1]);
            return;
        }

        if (args.Contains("--config", StringComparer.OrdinalIgnoreCase))
        {
            var form = new ConfigForm(ConfigIO.DefaultPath);
            Application.Run(form);
            return;
        }

        if (args.Length >= 2 && args[0].Equals("--config-shot", StringComparison.OrdinalIgnoreCase))
        {
            ConfigShot(args[1]);
            return;
        }

        if (args.Contains("--about", StringComparer.OrdinalIgnoreCase))
        {
            Application.Run(new AboutForm());
            return;
        }

        if (args.Length >= 2 && args[0].Equals("--about-shot", StringComparison.OrdinalIgnoreCase))
        {
            Shot(new AboutForm(), args[1]);
            return;
        }

        if (args.Contains("--keytest", StringComparer.OrdinalIgnoreCase))
        {
            KeyTest();
            return;
        }

        using var context = new TrayAppContext();
        Application.Run(context);
    }

    /// <summary>调试用：把任意窗体离屏渲染成 PNG。</summary>
    private static void Shot(Form form, string path)
    {
        using (form)
        {
            form.Opacity = 0;
            form.Show();
            Application.DoEvents();
            form.PerformLayout();

            using var bitmap = new Bitmap(form.ClientSize.Width, form.ClientSize.Height);
            form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.ClientSize));
            bitmap.Save(path, System.Drawing.Imaging.ImageFormat.Png);
            form.Hide();
        }
    }

    /// <summary>调试用：验证按键捕获能拿到真实 VK（模拟按 H）。</summary>
    private static void KeyTest()
    {
        using var capture = new SLQuickMsg.Input.KeyCapture();
        var captured = -1;
        capture.Captured += vk => captured = vk;
        capture.Start();
        Thread.Sleep(300);

        keybd_event(0x48, 0, 0, UIntPtr.Zero);
        Thread.Sleep(60);
        keybd_event(0x48, 0, 2, UIntPtr.Zero);
        Thread.Sleep(300);

        Console.WriteLine($"captured=0x{captured:X2}");
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);

    /// <summary>调试用：把配置窗口离屏渲染成 PNG，用来检查排版。</summary>
    private static void ConfigShot(string path)
    {
        using var form = new ConfigForm(ConfigIO.DefaultPath);
        form.Size = new Size(1120, 700);
        form.Opacity = 0;
        form.Show();
        Application.DoEvents();
        form.PerformLayout();

        using var bitmap = new Bitmap(form.ClientSize.Width, form.ClientSize.Height);
        form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.ClientSize));
        form.Hide();
        bitmap.Save(path, System.Drawing.Imaging.ImageFormat.Png);
    }

    /// <summary>调试用：把轮盘渲染成 PNG，不依赖屏幕与前台窗口。</summary>
    private static void RenderToFile(string path)
    {
        var overlay = new WheelOverlay();
        overlay.SetTitles(new[] { "集合", "撤离", "支援", "小心" });
        using var bitmap = overlay.RenderBitmap(1);
        bitmap.Save(path, System.Drawing.Imaging.ImageFormat.Png);
        overlay.Dispose();
    }

    /// <summary>调试用：直接在屏幕中央显示一次轮盘，4 秒后退出。</summary>
    private static void RunPreview()
    {
        var overlay = new WheelOverlay();
        var screen = Screen.PrimaryScreen!.Bounds;
        var center = new Point(screen.Left + screen.Width / 2, screen.Top + screen.Height / 2);
        overlay.ShowWheel(center, new[] { "集合", "撤离", "支援", "小心" }, 0);

        var context = new ApplicationContext();
        var timer = new System.Windows.Forms.Timer { Interval = 4000 };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            overlay.HideWheel();
            context.ExitThread();
        };
        timer.Start();
        Application.Run(context);
    }
}
