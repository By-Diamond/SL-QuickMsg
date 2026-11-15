using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using SLQuickMsg.Input;

namespace SLQuickMsg.Wheel;

/// <summary>
/// 轮盘覆盖层：置顶、逐像素透明、鼠标穿透、不抢焦点。
/// 由于不激活窗口，游戏始终保持前台焦点。
/// </summary>
internal sealed class WheelOverlay : Form
{
    private const int WsExLayered = 0x00080000;
    private const int WsExToolWindow = 0x00000080;
    private const int SwShowNoActivate = 4;
    private const int SwHide = 0;
    private const int UlwAlpha = 2;

    [StructLayout(LayoutKind.Sequential)]
    private struct PointStruct
    {
        public int X;
        public int Y;
        public PointStruct(int x, int y) { X = x; Y = y; }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SizeStruct
    {
        public int Cx;
        public int Cy;
        public SizeStruct(int cx, int cy) { Cx = cx; Cy = cy; }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct BlendFunction
    {
        public byte BlendOp;
        public byte BlendFlags;
        public byte SourceConstantAlpha;
        public byte AlphaFormat;
    }

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hwnd, int command);

    [DllImport("user32.dll")]
    private static extern IntPtr SetForegroundWindow(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern bool ClipCursor(IntPtr rect);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr destDc, ref PointStruct dest, ref SizeStruct size,
        IntPtr sourceDc, ref PointStruct source, int colorKey, ref BlendFunction blend, int flags);

    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hwnd, IntPtr dc);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleDC(IntPtr dc);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteDC(IntPtr dc);

