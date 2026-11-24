using System.Drawing;
using System.Windows.Forms;
using Grand_Theft_Auto_V_Platform_Switcher.Models;
using Grand_Theft_Auto_V_Platform_Switcher.Services;
using Grand_Theft_Auto_V_Platform_Switcher.UI.Controls;
using Grand_Theft_Auto_V_Platform_Switcher.UI.Views;

namespace Grand_Theft_Auto_V_Platform_Switcher.UI;

public sealed class DoctorView : UserControl
{
    private readonly EditionDetector _detector;
    private readonly DoctorService _doctorService;
    private readonly PlatformSwitchService _switchService;
    private readonly BattlEyeService _battlEyeService;
    private readonly ProcessGuard _processGuard;
    private readonly RegistryService _registryService;

    private EditionSpec _edition = EditionSpec.Legacy;
    private EditionSettings _editionSettings = new();
    private ThemePalette _palette = ThemeManager.Dark;

    private readonly BufferedPanel _scrollHost;
    private readonly BufferedTableLayoutPanel _root;

    // 1. Summary Bar
    private readonly CardPanel _summaryCard;
    private readonly BufferedTableLayoutPanel _summaryLayout;
    private readonly StatusPill _pillOverall;
    private readonly Label _lblSummary;
    private readonly ActionButton _btnRerun;
    private readonly ActionButton _btnCopyReport;

    // 2. Compact Checks Table Card
    private readonly CardPanel _checksCard;
    private readonly BufferedTableLayoutPanel _checksTable;
    private readonly List<CheckRowRefs> _rowRefs = new();

    // 3. Manual Rockstar Registry Strip
    private readonly CardPanel _registryCard;
    private readonly BufferedTableLayoutPanel _registryLayout;
    private readonly Label _lblRegTitle;
    private readonly StatusPill _pillAdminStatus;
    private readonly ActionButton _btnRestartAdmin;
    private readonly ActionButton _btnLinkSteam;
    private readonly ActionButton _btnUnlinkSteam;
    private readonly ActionButton _btnLinkEpic;
    private readonly ActionButton _btnUnlinkEpic;

    private readonly ToolTip _toolTip = new();
    private DoctorReport? _latestReport;
    private bool _isBusy;
    private bool _externalBusy;
    private bool _isElevated;
    private bool _showingFeedback;
    private bool _feedbackIsWarning;
    private bool _isUpdatingLayout;

    private sealed class CheckRowRefs
    {
        public required StatusPill Badge { get; init; }
        public required Label Title { get; init; }
        public required Label Summary { get; init; }
        public ActionButton? FixButton { get; init; }
        public required DoctorSeverity Severity { get; init; }
    }

    public event EventHandler? SettingsChanged;
    public event EventHandler? ElevationRequested;
    public event EventHandler<DoctorReport>? ReportUpdated;
    public event EventHandler<bool>? BusyChanged;
    public event EventHandler<string>? StatusMessage;
    public event EventHandler<GamePlatform>? LinkRegistryRequested;
    public event EventHandler<GamePlatform>? UnlinkRegistryRequested;

    public bool IsBusy => _isBusy;

    public DoctorView(
        EditionDetector detector,
        DoctorService doctorService,
        PlatformSwitchService switchService,
        BattlEyeService battlEyeService,
        ProcessGuard? processGuard = null,
        RegistryService? registryService = null)
    {
        _detector = detector;
        _doctorService = doctorService;
        _switchService = switchService;
        _battlEyeService = battlEyeService;
        _processGuard = processGuard ?? new ProcessGuard();
        _registryService = registryService ?? new RegistryService(new AppLogger());

        AutoScaleMode = AutoScaleMode.Inherit;
        Dock = DockStyle.Fill;
        Margin = new Padding(0);
        Padding = new Padding(0);

        _scrollHost = new BufferedPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            Margin = new Padding(0),
            Padding = new Padding(0)
        };

