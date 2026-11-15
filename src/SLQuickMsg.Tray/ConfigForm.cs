using System.ComponentModel;
using System.Windows.Forms;
using SLQuickMsg.Input;

namespace SLQuickMsg.Tray;

/// <summary>配置界面：左侧轮盘列表，右侧当前轮盘的按键与槽位（标题 + 正文）。</summary>
internal sealed class ConfigForm : Form
{
    private readonly string _configPath;
    private readonly QuickMsgConfig _config;

    private readonly ListBox _wheelList = new() { Dock = DockStyle.Fill, IntegralHeight = false };
    private readonly TextBox _prefix = new() { Dock = DockStyle.Fill };
    private readonly CheckBox _notify = new() { Text = "发送后托盘气泡提示", AutoSize = true };

    private readonly TextBox _key = new() { Width = 90, ReadOnly = true };
    private readonly Button _capture = new() { Text = "按下按键…", Width = 110 };
    private readonly TextBox _name = new() { Width = 180 };
    private readonly NumericUpDown _rotation = new() { Minimum = -180, Maximum = 180, Increment = 45, Width = 70 };
    private readonly DataGridView _grid = new();
    private readonly Label _status = new() { AutoSize = true, ForeColor = System.Drawing.Color.DimGray };

    private SplitContainer? _split;
    private BindingList<WheelSlot> _slots = new();
    private bool _binding;
    private bool _updatingList;
    private readonly KeyCapture _keyCapture = new();
    private WheelConfig? _current;
    private bool _dirty;

    public ConfigForm(string configPath)
    {
        _configPath = configPath;
        _config = ConfigIO.Load(configPath);

        Text = "SL QuickMsg 配置";
        MinimumSize = new System.Drawing.Size(900, 560);
        StartPosition = FormStartPosition.CenterScreen;
        KeyPreview = true;

        BuildLayout();
        TrySetSplitter();
        ApplyGlobalFields();
        RebuildWheelList();

        _keyCapture.Captured += OnKeyCaptured;
        _keyCapture.Cancelled += EndCapture;
        FormClosing += OnFormClosing;
        Load += (_, _) => TrySetSplitter();
    }

    public event Action? ConfigSaved;

    /// <summary>预览轮盘：参数为标题列表与旋转角度。</summary>
    public Action<IReadOnlyList<string>, double>? PreviewRequested { get; set; }