    [DllImport("gdi32.dll")]
    private static extern IntPtr SelectObject(IntPtr dc, IntPtr obj);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr obj);

    private readonly int _size = 560;
    private readonly Font _titleFont = new("Microsoft YaHei UI", 11f, FontStyle.Bold, GraphicsUnit.Point);
    private readonly Font _hintFont = new("Microsoft YaHei UI", 9f, FontStyle.Regular, GraphicsUnit.Point);

    private string[] _titles = Array.Empty<string>();
    private int _activeIndex = -1;
    private double _rotation;

    public WheelOverlay()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        Size = new Size(_size, _size);
        SetStyle(ControlStyles.Opaque, true);
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= WsExLayered | WsExToolWindow;
            return cp;
        }
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        // 由 UpdateLayeredWindow 负责绘制
    }

    /// <summary>在指定屏幕点居中显示轮盘。</summary>
    public void ShowWheel(Point screenCenter, IReadOnlyList<string> titles, double rotationDegrees)
    {
        _titles = titles.ToArray();
        _rotation = rotationDegrees;
        _activeIndex = -1;

        Location = new Point(screenCenter.X - _size / 2, screenCenter.Y - _size / 2);
        if (!IsHandleCreated)
        {
            _ = Handle;
        }

        Render();

        // 主动接管前台：游戏失焦后会释放鼠标锁定，鼠标不再转视角
        Show();
        FocusHelper.ForceForeground(Handle);
    }

    public void SetTitles(IReadOnlyList<string> titles)
    {
        _titles = titles.ToArray();
    }

    public void SetActive(int index)
    {
        if (_activeIndex == index)
        {
            return;
        }

        _activeIndex = index;
        Render();
    }

    public void HideWheel()
    {
        _activeIndex = -1;
        if (IsHandleCreated)
        {
            ShowWindow(Handle, SwHide);
        }
    }

    /// <summary>把轮盘画成位图（供覆盖层与调试导出共用）。</summary>
    public Bitmap RenderBitmap(int activeIndex)
    {
        var bitmap = new Bitmap(_size, _size, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            g.Clear(Color.Transparent);

            var count = _titles.Length;
            var center = new PointF(_size / 2f, _size / 2f);
            const float outer = 258f;
            const float inner = 92f;

            if (count > 0)
            {
                var step = 360f / count;
                for (var i = 0; i < count; i++)
                {
                    var start = -90f - step / 2f + (float)_rotation + i * step;
                    var active = i == activeIndex;
                    using var path = BuildSector(center, inner, outer, start, step);
                    using var fill = new SolidBrush(active
                        ? Color.FromArgb(235, 30, 120, 220)
                        : Color.FromArgb(200, 14, 18, 24));
                    using var border = new Pen(active ? Color.FromArgb(255, 180, 220, 255) : Color.FromArgb(90, 255, 255, 255),
                        active ? 2.4f : 1f);
                    g.FillPath(fill, path);
                    g.DrawPath(border, path);

                    DrawTitle(g, center, inner, outer, start + step / 2f, _titles[i]);
                }
            }

            using (var hub = new SolidBrush(Color.FromArgb(190, 8, 10, 14)))
            using (var hubBorder = new Pen(Color.FromArgb(80, 255, 255, 255), 1f))
            {
                g.FillEllipse(hub, center.X - inner, center.Y - inner, inner * 2, inner * 2);
                g.DrawEllipse(hubBorder, center.X - inner, center.Y - inner, inner * 2, inner * 2);
            }

            DrawCentered(g, "松开发送", _hintFont, Color.FromArgb(235, 255, 255, 255), new PointF(center.X, center.Y - 12));
            DrawCentered(g, "中间取消", _hintFont, Color.FromArgb(150, 200, 210, 225), new PointF(center.X, center.Y + 14));
        }

        return bitmap;
    }

    private void Render()
    {
        using var bitmap = RenderBitmap(_activeIndex);
        ApplyLayeredBitmap(bitmap);
    }

    private static GraphicsPath BuildSector(PointF center, float inner, float outer, float start, float sweep)
    {
        var path = new GraphicsPath();
        var outerRect = new RectangleF(center.X - outer, center.Y - outer, outer * 2, outer * 2);
        var innerRect = new RectangleF(center.X - inner, center.Y - inner, inner * 2, inner * 2);
        path.AddArc(outerRect, start, sweep);
        path.AddArc(innerRect, start + sweep, -sweep);
        path.CloseFigure();
        return path;
    }

    private void DrawTitle(Graphics g, PointF center, float inner, float outer, float angleDegrees, string title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return;
        }

        var radius = (inner + outer) / 2f;
        var radians = angleDegrees * Math.PI / 180d;
        var point = new PointF(
            center.X + (float)(Math.Cos(radians) * radius),
            center.Y + (float)(Math.Sin(radians) * radius));

        var rect = new RectangleF(point.X - 78, point.Y - 26, 156, 52);
        using var format = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center,
            Trimming = StringTrimming.EllipsisCharacter,
        };
        g.DrawString(title, _titleFont, Brushes.White, rect, format);
    }

    private static void DrawCentered(Graphics g, string text, Font font, Color color, PointF point)
    {
        using var brush = new SolidBrush(color);
        using var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        g.DrawString(text, font, brush, new RectangleF(point.X - 90, point.Y - 14, 180, 28), format);
    }

    private void ApplyLayeredBitmap(Bitmap bitmap)
    {
        var screenDc = GetDC(IntPtr.Zero);
        var memDc = CreateCompatibleDC(screenDc);
        var hBitmap = IntPtr.Zero;
        var oldObject = IntPtr.Zero;

        try
        {
            hBitmap = bitmap.GetHbitmap(Color.FromArgb(0));
            oldObject = SelectObject(memDc, hBitmap);

            var dest = new PointStruct(Left, Top);
            var size = new SizeStruct(bitmap.Width, bitmap.Height);
            var source = new PointStruct(0, 0);
            var blend = new BlendFunction
            {
                BlendOp = 0,
                BlendFlags = 0,
                SourceConstantAlpha = 255,
                AlphaFormat = 1,
            };

            UpdateLayeredWindow(Handle, screenDc, ref dest, ref size, memDc, ref source, 0, ref blend, UlwAlpha);
        }
        finally
        {
            if (oldObject != IntPtr.Zero)
            {
                SelectObject(memDc, oldObject);
            }

            if (hBitmap != IntPtr.Zero)
            {
                DeleteObject(hBitmap);
            }

            DeleteDC(memDc);
            ReleaseDC(IntPtr.Zero, screenDc);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _titleFont.Dispose();
            _hintFont.Dispose();
        }

        base.Dispose(disposing);
    }
}
