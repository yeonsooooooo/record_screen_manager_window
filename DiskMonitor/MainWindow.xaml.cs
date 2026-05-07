using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace DiskMonitor;

public partial class MainWindow : Window
{
    private readonly DispatcherTimer _timer;
    private AppSettings _settings;
    private bool _checkInProgress;
    private DateTime? _nextRunAt;
    private readonly DispatcherTimer _statusTicker;

    public MainWindow()
    {
        InitializeComponent();

        _settings = AppSettings.Load();

        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMinutes(Math.Max(1, _settings.IntervalMinutes)),
        };
        _timer.Tick += async (_, _) => await RunCheckAsync(manual: false);

        _statusTicker = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1),
        };
        _statusTicker.Tick += (_, _) => UpdateStatusTexts();
        _statusTicker.Start();

        ApplySettingsToUi();
        UpdateThresholdHint();
        UpdateDriveSummary();

        Loaded += MainWindow_Loaded;
        Closing += MainWindow_Closing;
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        if (_settings.IsRunning && ValidateInputs(silent: true, out _))
        {
            StartMonitoring();
        }
        else
        {
            UpdateOnOffUi(running: false);
        }
    }

    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        SaveCurrentSettingsFromUi(persistRunning: true);
    }

    private void ApplySettingsToUi()
    {
        FolderPathBox.Text = _settings.FolderPath;
        IntervalMinutesBox.Text = _settings.IntervalMinutes.ToString(CultureInfo.InvariantCulture);

        if (_settings.ThresholdKind == ThresholdKind.UsedPercent)
            UsedPercentRadio.IsChecked = true;
        else
            FreeMbRadio.IsChecked = true;

        ThresholdValueBox.Text = _settings.ThresholdValue.ToString(CultureInfo.InvariantCulture);
    }

    private ThresholdKind GetSelectedThresholdKind() =>
        UsedPercentRadio.IsChecked == true ? ThresholdKind.UsedPercent : ThresholdKind.FreeSpaceMegabytes;

    private void UpdateThresholdHint()
    {
        if (ThresholdLabel == null || ThresholdHint == null)
            return;

        if (GetSelectedThresholdKind() == ThresholdKind.UsedPercent)
        {
            ThresholdLabel.Text = "한도 값(%)";
            ThresholdHint.Text = "예: 90 = 사용률 90% 이상일 때 1개 삭제";
        }
        else
        {
            ThresholdLabel.Text = "한도 값(MB)";
            ThresholdHint.Text = "예: 1024 = 남은 공간이 1024MB 미만일 때 1개 삭제";
        }
    }

    private void ThresholdKind_Changed(object sender, RoutedEventArgs e) => UpdateThresholdHint();

    private void BrowseFolder_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "감시할 폴더 선택",
            Multiselect = false,
        };

        if (!string.IsNullOrWhiteSpace(FolderPathBox.Text) && Directory.Exists(FolderPathBox.Text))
            dlg.InitialDirectory = FolderPathBox.Text;

        if (dlg.ShowDialog(this) == true)
        {
            FolderPathBox.Text = dlg.FolderName;
            UpdateDriveSummary();
        }
    }

    private bool ValidateInputs(bool silent, out string error)
    {
        error = "";

        var folder = FolderPathBox.Text?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(folder))
        {
            error = "감시 폴더 경로를 입력하세요.";
        }
        else if (!Directory.Exists(folder))
        {
            error = "지정한 폴더가 존재하지 않습니다.";
        }
        else if (!int.TryParse(IntervalMinutesBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var minutes)
                 || minutes < 1 || minutes > 1440)
        {
            error = "모니터링 주기는 1 ~ 1440분 사이의 정수여야 합니다.";
        }
        else if (!double.TryParse(ThresholdValueBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var th)
                 || th <= 0)
        {
            error = "한도 값은 0보다 큰 숫자여야 합니다.";
        }
        else if (GetSelectedThresholdKind() == ThresholdKind.UsedPercent && th > 100)
        {
            error = "사용률(%) 한도는 0 ~ 100 사이여야 합니다.";
        }
        else if (DiskMonitorLogic.GetDriveRootForPath(folder) is null)
        {
            error = "드라이브 정보를 판별할 수 없습니다. 로컬 경로인지 확인하세요.";
        }

        if (error.Length > 0)
        {
            if (!silent)
                MessageBox.Show(this, error, "입력 확인", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
        return true;
    }

    private void SaveCurrentSettingsFromUi(bool persistRunning)
    {
        var folder = FolderPathBox.Text?.Trim() ?? "";
        int.TryParse(IntervalMinutesBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var minutes);
        if (minutes < 1) minutes = 1;
        if (minutes > 1440) minutes = 1440;

        double.TryParse(ThresholdValueBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var th);

        _settings.FolderPath = folder;
        _settings.IntervalMinutes = minutes;
        _settings.ThresholdKind = GetSelectedThresholdKind();
        _settings.ThresholdValue = th;
        if (persistRunning)
            _settings.IsRunning = _timer.IsEnabled;

        _settings.Save();
    }

    private void SaveSettings_Click(object sender, RoutedEventArgs e)
    {
        SaveCurrentSettingsFromUi(persistRunning: false);
        AppendLog("설정을 저장했습니다.");
    }

    private void OnOffToggle_Click(object sender, RoutedEventArgs e)
    {
        if (_timer.IsEnabled)
        {
            StopMonitoring();
            _settings.IsRunning = false;
            _settings.Save();
            return;
        }

        if (!ValidateInputs(silent: false, out _))
        {
            UpdateOnOffUi(running: false);
            return;
        }

        SaveCurrentSettingsFromUi(persistRunning: false);
        StartMonitoring();
        _settings.IsRunning = true;
        _settings.Save();
    }

    private void StartMonitoring()
    {
        var minutes = Math.Max(1, _settings.IntervalMinutes);
        _timer.Interval = TimeSpan.FromMinutes(minutes);
        _timer.Start();
        _nextRunAt = DateTime.Now.AddMinutes(minutes);

        UpdateOnOffUi(running: true);
        AppendLog($"모니터링을 시작합니다. 주기 {minutes}분, 폴더: {_settings.FolderPath}");

        _ = RunCheckAsync(manual: false);
    }

    private void StopMonitoring()
    {
        _timer.Stop();
        _nextRunAt = null;
        UpdateOnOffUi(running: false);
        AppendLog("모니터링을 중지했습니다.");
    }

    private void UpdateOnOffUi(bool running)
    {
        OnOffToggle.IsChecked = running;
        OnOffToggle.Content = running ? "ON" : "OFF";
        StatusText.Text = running ? "동작 중" : "중지됨";
        StatusText.Foreground = running
            ? System.Windows.Media.Brushes.DarkGreen
            : System.Windows.Media.Brushes.DarkRed;

        FolderPathBox.IsEnabled = !running;
        IntervalMinutesBox.IsEnabled = !running;
        ThresholdValueBox.IsEnabled = !running;
        UsedPercentRadio.IsEnabled = !running;
        FreeMbRadio.IsEnabled = !running;
    }

    private async void RunOnce_Click(object sender, RoutedEventArgs e)
    {
        if (!ValidateInputs(silent: false, out _))
            return;

        SaveCurrentSettingsFromUi(persistRunning: false);
        await RunCheckAsync(manual: true);
    }

    private async Task RunCheckAsync(bool manual)
    {
        if (_checkInProgress)
        {
            if (manual)
                AppendLog("이미 검사가 진행 중입니다.");
            return;
        }

        _checkInProgress = true;
        try
        {
            var folder = _settings.FolderPath;
            var kind = _settings.ThresholdKind;
            var thresholdValue = _settings.ThresholdValue;

            UpdateDriveSummary();

            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
            {
                AppendLog("폴더가 유효하지 않아 검사를 건너뜁니다.");
                _nextRunAt = _timer.IsEnabled ? DateTime.Now.Add(_timer.Interval) : null;
                return;
            }

            var (triggered, triggerError) = await Task.Run(() =>
            {
                var hit = DiskMonitorLogic.ShouldTrigger(folder, kind, thresholdValue, out var err);
                return (hit, err);
            });

            if (!triggered)
            {
                if (!string.IsNullOrEmpty(triggerError))
                    AppendLog($"검사 건너뜀: {triggerError}");
                else
                    AppendLog(BuildOkMessage(folder, kind, thresholdValue));

                _nextRunAt = _timer.IsEnabled ? DateTime.Now.Add(_timer.Interval) : null;
                return;
            }

            AppendLog($"한도 초과 감지 → 가장 오래된 파일 1개를 삭제합니다.");

            var (ok, deletedPath, deleteError) = await Task.Run(() =>
            {
                var success = DiskMonitorLogic.TryDeleteOldestFile(folder, out var dp, out var de);
                return (success, dp, de);
            });

            if (ok)
                AppendLog($"삭제 완료: {deletedPath}");
            else
                AppendLog($"삭제 실패: {deleteError}");

            UpdateDriveSummary();
            _nextRunAt = _timer.IsEnabled ? DateTime.Now.Add(_timer.Interval) : null;
        }
        catch (Exception ex)
        {
            AppendLog($"오류: {ex.Message}");
        }
        finally
        {
            _checkInProgress = false;
        }
    }

    private string BuildOkMessage(string folder, ThresholdKind kind, double thresholdValue)
    {
        if (!DiskMonitorLogic.TryGetDriveMetrics(folder, out var drive, out var usedPercent, out var freeBytes))
            return "검사 완료 (드라이브 정보 없음)";

        if (kind == ThresholdKind.UsedPercent)
            return $"검사 완료 - 사용률 {usedPercent:0.0}% (한도 {thresholdValue:0.0}%) [{drive.Name}]";

        var freeMb = freeBytes / 1024.0 / 1024.0;
        return $"검사 완료 - 남은 공간 {freeMb:0.0}MB (한도 {thresholdValue:0.0}MB) [{drive.Name}]";
    }

    private void UpdateDriveSummary()
    {
        var folder = FolderPathBox.Text?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(folder))
        {
            DriveSummaryText.Text = "드라이브 정보를 확인하려면 경로를 입력하세요.";
            return;
        }

        if (!Directory.Exists(folder))
        {
            DriveSummaryText.Text = "지정한 폴더가 존재하지 않습니다.";
            return;
        }

        if (!DiskMonitorLogic.TryGetDriveMetrics(folder, out var drive, out var usedPercent, out var freeBytes))
        {
            DriveSummaryText.Text = "드라이브 정보를 읽을 수 없습니다.";
            return;
        }

        var totalGb = drive.TotalSize / 1024.0 / 1024.0 / 1024.0;
        var freeGb = freeBytes / 1024.0 / 1024.0 / 1024.0;
        DriveSummaryText.Text =
            $"드라이브 {drive.Name}  |  사용률 {usedPercent:0.0}%  |  남은 공간 {freeGb:0.00}GB / 총 {totalGb:0.00}GB";
    }

    private void UpdateStatusTexts()
    {
        if (_timer.IsEnabled && _nextRunAt is { } next)
        {
            var remain = next - DateTime.Now;
            if (remain < TimeSpan.Zero) remain = TimeSpan.Zero;
            NextRunText.Text = $"다음 검사까지: {(int)remain.TotalMinutes:00}:{remain.Seconds:00}";
        }
        else
        {
            NextRunText.Text = "";
        }
    }

    private void AppendLog(string message)
    {
        var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}";
        if (Dispatcher.CheckAccess())
            AppendLogCore(line);
        else
            Dispatcher.BeginInvoke(new Action(() => AppendLogCore(line)));
    }

    private void AppendLogCore(string line)
    {
        LogBox.AppendText(line);
        LogBox.ScrollToEnd();
    }

    private void ClearLog_Click(object sender, RoutedEventArgs e) => LogBox.Clear();
}
