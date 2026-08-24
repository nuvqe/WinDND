using WinDND.Core.Models;
using WinDND.Core.Scheduling;

namespace WinDND.UI;

internal sealed class SettingsForm : Form
{
    private const int EnabledColumn = 0;
    private const int ActionColumn = 1;
    private const int CronColumn = 2;
    private const int ExplanationColumn = 3;
    private readonly CronScheduleService _cronService;
    private readonly string _hardwareDeviceStatus;
    private readonly TabControl _tabs = new();
    private readonly CheckBox _scheduleEnabled = new();
    private readonly DataGridView _grid = new();
    private readonly Label _status = new();
    private readonly CheckBox _hardwareEnabled = new();
    private readonly ComboBox _microphoneScope = new();
    private readonly ComboBox _keyRouting = new();
    private readonly ComboBox _leftAction = new();
    private readonly ComboBox _rightAction = new();
    private readonly ComboBox _leftIndicator = new();
    private readonly ComboBox _rightIndicator = new();
    private readonly TextBox _mutedColor = new();
    private readonly TextBox _unmutedColor = new();
    private readonly CheckBox _configureKeys = new();
    private readonly TextBox _vendorId = new();
    private readonly TextBox _productIds = new();
    private readonly Label _hardwareStatus = new();

    public SettingsForm(AppSettings settings, CronScheduleService cronService, string hardwareDeviceStatus = "")
    {
        _cronService = cronService;
        _hardwareDeviceStatus = hardwareDeviceStatus;
        Text = "WinDND settings";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(760, 500);
        Size = new Size(980, 600);
        ShowIcon = false;
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = new Font("Segoe UI", 9F);

        BuildLayout();
        LoadSettings(settings);
    }

    public AppSettings Result { get; private set; } = new();

    private void BuildLayout()
    {
        TableLayoutPanel root = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(12)
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        _scheduleEnabled.AutoSize = true;
        _scheduleEnabled.Text = "Enable scheduled rules";
        _scheduleEnabled.Margin = new Padding(0, 0, 0, 8);
        root.Controls.Add(_scheduleEnabled, 0, 0);

        Label help = new()
        {
            AutoSize = true,
            Text = "Use five fields: minute, hour, day of month, month, day of week. Actions: mute, unmute, or 0–100.",
            ForeColor = SystemColors.GrayText,
            Margin = new Padding(0, 0, 0, 8)
        };
        root.Controls.Add(help, 0, 1);

        ConfigureGrid();
        root.Controls.Add(_grid, 0, 2);

        FlowLayoutPanel ruleButtons = new()
        {
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            Margin = new Padding(0, 8, 0, 4)
        };
        Button add = new() { Text = "Add rule", AutoSize = true };
        Button remove = new() { Text = "Remove selected", AutoSize = true };
        add.Click += (_, _) => AddRule();
        remove.Click += (_, _) => RemoveSelectedRules();
        ruleButtons.Controls.Add(add);
        ruleButtons.Controls.Add(remove);
        root.Controls.Add(ruleButtons, 0, 3);

        TableLayoutPanel footer = new()
        {
            AutoSize = false,
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Margin = Padding.Empty
        };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        footer.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        _status.AutoSize = false;
        _status.Dock = DockStyle.Fill;
        _status.Padding = new Padding(0, 0, 8, 0);
        _status.Margin = Padding.Empty;
        _status.TextAlign = ContentAlignment.MiddleLeft;
        _status.ForeColor = SystemColors.GrayText;
        _status.Text = "Later rows win if rules occur at the same time.";
        footer.Controls.Add(_status, 0, 0);

        FlowLayoutPanel actionButtons = new()
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Anchor = AnchorStyles.Right,
            Margin = Padding.Empty
        };
        Button save = new() { Text = "Save", AutoSize = true };
        Button cancel = new() { Text = "Cancel", AutoSize = true, DialogResult = DialogResult.Cancel };
        save.Click += (_, _) => SaveAndClose();
        actionButtons.Controls.Add(save);
        actionButtons.Controls.Add(cancel);
        footer.Controls.Add(actionButtons, 1, 0);

