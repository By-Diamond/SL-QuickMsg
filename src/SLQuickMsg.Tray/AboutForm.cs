using System.Diagnostics;
using System.Reflection;
using System.Windows.Forms;

namespace SLQuickMsg.Tray;

/// <summary>关于页：图标、版本、作者、仓库与 B 站链接。</summary>
internal sealed class AboutForm : Form
{
    public const string RepositoryUrl = "https://github.com/By-Diamond/SL-QuickMsg";
    public const string BilibiliUrl = "https://space.bilibili.com/602152864";

    public AboutForm()
    {
        Text = "关于 SL QuickMsg";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.None;
        ClientSize = new Size(460, 372);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            Padding = new Padding(22, 20, 22, 18),
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));

        var header = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Margin = new Padding(0),
        };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 94));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        var icon = new PictureBox
        {
            Image = LoadIcon(),
            SizeMode = PictureBoxSizeMode.Zoom,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 0, 10, 0),
        };

        var info = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Margin = new Padding(0) };
        info.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        info.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
        info.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
        info.Controls.Add(new Label
        {
            Text = "SL QuickMsg",
            Font = new Font("Microsoft YaHei UI", 16f, FontStyle.Bold),
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 2, 0, 0),
        }, 0, 0);
        info.Controls.Add(new Label
        {
            Text = "版本 " + VersionText,
            ForeColor = Color.DimGray,
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0),
        }, 0, 1);
        info.Controls.Add(new Label
        {
            Text = "By Diamond",
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0),
        }, 0, 2);

        header.Controls.Add(icon, 0, 0);
        header.Controls.Add(info, 1, 0);
        root.Controls.Add(header, 0, 0);

        root.Controls.Add(new Label
        {
            Text = "SCP: Secret Laboratory 快速聊天轮盘\n按住触发键选方向，松手自动发送 .tc 消息",
            AutoSize = true,
            Margin = new Padding(0, 10, 0, 6),
        }, 0, 1);

        var links = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            Margin = new Padding(0, 4, 0, 0),
        };
        var repoLink = new LinkLabel { Text = "GitHub 仓库：" + RepositoryUrl, AutoSize = true, Margin = new Padding(0, 4, 0, 0) };
        repoLink.LinkClicked += (_, _) => OpenUrl(RepositoryUrl);
        var biliLink = new LinkLabel { Text = "Bilibili 主页：" + BilibiliUrl, AutoSize = true, Margin = new Padding(0, 6, 0, 0) };
        biliLink.LinkClicked += (_, _) => OpenUrl(BilibiliUrl);
        links.Controls.Add(repoLink);
        links.Controls.Add(biliLink);
        root.Controls.Add(links, 0, 2);

        var bottom = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Margin = new Padding(0, 6, 0, 0),
        };
        var close = new Button
        {
            Text = "关闭",
            DialogResult = DialogResult.OK,
            Width = 76,
            Height = 30,
            Margin = new Padding(0),
        };
        bottom.Controls.Add(close);
        root.Controls.Add(bottom, 0, 4);

        Controls.Add(root);
        AcceptButton = close;
        CancelButton = close;
    }

    public static string VersionText =>
        Assembly.GetExecutingAssembly().GetName().Version is { } v
            ? $"{v.Major}.{v.Minor}.{v.Build}"
            : "0.1.0";

    private static Image LoadIcon()
    {
        try
        {
            using var icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            if (icon is not null)
            {
                return icon.ToBitmap();
            }
        }
        catch
        {
            // 用系统图标兜底
        }

        return SystemIcons.Application.ToBitmap();
    }

    private static void OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show("打开链接失败：" + ex.Message, "SL QuickMsg", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
}