        _root = new BufferedTableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(10, 2, 10, 2),
            Margin = new Padding(0)
        };
        _root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        _root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        _root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        // 1. Summary Bar
        _summaryCard = new CardPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(9, 2, 9, 2),
            Margin = new Padding(0, 0, 0, 3),
            CornerRadius = 8
        };
        _summaryLayout = new BufferedTableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 4,
            RowCount = 1,
            Margin = new Padding(0),
            Padding = new Padding(0)
        };
        _summaryLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _summaryLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        _summaryLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _summaryLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _summaryLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        _pillOverall = new StatusPill
        {
            Font = new Font("Segoe UI Semibold", 8F),
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 0, 8, 0)
        };
        _pillOverall.SetState("Checking", StatusTone.Neutral);

        _lblSummary = new Label
        {
            Text = "Running health checks...",
            Font = new Font("Segoe UI Semibold", 8.6F),
            UseMnemonic = false,
            AutoSize = false,
            AutoEllipsis = true,
            Size = new Size(10, 1),
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = new Padding(0, 0, 8, 0),
            TabStop = false
        };

        _btnRerun = new ActionButton
        {
            Text = "Re-check",
            Font = new Font("Segoe UI", 8F),
            AutoSize = true,
            Padding = new Padding(6, 0, 6, 0),
            Margin = new Padding(0, 0, 4, 0),
            Anchor = AnchorStyles.Right
        };
        _btnCopyReport = new ActionButton
        {
            Text = "Copy Report",
            Font = new Font("Segoe UI", 8F),
            AutoSize = true,
            Padding = new Padding(6, 0, 6, 0),
            Margin = new Padding(0),
            Anchor = AnchorStyles.Right
        };

        _btnRerun.Click += async (_, _) => await RunDiagnosticsAsync();
        _btnCopyReport.Click += async (_, _) => await CopyDiagnosticReportAsync();

        _summaryLayout.Controls.Add(_pillOverall, 0, 0);
        _summaryLayout.Controls.Add(_lblSummary, 1, 0);
        _summaryLayout.Controls.Add(_btnRerun, 2, 0);
        _summaryLayout.Controls.Add(_btnCopyReport, 3, 0);
        _summaryCard.Controls.Add(_summaryLayout);

        // 2. Dense Checks Table
        _checksCard = new CardPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(8, 1, 8, 1),
            Margin = new Padding(0, 0, 0, 3),
            CornerRadius = 8
        };

        _checksTable = new BufferedTableLayoutPanel
        {
            Dock = DockStyle.Top,
            BackColor = Color.Transparent,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 4,
            RowCount = 0,
            Margin = new Padding(0),
            Padding = new Padding(0)
        };
        _checksTable.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _checksTable.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _checksTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        _checksTable.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _checksCard.Controls.Add(_checksTable);

        // 3. Manual Rockstar Registry Fixes Strip
        _registryCard = new CardPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(9, 2, 9, 2),
            Margin = new Padding(0),
            CornerRadius = 8
        };

        _registryLayout = new BufferedTableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            RowCount = 2,
            Margin = new Padding(0),
            Padding = new Padding(0)
        };
        _registryLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        _registryLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _registryLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var regTopRow = new BufferedTableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 3,
            RowCount = 1,
            Margin = new Padding(0, 0, 0, 2),
            Padding = new Padding(0)
        };
        regTopRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        regTopRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        regTopRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        regTopRow.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        _lblRegTitle = new Label
        {
            Text = "Manual Rockstar Registry Fixes (only if Rockstar Launcher won't recognize Steam or Epic)",
            Font = new Font("Segoe UI Semibold", 8.5F),
            UseMnemonic = false,
            AutoSize = false,
            AutoEllipsis = true,
            Size = new Size(10, 1),
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = new Padding(0, 0, 6, 0),
            TabStop = false
        };
        _pillAdminStatus = new StatusPill
        {
            Font = new Font("Segoe UI Semibold", 7.8F),
            Anchor = AnchorStyles.Right,
            Margin = new Padding(0, 0, 6, 0)
        };
        _btnRestartAdmin = new ActionButton
        {
            Text = "Restart as Admin",
            Font = new Font("Segoe UI", 7.9F),
            Variant = ActionButtonVariant.PrimaryAccent,
            AutoSize = true,
            Padding = new Padding(6, 0, 6, 0),
            Margin = new Padding(0),
            Anchor = AnchorStyles.Right
        };
        _btnRestartAdmin.Click += (_, _) =>
        {
            if (_isBusy || _externalBusy)
            {
                return;
            }

            if (_registryService.RestartAsAdministrator())
            {
                ElevationRequested?.Invoke(this, EventArgs.Empty);
            }
        };

        regTopRow.Controls.Add(_lblRegTitle, 0, 0);
        regTopRow.Controls.Add(_pillAdminStatus, 1, 0);
        regTopRow.Controls.Add(_btnRestartAdmin, 2, 0);

        var regButtonsRow = new BufferedTableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 4,
            RowCount = 1,
            Margin = new Padding(0),
            Padding = new Padding(0)
        };
        regButtonsRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
        regButtonsRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
        regButtonsRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
        regButtonsRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
        regButtonsRow.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        _btnLinkSteam = CreateRegistryButton("Link Steam", new Padding(0, 0, 3, 0),
            "Register your GTA V folder for Steam in Rockstar Games Launcher");
        _btnUnlinkSteam = CreateRegistryButton("Unlink Steam", new Padding(3, 0, 3, 0),
            "Stop Rockstar Games Launcher from redirecting launches to Steam");
        _btnLinkEpic = CreateRegistryButton("Link Epic", new Padding(3, 0, 3, 0),
            "Register your GTA V folder for Epic Games in Rockstar Games Launcher");
        _btnUnlinkEpic = CreateRegistryButton("Unlink Epic", new Padding(3, 0, 0, 0),
            "Stop Rockstar Games Launcher from redirecting launches to Epic Games");

        _btnLinkSteam.Click += (_, _) => LinkRegistryRequested?.Invoke(this, GamePlatform.Steam);
        _btnUnlinkSteam.Click += (_, _) => UnlinkRegistryRequested?.Invoke(this, GamePlatform.Steam);
        _btnLinkEpic.Click += (_, _) => LinkRegistryRequested?.Invoke(this, GamePlatform.EGL);
        _btnUnlinkEpic.Click += (_, _) => UnlinkRegistryRequested?.Invoke(this, GamePlatform.EGL);

        regButtonsRow.Controls.Add(_btnLinkSteam, 0, 0);
        regButtonsRow.Controls.Add(_btnUnlinkSteam, 1, 0);
        regButtonsRow.Controls.Add(_btnLinkEpic, 2, 0);
        regButtonsRow.Controls.Add(_btnUnlinkEpic, 3, 0);

        _registryLayout.Controls.Add(regTopRow, 0, 0);
        _registryLayout.Controls.Add(regButtonsRow, 0, 1);
        _registryCard.Controls.Add(_registryLayout);

        _root.Controls.Add(_summaryCard, 0, 0);
        _root.Controls.Add(_checksCard, 0, 1);
        _root.Controls.Add(_registryCard, 0, 2);

        _scrollHost.Controls.Add(_root);
        Controls.Add(_scrollHost);

        _scrollHost.SizeChanged += (_, _) => UpdateLayout();
        SizeChanged += (_, _) => UpdateLayout();
    }

    private ActionButton CreateRegistryButton(string text, Padding margin, string tooltip)
    {
        var btn = new ActionButton
        {
            Text = text,
            Font = new Font("Segoe UI", 8F),
            Dock = DockStyle.Fill,
            AutoSize = true,
            Padding = new Padding(4, 0, 4, 0),
            Margin = margin
        };
        _toolTip.SetToolTip(btn, tooltip);
        return btn;
    }

    public void ApplyTheme(ThemePalette palette)
    {
        _palette = palette;
        BackColor = palette.WindowBg;
        ForeColor = palette.TextPrimary;
        _scrollHost.BackColor = palette.WindowBg;
        _root.BackColor = palette.WindowBg;

        _summaryCard.ApplyTheme(palette);
        _checksCard.ApplyTheme(palette);
        _registryCard.ApplyTheme(palette);

        _pillOverall.ApplyTheme(palette);
        _btnRerun.ApplyTheme(palette);
        _btnCopyReport.ApplyTheme(palette);

        _lblRegTitle.ForeColor = palette.TextPrimary;
        _pillAdminStatus.ApplyTheme(palette);
        _btnRestartAdmin.ApplyTheme(palette);
        _btnLinkSteam.ApplyTheme(palette);
        _btnUnlinkSteam.ApplyTheme(palette);
        _btnLinkEpic.ApplyTheme(palette);
        _btnUnlinkEpic.ApplyTheme(palette);

        if (_showingFeedback)
        {
            _lblSummary.ForeColor = _feedbackIsWarning ? palette.WarningText : palette.SuccessText;
        }
        else if (_latestReport is not null)
        {
            UpdateSummaryDisplay(_latestReport);
        }
        else
        {
            _lblSummary.ForeColor = palette.TextSecondary;
        }

        foreach (var row in _rowRefs)
        {
            row.Badge.ApplyTheme(palette);
            row.Title.ForeColor = palette.TextPrimary;
            row.Summary.ForeColor = palette.TextSecondary;
            row.FixButton?.ApplyTheme(palette);
        }
    }

    public void SetExternalBusy(bool busy)
    {
        _externalBusy = busy;
        UpdateButtonStates();
    }

    public async Task LoadAndRunAsync(EditionSpec edition, EditionSettings editionSettings)
    {
        _edition = edition;
        _editionSettings = editionSettings;
        _showingFeedback = false;
        await RunDiagnosticsAsync();
    }

    public void UpdateFromReport(EditionSpec edition, EditionSettings editionSettings, DoctorReport report)
    {
        _edition = edition;
        _editionSettings = editionSettings;
        ApplyReport(report);
    }

    private void SetViewBusy(bool busy, string? statusMessage = null)
    {
        _isBusy = busy;
        UseWaitCursor = busy;
        UpdateButtonStates();

        if (!string.IsNullOrWhiteSpace(statusMessage))
        {
            _showingFeedback = false;
            _pillOverall.SetState("Checking", StatusTone.Neutral);
            _lblSummary.Text = statusMessage;
            _lblSummary.ForeColor = _palette.TextSecondary;
        }

        BusyChanged?.Invoke(this, busy);
    }

    private void UpdateButtonStates()
    {
        var locked = _isBusy || _externalBusy;
        _btnRerun.Enabled = !locked;
        _btnCopyReport.Enabled = !locked && _latestReport is not null;
        _btnRestartAdmin.Enabled = !locked && !_isElevated;
        _btnLinkSteam.Enabled = !locked && _isElevated;
        _btnUnlinkSteam.Enabled = !locked && _isElevated;
        _btnLinkEpic.Enabled = !locked && _isElevated;
        _btnUnlinkEpic.Enabled = !locked && _isElevated;

        foreach (var row in _rowRefs)
        {
            if (row.FixButton is not null && !row.FixButton.IsDisposed)
            {
                row.FixButton.Enabled = !locked;
            }
        }
    }

    private void ShowInlineFeedback(string message, bool isWarning = false)
    {
        _showingFeedback = true;
        _feedbackIsWarning = isWarning;
        _pillOverall.SetState(isWarning ? "Notice" : "Updated", isWarning ? StatusTone.Warning : StatusTone.Success);
        _lblSummary.Text = message;
        _lblSummary.ForeColor = isWarning ? _palette.WarningText : _palette.SuccessText;
        _toolTip.SetToolTip(_lblSummary, message);
    }

    private async Task RunDiagnosticsAsync()
    {
        if (_isBusy)
        {
            return;
        }

        SetViewBusy(true, $"Checking {_edition.DisplayName}...");
        try
        {
            var report = await Task.Run(() => _doctorService.Diagnose(_edition, _editionSettings));
            if (IsDisposed)
            {
                return;
            }

            ApplyReport(report);
            ReportUpdated?.Invoke(this, report);
        }
        finally
        {
            if (!IsDisposed)
            {
                SetViewBusy(false);
            }
        }
    }

    private void ApplyReport(DoctorReport report)
    {
        _latestReport = report;
        _isElevated = report.IsElevated;
        _showingFeedback = false;

        UpdateSummaryDisplay(report);

        if (_isElevated)
        {
            _pillAdminStatus.SetState("Admin active", StatusTone.Success);
            _btnRestartAdmin.Visible = false;
        }
        else
        {
            _pillAdminStatus.SetState("Admin needed", StatusTone.Warning);
            _btnRestartAdmin.Visible = true;
        }

        _checksTable.SuspendLayout();
        while (_checksTable.Controls.Count > 0)
        {
            var ctrl = _checksTable.Controls[0];
            _checksTable.Controls.RemoveAt(0);
            ctrl.Dispose();
        }
        _checksTable.RowStyles.Clear();
        _rowRefs.Clear();

        _checksTable.RowCount = report.Checks.Count;
        for (var i = 0; i < report.Checks.Count; i++)
        {
            _checksTable.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            AddCheckRow(report.Checks[i], i);
        }
        _checksTable.ResumeLayout(true);

        UpdateButtonStates();
        UpdateLayout();
    }

    private void UpdateSummaryDisplay(DoctorReport report)
    {
        if (report.ErrorCount > 0)
        {
            _pillOverall.SetState($"{report.ErrorCount} Error{(report.ErrorCount == 1 ? "" : "s")}", StatusTone.Danger);
            _lblSummary.Text = $"{_edition.DisplayName}: {report.ErrorCount} error(s), {report.WarningCount} warning(s) — fix items below.";
            _lblSummary.ForeColor = _palette.DangerText;
        }
        else if (report.WarningCount > 0)
        {
            _pillOverall.SetState($"{report.WarningCount} Warning{(report.WarningCount == 1 ? "" : "s")}", StatusTone.Warning);
            _lblSummary.Text = $"{_edition.DisplayName}: {report.WarningCount} item(s) need attention.";
            _lblSummary.ForeColor = _palette.WarningText;
        }
        else
        {
            _pillOverall.SetState("All Good", StatusTone.Success);
            _lblSummary.Text = $"{_edition.DisplayName}: All health checks passed.";
            _lblSummary.ForeColor = _palette.SuccessText;
        }

        _toolTip.SetToolTip(_lblSummary, _lblSummary.Text);
    }

    private void AddCheckRow(DoctorCheckItem check, int rowIndex)
    {
        var hasFix = check.FixType != DoctorFixType.None && !string.IsNullOrWhiteSpace(check.FixLabel);
        var (tone, badgeText) = check.Severity switch
        {
            DoctorSeverity.Ok => (StatusTone.Success, "OK"),
            DoctorSeverity.Warning => (StatusTone.Warning, "WARN"),
            _ => (StatusTone.Danger, "ERROR")
        };

        var rowMargin = new Padding(0);
        var badge = new StatusPill
        {
            Font = new Font("Segoe UI Semibold", 7.5F),
            MinimumSize = new Size(44, 15),
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 0, 6, 0)
        };
        badge.SetState(badgeText, tone);
        badge.ApplyTheme(_palette);

        var lblTitle = new Label
        {
            Text = check.Title,
            Font = new Font("Segoe UI Semibold", 8.3F),
            ForeColor = _palette.TextPrimary,
            UseMnemonic = false,
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 0, 6, 0),
            TabStop = false
        };

        var lblSummary = new SingleLineEllipsisLabel
        {
            Text = check.Summary,
            Font = new Font("Segoe UI", 8.3F),
            ForeColor = _palette.TextSecondary,
            Dock = DockStyle.Fill,
            Margin = rowMargin
        };

        var tipText = string.IsNullOrWhiteSpace(check.Details)
            ? check.Summary
            : $"{check.Summary}\n\n{check.Details}";
        _toolTip.SetToolTip(badge, tipText);
        _toolTip.SetToolTip(lblTitle, tipText);
        _toolTip.SetToolTip(lblSummary, tipText);

        _checksTable.Controls.Add(badge, 0, rowIndex);
        _checksTable.Controls.Add(lblTitle, 1, rowIndex);
        _checksTable.Controls.Add(lblSummary, 2, rowIndex);

        ActionButton? btnFix = null;
        if (hasFix)
        {
            var variant = check.Severity switch
            {
                DoctorSeverity.Error => ActionButtonVariant.Danger,
                DoctorSeverity.Warning => ActionButtonVariant.PrimaryAccent,
                _ => ActionButtonVariant.Secondary
            };
            btnFix = new ActionButton
            {
                Text = check.FixLabel,
                Font = new Font("Segoe UI", 7.8F),
                Variant = variant,
                AutoSize = true,
                Padding = new Padding(5, 0, 5, 0),
                Margin = new Padding(6, 0, 0, 0),
                Anchor = AnchorStyles.Right,
                Enabled = !_isBusy && !_externalBusy
            };
            btnFix.ApplyTheme(_palette);
            btnFix.Click += async (_, _) => await HandleFixClickAsync(check);
            _checksTable.Controls.Add(btnFix, 3, rowIndex);
        }

        _rowRefs.Add(new CheckRowRefs
        {
            Badge = badge,
            Title = lblTitle,
            Summary = lblSummary,
            FixButton = btnFix,
            Severity = check.Severity
        });
    }

    private async Task HandleFixClickAsync(DoctorCheckItem check)
    {
        if (_isBusy || _externalBusy)
        {
            return;
        }

        if (check.FixType == DoctorFixType.BrowseGamePath)
        {
            using var dialog = new FolderBrowserDialog
            {
                Description = $"Select your {_edition.DisplayName} folder containing {_edition.MainExecutable}",
                UseDescriptionForTitle = true
            };
            if (dialog.ShowDialog(FindForm()) == DialogResult.OK)
            {
                _editionSettings.GameDirectory = dialog.SelectedPath;
                _editionSettings.CurrentPlatform = _detector.DetectActivePlatform(_edition, dialog.SelectedPath);
                SettingsChanged?.Invoke(this, EventArgs.Empty);
                ShowInlineFeedback($"Game folder updated to {dialog.SelectedPath}.");
                StatusMessage?.Invoke(this, $"Game folder updated to {dialog.SelectedPath}.");
                await RunDiagnosticsAsync();
            }
            return;
        }

        if (check.FixType == DoctorFixType.AdoptEpicInstallation)
        {
            var inspection = await Task.Run(() => _doctorService.InspectEpicManifest(_edition, _editionSettings));
            if (IsDisposed)
            {
                return;
            }

            if (inspection.State == EpicManifestState.NotFound ||
                (!inspection.HasTargetEgstore && !inspection.HasSourceEgstore))
            {
                using var setupDialog = new EpicSetupDialog(_edition, _processGuard, _palette);
                if (setupDialog.ShowDialog(FindForm()) != DialogResult.OK)
                {
                    return;
                }

                SetViewBusy(true, "Linking Epic Games installation...");
                try
                {
                    var folderAdoptRes = await _doctorService.AdoptEpicFromFolderAsync(
                        _edition,
                        _editionSettings,
                        setupDialog.SelectedTempFolder);
                    if (IsDisposed)
                    {
                        return;
                    }

                    if (folderAdoptRes.Success)
                    {
                        SettingsChanged?.Invoke(this, EventArgs.Empty);
                        ShowInlineFeedback("Epic Games installation linked.");
                        StatusMessage?.Invoke(this, "Epic Games installation linked.");
                        MessageBox.Show(FindForm(), folderAdoptRes.Message, "Epic Games Linked", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    else
                    {
                        ShowInlineFeedback(folderAdoptRes.Message, isWarning: true);
                        MessageBox.Show(FindForm(), folderAdoptRes.Message, "Setup Could Not Be Applied", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                }
                finally
                {
                    if (!IsDisposed)
                    {
                        SetViewBusy(false);
                        await RunDiagnosticsAsync();
                    }
                }
                return;
            }
        }

        SetViewBusy(true, $"Applying fix: {check.FixLabel}...");
        var shouldRerun = true;
        OperationResult? appliedResult = null;
        try
        {
            appliedResult = await _doctorService.ApplyFixAsync(check, _edition, _editionSettings);
            if (IsDisposed)
            {
                return;
            }

            if (appliedResult.Success)
            {
                SettingsChanged?.Invoke(this, EventArgs.Empty);
                if (check.FixType == DoctorFixType.RestartAsAdmin)
                {
                    shouldRerun = false;
                    SetViewBusy(false);
                    ElevationRequested?.Invoke(this, EventArgs.Empty);
                    return;
                }

                StatusMessage?.Invoke(this, appliedResult.Message);
            }
            else
            {
                MessageBox.Show(FindForm(), appliedResult.Message, "Fix Could Not Be Applied", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
        finally
        {
            if (!IsDisposed && shouldRerun)
            {
                SetViewBusy(false);
                await RunDiagnosticsAsync();
                if (appliedResult is not null)
                {
                    ShowInlineFeedback(appliedResult.Message, isWarning: !appliedResult.Success);
                }
            }
        }
    }

    private async Task CopyDiagnosticReportAsync()
    {
        if (_isBusy || _externalBusy || _latestReport is null)
        {
            return;
        }

        var reportSnapshot = _latestReport;
        SetViewBusy(true, "Preparing support report...");
        try
        {
            var markdown = await Task.Run(() =>
            {
                var versions = _switchService.GetVersionSnapshot(_edition, _editionSettings.GameDirectory);
                var beStatus = _battlEyeService.Inspect(_edition, _editionSettings.GameDirectory);
                return _doctorService.BuildMarkdownReport(reportSnapshot, versions, beStatus);
            });

            if (IsDisposed)
            {
                return;
            }

            Clipboard.SetText(markdown);
            StatusMessage?.Invoke(this, "Support report copied to clipboard.");
            ShowInlineFeedback("Support report copied to clipboard.");
        }
        catch (Exception ex)
        {
            MessageBox.Show(FindForm(), $"Unable to copy report to clipboard: {ex.Message}", "Copy Failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally
        {
            if (!IsDisposed)
            {
                SetViewBusy(false);
            }
        }
    }

    internal void UpdateLayout()
    {
        if (_isUpdatingLayout || IsDisposed || _scrollHost.IsDisposed || _scrollHost.ClientSize.Width <= 0)
        {
            return;
        }

        _isUpdatingLayout = true;
        try
        {
            var hostW = _scrollHost.ClientSize.Width;
            var hostH = Math.Max(0, _scrollHost.ClientSize.Height);
            var usableW = Math.Max(260, hostW - _root.Padding.Horizontal);

            var summaryH = _summaryCard.GetPreferredSize(new Size(usableW, 0)).Height + _summaryCard.Margin.Vertical;
            var checksTableH = _checksTable.GetPreferredSize(new Size(Math.Max(200, usableW - _checksCard.Padding.Horizontal), 0)).Height;
            var checksH = checksTableH + _checksCard.Padding.Vertical + _checksCard.Margin.Vertical;
            var registryH = _registryCard.GetPreferredSize(new Size(usableW, 0)).Height + _registryCard.Margin.Vertical;
            var requiredHeight = _root.Padding.Vertical + summaryH + checksH + registryH;

            var needsScroll = requiredHeight > hostH && hostH > 0;

            _root.SuspendLayout();
            if (needsScroll)
            {
                _scrollHost.AutoScroll = true;
                var scrollW = Math.Max(260, _scrollHost.ClientSize.Width);
                _root.Dock = DockStyle.Top;
                _root.MinimumSize = new Size(scrollW, requiredHeight);
                _root.MaximumSize = new Size(scrollW, 0);
            }
            else
            {
                _scrollHost.AutoScroll = false;
                _scrollHost.AutoScrollMinSize = Size.Empty;
                _root.MinimumSize = Size.Empty;
                _root.MaximumSize = Size.Empty;
                _root.Dock = DockStyle.Fill;
            }
            _root.ResumeLayout(true);
        }
        finally
        {
            _isUpdatingLayout = false;
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _toolTip.Dispose();
        }
        base.Dispose(disposing);
    }

    internal Task RunDiagnosticsForTestAsync() => RunDiagnosticsAsync();
    internal void ShowInlineFeedbackForTest(string message, bool isWarning) => ShowInlineFeedback(message, isWarning);
    internal Label InlineFeedbackLabelForTest => _lblSummary;
    internal TableLayoutPanel ChecksListPanelForTest => _checksTable;
    internal BufferedPanel ScrollHostForTest => _scrollHost;
    internal bool IsBusyForTest => _isBusy;
    internal IReadOnlyList<Control> GetBusyGuardedControlsForTest() =>
        [_btnRerun, _btnCopyReport, _btnRestartAdmin, _btnLinkSteam, _btnUnlinkSteam, _btnLinkEpic, _btnUnlinkEpic];
}