        AcceptButton = save;
        CancelButton = cancel;
        TabPage schedulePage = new("Schedule") { Padding = new Padding(3) };
        schedulePage.Controls.Add(root);

        _tabs.Dock = DockStyle.Fill;
        _tabs.TabPages.Add(schedulePage);
        _tabs.TabPages.Add(BuildHardwarePage());
        _tabs.SelectedIndexChanged += (_, _) =>
        {
            _status.ForeColor = SystemColors.GrayText;
            _status.Text = _tabs.SelectedIndex == 0
                ? "Later rows win if rules occur at the same time."
                : "Muted and unmuted indicators use red and blue by default.";
        };

        TableLayoutPanel formLayout = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2
        };
        formLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        formLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44F));
        formLayout.Controls.Add(_tabs, 0, 0);
        formLayout.Controls.Add(footer, 0, 1);
        Controls.Add(formLayout);
    }

    private TabPage BuildHardwarePage()
    {
        TabPage page = new("Hardware controller") { Padding = new Padding(12), AutoScroll = true };
        TableLayoutPanel root = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        _hardwareEnabled.AutoSize = true;
        _hardwareEnabled.Text = "Enable USB safe-button controller";
        _hardwareEnabled.Margin = new Padding(0, 0, 0, 8);
        root.Controls.Add(_hardwareEnabled, 0, 0);

        Label explanation = new()
        {
            AutoSize = true,
            MaximumSize = new Size(880, 0),
            Text = "Supports SayoDevice O2/O2C keypads, including O2L RGB V2. F13 controls the left action; F14 controls the right action.",
            ForeColor = SystemColors.GrayText,
            Margin = new Padding(0, 0, 0, 10)
        };
        root.Controls.Add(explanation, 0, 1);

        GroupBox behaviorGroup = new()
        {
            Text = "Behavior and indicators",
            AutoSize = true,
            Dock = DockStyle.Top,
            Padding = new Padding(10),
            Margin = new Padding(0, 0, 0, 10)
        };
        TableLayoutPanel behavior = CreateSettingsTable(6);
        ConfigureDropDown(_microphoneScope, "All active input devices", "Default communications input");
        ConfigureDropDown(
            _keyRouting,
            "WinDND only",
            "Foreground application only",
            "WinDND and foreground application");
        ConfigureDropDown(_leftAction, "Toggle microphone", "Toggle output sound", "None");
        ConfigureDropDown(_rightAction, "Toggle microphone", "Toggle output sound", "None");
        ConfigureDropDown(_leftIndicator, "Microphone state", "Output-sound state", "Off");
        ConfigureDropDown(_rightIndicator, "Microphone state", "Output-sound state", "Off");
        AddSettingRow(behavior, 0, "Microphone scope", _microphoneScope);
        AddSettingRow(behavior, 1, "F13/F14 key routing", _keyRouting);

        TableLayoutPanel buttonHeaders = new() { AutoSize = true, Dock = DockStyle.Fill, ColumnCount = 2 };
        buttonHeaders.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        buttonHeaders.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        buttonHeaders.Controls.Add(new Label { Text = "Action", AutoSize = true }, 0, 0);
        buttonHeaders.Controls.Add(new Label { Text = "Indicator", AutoSize = true }, 1, 0);
        AddSettingRow(behavior, 2, "", buttonHeaders);
        AddButtonBehaviorRow(behavior, 3, "Left button (F13)", _leftAction, _leftIndicator);
        AddButtonBehaviorRow(behavior, 4, "Right button (F14)", _rightAction, _rightIndicator);

        TableLayoutPanel colors = new() { AutoSize = true, Dock = DockStyle.Fill, ColumnCount = 4 };
        colors.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        colors.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        colors.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        colors.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        _mutedColor.Width = 90;
        _unmutedColor.Width = 90;
        colors.Controls.Add(new Label { Text = "Muted", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
        colors.Controls.Add(_mutedColor, 1, 0);
        colors.Controls.Add(new Label { Text = "Unmuted", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(12, 3, 3, 3) }, 2, 0);
        colors.Controls.Add(_unmutedColor, 3, 0);
        AddSettingRow(behavior, 5, "RGB colors (#RRGGBB)", colors);
        behaviorGroup.Controls.Add(behavior);
        root.Controls.Add(behaviorGroup, 0, 2);

        GroupBox deviceGroup = new()
        {
            Text = "SayoDevice profile",
            AutoSize = true,
            Dock = DockStyle.Top,
            Padding = new Padding(10),
            Margin = new Padding(0)
        };
        TableLayoutPanel device = CreateSettingsTable(4);
        AddSettingRow(device, 0, "USB vendor ID (hex)", _vendorId);
        AddSettingRow(device, 1, "USB product IDs (hex)", _productIds);
        _configureKeys.AutoSize = true;
        _configureKeys.Text = "Program the first two keypad buttons as F13/F14 and save to device";
        AddSettingRow(device, 2, "Key assignment", _configureKeys);
        _hardwareStatus.AutoSize = true;
        _hardwareStatus.Text = string.IsNullOrWhiteSpace(_hardwareDeviceStatus)
            ? "Device detection will begin after this feature is enabled."
            : _hardwareDeviceStatus;
        _hardwareStatus.ForeColor = SystemColors.GrayText;
        AddSettingRow(device, 3, "Current status", _hardwareStatus);
        deviceGroup.Controls.Add(device);
        root.Controls.Add(deviceGroup, 0, 3);

        page.Controls.Add(root);
        return page;
    }

    private static TableLayoutPanel CreateSettingsTable(int rowCount)
    {
        TableLayoutPanel table = new()
        {
            AutoSize = true,
            Dock = DockStyle.Top,
            ColumnCount = 2,
            RowCount = rowCount
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 185));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        return table;
    }

    private static void AddSettingRow(TableLayoutPanel table, int row, string labelText, Control control)
    {
        Label label = new() { Text = labelText, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 6, 8, 3) };
        control.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        control.Margin = new Padding(3);
        table.Controls.Add(label, 0, row);
        table.Controls.Add(control, 1, row);
    }

    private static void AddButtonBehaviorRow(
        TableLayoutPanel table,
        int row,
        string label,
        ComboBox action,
        ComboBox indicator)
    {
        TableLayoutPanel values = new() { AutoSize = true, Dock = DockStyle.Fill, ColumnCount = 2 };
        values.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        values.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        action.Dock = DockStyle.Fill;
        indicator.Dock = DockStyle.Fill;
        values.Controls.Add(action, 0, 0);
        values.Controls.Add(indicator, 1, 0);
        AddSettingRow(table, row, label, values);
    }

    private static void ConfigureDropDown(ComboBox comboBox, params string[] items)
    {
        comboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        comboBox.Items.AddRange(items);
        comboBox.SelectedIndex = 0;
    }

    private void ConfigureGrid()
    {
        _grid.Dock = DockStyle.Fill;
        _grid.AllowUserToAddRows = false;
        _grid.AllowUserToDeleteRows = true;
        _grid.AllowUserToResizeRows = false;
        _grid.AutoGenerateColumns = false;
        _grid.BackgroundColor = SystemColors.Window;
        _grid.BorderStyle = BorderStyle.Fixed3D;
        _grid.MultiSelect = true;
        _grid.RowHeadersVisible = false;
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grid.Columns.Add(new DataGridViewCheckBoxColumn
        {
            HeaderText = "On",
            Width = 45,
            SortMode = DataGridViewColumnSortMode.NotSortable
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "Action",
            Width = 100,
            SortMode = DataGridViewColumnSortMode.NotSortable
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "Cron expression",
            Width = 190,
            SortMode = DataGridViewColumnSortMode.NotSortable
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "Meaning",
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
            ReadOnly = true,
            SortMode = DataGridViewColumnSortMode.NotSortable
        });
        _grid.CellEndEdit += (_, args) => UpdateExplanation(args.RowIndex);
        _grid.CellValueChanged += (_, args) =>
        {
            if (args.RowIndex >= 0 && (args.ColumnIndex == ActionColumn || args.ColumnIndex == CronColumn))
            {
                UpdateExplanation(args.RowIndex);
            }
        };
        _grid.DataError += (_, args) => args.ThrowException = false;
    }

    private void LoadSettings(AppSettings settings)
    {
        _scheduleEnabled.Checked = settings.ScheduleEnabled;
        foreach (ScheduleRule rule in settings.Rules)
        {
            int index = _grid.Rows.Add(rule.Enabled, rule.Action, rule.Cron, string.Empty);
            UpdateExplanation(index);
        }

        HardwareControllerSettings hardware = settings.Hardware;
        _hardwareEnabled.Checked = hardware.Enabled;
        _microphoneScope.SelectedIndex = hardware.MicrophoneScope == MicrophoneScope.AllActive ? 0 : 1;
        _keyRouting.SelectedIndex = ToKeyRoutingIndex(hardware.KeyRouting);
        _leftAction.SelectedIndex = ToActionIndex(hardware.LeftButtonAction);
        _rightAction.SelectedIndex = ToActionIndex(hardware.RightButtonAction);
        _leftIndicator.SelectedIndex = ToIndicatorIndex(hardware.LeftIndicator);
        _rightIndicator.SelectedIndex = ToIndicatorIndex(hardware.RightIndicator);
        _mutedColor.Text = hardware.MutedColor;
        _unmutedColor.Text = hardware.UnmutedColor;
        _configureKeys.Checked = hardware.ConfigureKeys;
        _vendorId.Text = hardware.VendorId.ToString("X4");
        _productIds.Text = string.Join(", ", hardware.ProductIds.Select(id => id.ToString("X4")));
    }

    private void AddRule()
    {
        int index = _grid.Rows.Add(true, "mute", "0 22 * * *", string.Empty);
        UpdateExplanation(index);
        _grid.CurrentCell = _grid.Rows[index].Cells[ActionColumn];
        _grid.BeginEdit(selectAll: true);
    }

    private void RemoveSelectedRules()
    {
        foreach (DataGridViewRow row in _grid.SelectedRows.Cast<DataGridViewRow>().OrderByDescending(row => row.Index))
        {
            if (!row.IsNewRow)
            {
                _grid.Rows.RemoveAt(row.Index);
            }
        }
    }

    private void UpdateExplanation(int rowIndex)
    {
        if (rowIndex < 0 || rowIndex >= _grid.Rows.Count)
        {
            return;
        }

        DataGridViewRow row = _grid.Rows[rowIndex];
        string actionText = Convert.ToString(row.Cells[ActionColumn].Value)?.Trim() ?? string.Empty;
        string cronText = Convert.ToString(row.Cells[CronColumn].Value)?.Trim() ?? string.Empty;

        ScheduleRule candidate = new() { Action = actionText, Cron = cronText };
        if (!_cronService.TryValidate(candidate, out string error))
        {
            row.Cells[ExplanationColumn].Value = $"Invalid: {error}";
            row.DefaultCellStyle.ForeColor = Color.Firebrick;
            return;
        }

        row.Cells[ExplanationColumn].Value = _cronService.Describe(cronText);
        row.DefaultCellStyle.ForeColor = SystemColors.ControlText;
    }

    private void SaveAndClose()
    {
        _grid.EndEdit();
        List<ScheduleRule> rules = [];

        foreach (DataGridViewRow row in _grid.Rows)
        {
            ScheduleRule rule = new()
            {
                Enabled = Convert.ToBoolean(row.Cells[EnabledColumn].Value ?? false),
                Action = Convert.ToString(row.Cells[ActionColumn].Value)?.Trim().ToLowerInvariant() ?? string.Empty,
                Cron = Convert.ToString(row.Cells[CronColumn].Value)?.Trim() ?? string.Empty
            };

            if (!_cronService.TryValidate(rule, out string error))
            {
                _status.ForeColor = Color.Firebrick;
                _status.Text = $"Row {row.Index + 1}: {error}";
                _grid.CurrentCell = row.Cells[string.IsNullOrWhiteSpace(rule.Action) ? ActionColumn : CronColumn];
                return;
            }

            rules.Add(rule);
        }

        if (!TryBuildHardwareSettings(out HardwareControllerSettings hardware))
        {
            return;
        }

        Result = new AppSettings
        {
            ScheduleEnabled = _scheduleEnabled.Checked,
            Rules = rules,
            Hardware = hardware
        };
        DialogResult = DialogResult.OK;
        Close();
    }

    private bool TryBuildHardwareSettings(out HardwareControllerSettings settings)
    {
        settings = new HardwareControllerSettings();
        if (!TryParseHexId(_vendorId.Text, out int vendorId))
        {
            return ShowHardwareValidation("USB vendor ID must contain four hexadecimal digits.", _vendorId);
        }

        List<int> productIds = [];
        foreach (string token in _productIds.Text.Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries))
        {
            if (!TryParseHexId(token, out int productId))
            {
                return ShowHardwareValidation("USB product IDs must be hexadecimal values separated by commas.", _productIds);
            }

            productIds.Add(productId);
        }

        if (!IsRgbColor(_mutedColor.Text))
        {
            return ShowHardwareValidation("Muted color must use #RRGGBB format.", _mutedColor);
        }

        if (!IsRgbColor(_unmutedColor.Text))
        {
            return ShowHardwareValidation("Unmuted color must use #RRGGBB format.", _unmutedColor);
        }

        settings = new HardwareControllerSettings
        {
            Enabled = _hardwareEnabled.Checked,
            MicrophoneScope = _microphoneScope.SelectedIndex == 0
                ? MicrophoneScope.AllActive
                : MicrophoneScope.DefaultCommunications,
            KeyRouting = FromKeyRoutingIndex(_keyRouting.SelectedIndex),
            LeftButtonAction = FromActionIndex(_leftAction.SelectedIndex),
            RightButtonAction = FromActionIndex(_rightAction.SelectedIndex),
            LeftIndicator = FromIndicatorIndex(_leftIndicator.SelectedIndex),
            RightIndicator = FromIndicatorIndex(_rightIndicator.SelectedIndex),
            MutedColor = _mutedColor.Text.Trim().ToUpperInvariant(),
            UnmutedColor = _unmutedColor.Text.Trim().ToUpperInvariant(),
            ConfigureKeys = _configureKeys.Checked,
            VendorId = vendorId,
            ProductIds = productIds.Distinct().ToList()
        };
        return true;
    }

    private bool ShowHardwareValidation(string message, Control control)
    {
        _hardwareStatus.ForeColor = Color.Firebrick;
        _hardwareStatus.Text = message;
        _tabs.SelectedIndex = 1;
        control.Focus();
        return false;
    }

    private static bool TryParseHexId(string value, out int result) =>
        int.TryParse(value.Trim(), System.Globalization.NumberStyles.HexNumber, null, out result) && result is >= 0 and <= 0xFFFF;

    private static bool IsRgbColor(string value) =>
        value.Trim().Length == 7 && value.Trim()[0] == '#' &&
        int.TryParse(value.Trim().AsSpan(1), System.Globalization.NumberStyles.HexNumber, null, out _);

    private static int ToActionIndex(HardwareButtonAction action) => action switch
    {
        HardwareButtonAction.ToggleMicrophone => 0,
        HardwareButtonAction.ToggleOutput => 1,
        _ => 2
    };

    private static HardwareButtonAction FromActionIndex(int index) => index switch
    {
        0 => HardwareButtonAction.ToggleMicrophone,
        1 => HardwareButtonAction.ToggleOutput,
        _ => HardwareButtonAction.None
    };

    private static int ToKeyRoutingIndex(HardwareKeyRouting routing) => routing switch
    {
        HardwareKeyRouting.WinDndOnly => 0,
        HardwareKeyRouting.ForegroundApplicationOnly => 1,
        HardwareKeyRouting.WinDndAndForegroundApplication => 2,
        _ => 0
    };

    private static HardwareKeyRouting FromKeyRoutingIndex(int index) => index switch
    {
        1 => HardwareKeyRouting.ForegroundApplicationOnly,
        2 => HardwareKeyRouting.WinDndAndForegroundApplication,
        _ => HardwareKeyRouting.WinDndOnly
    };

    private static int ToIndicatorIndex(HardwareIndicator indicator) => indicator switch
    {
        HardwareIndicator.Microphone => 0,
        HardwareIndicator.Output => 1,
        _ => 2
    };

    private static HardwareIndicator FromIndicatorIndex(int index) => index switch
    {
        0 => HardwareIndicator.Microphone,
        1 => HardwareIndicator.Output,
        _ => HardwareIndicator.None
    };
}