    private void BuildLayout()
    {
        MinimumSize = new Size(980, 620);
        Size = new Size(1120, 700);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(10),
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var globalRow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            WrapContents = true,
            Margin = new Padding(0, 0, 0, 6),
        };
        globalRow.Controls.Add(new Label { Text = "命令前缀", AutoSize = true, Margin = new Padding(0, 9, 6, 0) });
        _prefix.Width = 110;
        _prefix.Margin = new Padding(0, 4, 0, 0);
        globalRow.Controls.Add(_prefix);
        _notify.Margin = new Padding(16, 7, 0, 0);
        globalRow.Controls.Add(_notify);
        root.Controls.Add(globalRow, 0, 0);

        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            FixedPanel = FixedPanel.Panel1,
            SplitterWidth = 6,
            Panel1MinSize = 0,
            Panel2MinSize = 0,
        };
        _split = split;

        var leftLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Padding = new Padding(0) };
        leftLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        leftLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _wheelList.Margin = new Padding(0);
        leftLayout.Controls.Add(_wheelList, 0, 0);

        var leftButtons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            WrapContents = true,
            Margin = new Padding(0, 6, 0, 0),
        };
        var addWheel = new Button { Text = "新增", Width = 72, Height = 30 };
        var removeWheel = new Button { Text = "删除", Width = 72, Height = 30 };
        addWheel.Click += (_, _) => AddWheel();
        removeWheel.Click += (_, _) => RemoveWheel();
        leftButtons.Controls.Add(addWheel);
        leftButtons.Controls.Add(removeWheel);
        leftLayout.Controls.Add(leftButtons, 0, 1);
        split.Panel1.Controls.Add(leftLayout);

        var right = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(8, 0, 0, 0),
        };
        right.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        right.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        right.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        right.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var fieldRow1 = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            WrapContents = true,
            Margin = new Padding(0, 0, 0, 4),
        };
        fieldRow1.Controls.Add(new Label { Text = "按键", AutoSize = true, Margin = new Padding(0, 9, 6, 0) });
        _key.Width = 90;
        _key.Height = 26;
        _key.Margin = new Padding(0, 4, 6, 0);
        fieldRow1.Controls.Add(_key);
        _capture.Width = 104;
        _capture.Height = 30;
        _capture.Margin = new Padding(0, 2, 0, 0);
        fieldRow1.Controls.Add(_capture);
        fieldRow1.Controls.Add(new Label { Text = "名称", AutoSize = true, Margin = new Padding(18, 9, 6, 0) });
        _name.Width = 200;
        _name.Height = 26;
        _name.Margin = new Padding(0, 4, 0, 0);
        fieldRow1.Controls.Add(_name);
        right.Controls.Add(fieldRow1, 0, 0);

        var fieldRow2 = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            WrapContents = true,
            Margin = new Padding(0, 0, 0, 6),
        };
        fieldRow2.Controls.Add(new Label { Text = "轮盘旋转", AutoSize = true, Margin = new Padding(0, 9, 6, 0) });
        _rotation.Width = 70;
        _rotation.Margin = new Padding(0, 4, 6, 0);
        fieldRow2.Controls.Add(_rotation);
        fieldRow2.Controls.Add(new Label
        {
            Text = "度（0 = 上/右/下/左，45 = 斜角）",
            AutoSize = true,
            ForeColor = Color.DimGray,
            Margin = new Padding(0, 9, 0, 0),
        });
        right.Controls.Add(fieldRow2, 0, 1);

        _grid.Dock = DockStyle.Fill;
        _grid.Margin = new Padding(0);
        _grid.AutoGenerateColumns = false;
        _grid.AllowUserToAddRows = false;
        _grid.AllowUserToDeleteRows = false;
        _grid.RowHeadersVisible = false;
        _grid.BackgroundColor = Color.White;
        _grid.BorderStyle = BorderStyle.FixedSingle;
        _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "标题（轮盘上显示）",
            DataPropertyName = nameof(WheelSlot.Title),
            FillWeight = 35,
            MinimumWidth = 140,
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "正文（实际发送）",
            DataPropertyName = nameof(WheelSlot.Body),
            FillWeight = 65,
            MinimumWidth = 200,
        });
        _grid.DataError += (_, e) => e.ThrowException = false;
        right.Controls.Add(_grid, 0, 2);

        var slotButtons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            WrapContents = true,
            Margin = new Padding(0, 6, 0, 0),
        };
        var addSlot = new Button { Text = "添加槽位", Width = 90, Height = 30 };
        var removeSlot = new Button { Text = "删除选中", Width = 90, Height = 30 };
        var moveUp = new Button { Text = "上移", Width = 60, Height = 30 };
        var moveDown = new Button { Text = "下移", Width = 60, Height = 30 };
        var previewSlot = new Button { Text = "预览轮盘", Width = 90, Height = 30 };
        addSlot.Click += (_, _) => AddSlot();
        removeSlot.Click += (_, _) => RemoveSlot();
        moveUp.Click += (_, _) => MoveSlot(-1);
        moveDown.Click += (_, _) => MoveSlot(1);
        previewSlot.Click += (_, _) => Preview();
        slotButtons.Controls.Add(addSlot);
        slotButtons.Controls.Add(removeSlot);
        slotButtons.Controls.Add(moveUp);
        slotButtons.Controls.Add(moveDown);
        slotButtons.Controls.Add(previewSlot);
        slotButtons.Controls.Add(new Label
        {
            Text = "顺序 = 顺时针方向（第 1 条在正上方）",
            AutoSize = true,
            ForeColor = Color.DimGray,
            Margin = new Padding(8, 9, 0, 0),
        });
        right.Controls.Add(slotButtons, 0, 3);

        split.Panel2.Controls.Add(right);
        root.Controls.Add(split, 0, 1);

        var bottom = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _status.AutoSize = true;
        _status.Anchor = AnchorStyles.Left;
        _status.Margin = new Padding(0, 14, 0, 0);
        bottom.Controls.Add(_status, 0, 0);

        var bottomButtons = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 6, 0, 0),
        };
        var save = new Button { Text = "保存", Width = 90, Height = 32 };
        var reload = new Button { Text = "重新载入", Width = 90, Height = 32 };
        save.Click += (_, _) => SaveConfig();
        reload.Click += (_, _) => ReloadFromDisk();
        bottomButtons.Controls.Add(save);
        bottomButtons.Controls.Add(reload);
        bottom.Controls.Add(bottomButtons, 1, 0);
        root.Controls.Add(bottom, 0, 2);

        Controls.Add(root);

        _wheelList.SelectedIndexChanged += (_, _) =>
        {
            if (!_updatingList)
            {
                BindWheel(_wheelList.SelectedIndex);
            }
        };
        _capture.Click += (_, _) => StartCapture();
        _prefix.TextChanged += (_, _) => { if (!_binding) _dirty = true; };
        _notify.CheckedChanged += (_, _) => { if (!_binding) _dirty = true; };
        _name.TextChanged += OnNameChanged;
        _rotation.ValueChanged += (_, _) => { if (!_binding) _dirty = true; };
        _grid.CellValueChanged += (_, _) => { if (!_binding) _dirty = true; };
    }

    private void TrySetSplitter()
    {
        if (_split is null || _split.Width < 700)
        {
            return;
        }

        try
        {
            _split.SplitterDistance = 210;
        }
        catch (InvalidOperationException)
        {
            // 宽度还不够时忽略，下次布局会再试
        }
    }

    private void Preview()
    {
        if (_current is null)
        {
            MessageBox.Show(this, "先选一个轮盘", "SL QuickMsg", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var titles = _slots.Select(s => string.IsNullOrWhiteSpace(s.Title) ? "（未命名）" : s.Title).ToArray();
        PreviewRequested?.Invoke(titles, (double)_rotation.Value);
    }

    private void ApplyGlobalFields()
    {
        _prefix.Text = _config.CommandPrefix;
        _notify.Checked = _config.NotifyOnSend;
    }

    private void RebuildWheelList()
    {
        _wheelList.Items.Clear();
        foreach (var wheel in _config.Wheels)
        {
            _wheelList.Items.Add(Describe(wheel));
        }

        if (_wheelList.Items.Count > 0)
        {
            _wheelList.SelectedIndex = 0;
        }
        else
        {
            BindWheel(-1);
        }
    }

    private static string Describe(WheelConfig wheel) => $"{wheel.Key} · {wheel.Name}";

    private void OnNameChanged(object? sender, EventArgs e)
    {
        if (_binding || _current is null)
        {
            return;
        }

        _dirty = true;
        _current.Name = string.IsNullOrWhiteSpace(_name.Text) ? "未命名轮盘" : _name.Text.Trim();
        UpdateCurrentListItem();
    }

    /// <summary>只更新列表里的文字，不触发 SelectedIndexChanged 重绑（否则正在输入的名称会被覆盖）。</summary>
    private void UpdateCurrentListItem()
    {
        if (_current is null || _wheelList.SelectedIndex < 0)
        {
            return;
        }

        var index = _wheelList.SelectedIndex;
        _updatingList = true;
        try
        {
            _wheelList.Items[index] = Describe(_current);
            _wheelList.SelectedIndex = index;
        }
        finally
        {
            _updatingList = false;
        }
    }

    private void BindWheel(int index)
    {
        _binding = true;
        try
        {
            _current = index >= 0 && index < _config.Wheels.Count ? _config.Wheels[index] : null;

            if (_current is null)
            {
                _key.Text = "";
                _name.Text = "";
                _slots = new BindingList<WheelSlot>();
                _grid.DataSource = _slots;
                _grid.Enabled = false;
                return;
            }

            _grid.Enabled = true;
            _key.Text = _current.Key;
            _name.Text = _current.Name;
            _rotation.Value = Math.Clamp((decimal)_current.RotationDegrees, _rotation.Minimum, _rotation.Maximum);
            _slots = new BindingList<WheelSlot>(_current.Slots);
            _grid.DataSource = _slots;
            _dirty = false;
        }
        finally
        {
            _binding = false;
        }
    }

    private void AddWheel()
    {
        var wheel = new WheelConfig
        {
            Key = "",
            Name = "新轮盘",
            Slots =
            {
                new WheelSlot { Title = "槽位 1", Body = "" },
                new WheelSlot { Title = "槽位 2", Body = "" },
                new WheelSlot { Title = "槽位 3", Body = "" },
                new WheelSlot { Title = "槽位 4", Body = "" },
            },
        };

        _config.Wheels.Add(wheel);
        RebuildWheelList();
        _wheelList.SelectedIndex = _config.Wheels.Count - 1;
        _dirty = true;
    }

    private void RemoveWheel()
    {
        var index = _wheelList.SelectedIndex;
        if (index < 0 || index >= _config.Wheels.Count)
        {
            return;
        }

        _config.Wheels.RemoveAt(index);
        RebuildWheelList();
        _dirty = true;
    }

    private void AddSlot()
    {
        if (_current is null)
        {
            return;
        }

        if (_slots.Count >= 8)
        {
            MessageBox.Show(this, "一个轮盘最多 8 个槽位", "SL QuickMsg", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        _slots.Add(new WheelSlot { Title = $"槽位 {_slots.Count + 1}", Body = "" });
        _current.Slots = _slots.ToList();
        _dirty = true;
    }

    /// <summary>上移/下移一个槽位，直接改变它在轮盘上的方向顺序。</summary>
    private void MoveSlot(int delta)
    {
        if (_current is null)
        {
            return;
        }

        var row = _grid.CurrentCell?.RowIndex ?? -1;
        var target = row + delta;
        if (row < 0 || row >= _slots.Count || target < 0 || target >= _slots.Count)
        {
            return;
        }

        var item = _slots[row];
        _slots.RemoveAt(row);
        _slots.Insert(target, item);
        _current.Slots = _slots.ToList();

        if (target < _grid.Rows.Count)
        {
            _grid.CurrentCell = _grid.Rows[target].Cells[0];
        }

        _dirty = true;
    }

    private void RemoveSlot()
    {
        if (_current is null || _slots.Count == 0)
        {
            return;
        }

        var index = _grid.CurrentCell?.RowIndex ?? _slots.Count - 1;
        if (index < 0 || index >= _slots.Count)
        {
            index = _slots.Count - 1;
        }

        _slots.RemoveAt(index);
        _current.Slots = _slots.ToList();
        _dirty = true;
    }

    private void StartCapture()
    {
        _capture.Text = "请按键…（Esc 取消）";
        _capture.Enabled = false;
        _keyCapture.Start();
    }

    private void OnKeyCaptured(int virtualKey)
    {
        if (_current is null)
        {
            EndCapture();
            return;
        }

        var name = KeyCodeMap.ToName(virtualKey);
        if (name is null || !KeyCodeMap.TryGetVirtualKey(name, out _))
        {
            MessageBox.Show(this, $"按键 0x{virtualKey:X2} 暂不支持，换一个试试", "SL QuickMsg",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            EndCapture();
            return;
        }

        _current.Key = name;
        _key.Text = name;
        _dirty = true;
        UpdateCurrentListItem();
        EndCapture();
    }

    private void EndCapture()
    {
        _keyCapture.Stop();
        _capture.Text = "按下按键…";
        _capture.Enabled = true;
    }

    private void SaveConfig()
    {
        if (_current is not null)
        {
            _current.Slots = _slots.ToList();
            _current.Name = string.IsNullOrWhiteSpace(_name.Text) ? "未命名轮盘" : _name.Text.Trim();
            _current.RotationDegrees = (double)_rotation.Value;
            UpdateCurrentListItem();
        }

        _config.CommandPrefix = _prefix.Text;
        _config.NotifyOnSend = _notify.Checked;

        var problems = ValidateConfig();
        if (problems.Count > 0)
        {
            var result = MessageBox.Show(this, "发现以下问题，仍然保存？\n\n" + string.Join("\n", problems),
                "SL QuickMsg", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (result != DialogResult.Yes)
            {
                return;
            }
        }

        try
        {
            ConfigIO.Save(_configPath, _config);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "保存失败：" + ex.Message, "SL QuickMsg", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        _dirty = false;
        _status.Text = $"已保存 {DateTime.Now:HH:mm:ss}";
        ConfigSaved?.Invoke();
    }

    private List<string> ValidateConfig()
    {
        var problems = new List<string>();
        var keys = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var wheel in _config.Wheels)
        {
            if (!KeyCodeMap.TryGetVirtualKey(wheel.Key, out _))
            {
                problems.Add($"轮盘「{wheel.Name}」没有设置有效按键");
                continue;
            }

            if (keys.TryGetValue(wheel.Key, out var other))
            {
                problems.Add($"按键 {wheel.Key} 被「{other}」和「{wheel.Name}」同时使用");
            }
            else
            {
                keys[wheel.Key] = wheel.Name;
            }

            var usable = wheel.Slots.Count(s => !string.IsNullOrWhiteSpace(s.Body));
            if (usable == 0)
            {
                problems.Add($"轮盘「{wheel.Name}」没有可发送的正文");
            }

            if (wheel.Slots.Count > 8)
            {
                problems.Add($"轮盘「{wheel.Name}」槽位超过 8 个");
            }
        }

        return problems;
    }

    private void ReloadFromDisk()
    {
        if (_dirty)
        {
            var result = MessageBox.Show(this, "当前修改未保存，确定重新载入？", "SL QuickMsg",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (result != DialogResult.Yes)
            {
                return;
            }
        }

        var fresh = ConfigIO.Load(_configPath);
        _config.Wheels = fresh.Wheels;
        _config.CommandPrefix = fresh.CommandPrefix;
        _config.NotifyOnSend = fresh.NotifyOnSend;

        ApplyGlobalFields();
        RebuildWheelList();
        _dirty = false;
        _status.Text = "已从磁盘重新载入";
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _keyCapture.Dispose();
        }

        base.Dispose(disposing);
    }

    private void OnFormClosing(object? sender, FormClosingEventArgs e)
    {
        if (!_dirty)
        {
            return;
        }

        var result = MessageBox.Show(this, "有未保存的修改，是否保存？", "SL QuickMsg",
            MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);

        if (result == DialogResult.Cancel)
        {
            e.Cancel = true;
            return;
        }

        if (result == DialogResult.Yes)
        {
            SaveConfig();
            if (_dirty)
            {
                e.Cancel = true;
            }
        }
    }
}
