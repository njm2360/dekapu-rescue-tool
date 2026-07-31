using System.Collections.ObjectModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DekapuRescueTool.Models;
using DekapuRescueTool.Services;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;

namespace DekapuRescueTool.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly ILogReaderService _logReader;
    private readonly ISaveService _saveService;
    private readonly IDialogService _dialog;

    private IReadOnlyDictionary<string, List<SaveEntry>> _allData
        = new Dictionary<string, List<SaveEntry>>();

    private ScatterSeries _highlightSeries = new();

    private const double MarkerVisibleSpanDays = 1.0 / 24;
    private const double MinTimeRangeDays = 1.0 / 1440;
    private const double TimePaddingDays = 30.0 / 86400;

    public ObservableCollection<string> UserIds { get; } = new();
    public ObservableCollection<DateOnly> AvailableDates { get; } = new();
    public ObservableCollection<SaveEntry> DayEntries { get; } = new();

    [ObservableProperty] public partial string? SelectedUserId { get; set; }
    [ObservableProperty] public partial DateOnly? SelectedDate { get; set; }
    [ObservableProperty] public partial int CurrentIndex { get; set; }
    [ObservableProperty] public partial SaveEntry? CurrentEntry { get; set; }
    [ObservableProperty] public partial PlotModel PlotModel { get; set; } = new();
    [ObservableProperty] public partial string CurrentLogDir { get; set; }
    [ObservableProperty] public partial bool IsLoading { get; set; }
    [ObservableProperty] public partial string StatusMessage { get; set; } = "";

    public MainViewModel(ILogReaderService logReader, ISaveService saveService, IDialogService dialog)
    {
        _logReader = logReader;
        _saveService = saveService;
        _dialog = dialog;
        CurrentLogDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "AppData", "LocalLow", "VRChat", "VRChat");
    }

    public Task InitializeAsync() => LoadDataAsync();

    private async Task LoadDataAsync()
    {
        if (IsLoading) return;

        IsLoading = true;
        StatusMessage = "ログを読み込んでいます...";
        try
        {
            var dir = CurrentLogDir;
            var data = await Task.Run(() => _logReader.ReadAllLogs(dir));
            _allData = data.Entries;

            UserIds.Clear();
            SelectedUserId = null;

            foreach (var uid in _allData.Keys.OrderBy(k => k))
                UserIds.Add(uid);

            SelectedUserId = UserIds.FirstOrDefault();

            StatusMessage = $"読み込み完了: レコード {_allData.Values.Sum(l => l.Count)}件";
            if (data.UnknownVersionCount > 0)
                StatusMessage += $" (未対応バージョン {data.UnknownVersionCount}件をスキップ)";
        }
        catch (Exception ex)
        {
            StatusMessage = "ログの読み込みに失敗しました";
            _dialog.ShowError($"ログの読み込みに失敗しました。\n{ex.Message}", "エラー");
        }
        finally
        {
            IsLoading = false;
        }
    }

    partial void OnSelectedUserIdChanged(string? value)
    {
        AvailableDates.Clear();
        SelectedDate = null;

        if (value != null && _allData.TryGetValue(value, out var entries))
        {
            foreach (var date in entries.Select(e => DateOnly.FromDateTime(e.SaveTime)).Distinct().OrderBy(d => d))
                AvailableDates.Add(date);
        }

        if (AvailableDates.Count > 0)
            SelectedDate = AvailableDates[^1];
    }

    partial void OnSelectedDateChanged(DateOnly? value)
    {
        DayEntries.Clear();

        if (value != null && SelectedUserId != null &&
            _allData.TryGetValue(SelectedUserId, out var entries))
        {
            foreach (var entry in entries
                .Where(e => DateOnly.FromDateTime(e.SaveTime) == value)
                .OrderBy(e => e.SaveTime))
            {
                DayEntries.Add(entry);
            }
        }

        CurrentIndex = DayEntries.Count > 0 ? DayEntries.Count - 1 : 0;
        RefreshPlot();
        UpdateCurrentEntry();
    }

    partial void OnCurrentIndexChanged(int value) => UpdateCurrentEntry();

    private void UpdateCurrentEntry()
    {
        CurrentEntry = CurrentIndex >= 0 && CurrentIndex < DayEntries.Count
            ? DayEntries[CurrentIndex]
            : null;

        UpdateHighlight();
        PrevCommand.NotifyCanExecuteChanged();
        NextCommand.NotifyCanExecuteChanged();
    }

    private static string FormatSI(double value)
    {
        if (value == 0) return "0";
        var abs = Math.Abs(value);
        if (abs >= 1e15) return $"{value / 1e15:0.##}P";
        if (abs >= 1e12) return $"{value / 1e12:0.##}T";
        if (abs >= 1e9) return $"{value / 1e9:0.##}G";
        if (abs >= 1e6) return $"{value / 1e6:0.##}M";
        if (abs >= 1e3) return $"{value / 1e3:0.##}k";
        return $"{value:0.##}";
    }

    private void RefreshPlot()
    {
        var model = new PlotModel
        {
            PlotMargins = new OxyThickness(55, 5, 5, 35),
        };

        var xAxis = new DateTimeAxis
        {
            Position = AxisPosition.Bottom,
            StringFormat = "HH:mm:ss",
            FontSize = 11,
        };
        var yAxis = new LinearAxis
        {
            Position = AxisPosition.Left,
            LabelFormatter = FormatSI,
            FontSize = 11,
        };
        model.Axes.Add(xAxis);
        model.Axes.Add(yAxis);

        var lineSeries = new LineSeries
        {
            Color = OxyColors.SteelBlue,
            MarkerType = MarkerType.Circle,
            MarkerSize = 4
        };
        foreach (var e in DayEntries)
            lineSeries.Points.Add(new DataPoint(DateTimeAxis.ToDouble(e.SaveTime), e.CreditAll));
        model.Series.Add(lineSeries);

        _highlightSeries = new ScatterSeries
        {
            MarkerType = MarkerType.Circle,
            MarkerSize = 8,
            MarkerFill = OxyColors.OrangeRed
        };
        model.Series.Add(_highlightSeries);

        if (DayEntries.Count > 0)
        {
            var xMin = DayEntries.Min(e => DateTimeAxis.ToDouble(e.SaveTime));
            var xMax = DayEntries.Max(e => DateTimeAxis.ToDouble(e.SaveTime));
            ConfigureRange(xAxis, xMin, xMax, MinTimeRangeDays, TimePaddingDays);

            var yMin = (double)DayEntries.Min(e => e.CreditAll);
            var yMax = (double)DayEntries.Max(e => e.CreditAll);

            ConfigureRange(yAxis, yMin, yMax, Math.Max(1, Math.Abs(yMax) * 1e-6), (yMax - yMin) * 0.05);

            yAxis.IsZoomEnabled = false;
            yAxis.IsPanEnabled = false;

            lineSeries.MarkerType = xAxis.Maximum - xAxis.Minimum <= MarkerVisibleSpanDays
                ? MarkerType.Circle
                : MarkerType.None;

#pragma warning disable CS0618
            xAxis.AxisChanged += (_, _) => OnTimeRangeChanged(model, lineSeries, xAxis, yAxis);
#pragma warning restore CS0618
        }

        PlotModel = model;
        UpdateHighlight();
    }

    private static void ConfigureRange(Axis axis, double min, double max, double minRange, double padding)
    {
        (min, max) = Widen(min - padding, max + padding, minRange);

        axis.Minimum = axis.AbsoluteMinimum = min;
        axis.Maximum = axis.AbsoluteMaximum = max;
        axis.MinimumRange = minRange;
    }

    private static (double Min, double Max) Widen(double min, double max, double minRange)
    {
        if (max - min >= minRange) return (min, max);

        var center = (min + max) / 2;
        return (center - minRange / 2, center + minRange / 2);
    }

    private void OnTimeRangeChanged(PlotModel model, LineSeries series, Axis xAxis, Axis yAxis)
    {
        var span = xAxis.ActualMaximum - xAxis.ActualMinimum;
        if (double.IsNaN(span)) return;

        var changed = UpdateMarkerVisibility(series, span);
        changed |= FitValueAxis(series, xAxis, yAxis);

        if (changed)
            model.InvalidatePlot(false);
    }

    private static bool UpdateMarkerVisibility(LineSeries series, double span)
    {
        var marker = span <= MarkerVisibleSpanDays ? MarkerType.Circle : MarkerType.None;
        if (series.MarkerType == marker) return false;

        series.MarkerType = marker;
        return true;
    }

    private static bool FitValueAxis(LineSeries series, Axis xAxis, Axis yAxis)
    {
        var min = double.MaxValue;
        var max = double.MinValue;
        foreach (var p in series.Points)
        {
            if (p.X < xAxis.ActualMinimum || p.X > xAxis.ActualMaximum) continue;
            if (p.Y < min) min = p.Y;
            if (p.Y > max) max = p.Y;
        }
        if (min > max) return false;

        var padding = (max - min) * 0.05;
        var (lo, hi) = Widen(min - padding, max + padding, yAxis.MinimumRange);
        if (lo == yAxis.Minimum && hi == yAxis.Maximum) return false;

        yAxis.Minimum = lo;
        yAxis.Maximum = hi;
        return true;
    }

    private void UpdateHighlight()
    {
        _highlightSeries.Points.Clear();
        if (CurrentEntry != null)
        {
            _highlightSeries.Points.Add(new ScatterPoint(
                DateTimeAxis.ToDouble(CurrentEntry.SaveTime),
                CurrentEntry.CreditAll));
        }
        PlotModel.InvalidatePlot(true);
    }

    private bool CanPrev() => CurrentIndex > 0;
    private bool CanNext() => CurrentIndex < DayEntries.Count - 1;

    [RelayCommand(CanExecute = nameof(CanPrev))]
    private void Prev() => CurrentIndex--;

    [RelayCommand(CanExecute = nameof(CanNext))]
    private void Next() => CurrentIndex++;

    private bool HasEntry() => CurrentEntry != null;

    partial void OnCurrentEntryChanged(SaveEntry? value)
    {
        SaveCommand.NotifyCanExecuteChanged();
        CopyUrlCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(HasEntry))]
    private void CopyUrl()
    {
        if (CurrentEntry == null) return;

        try
        {
            Clipboard.SetText(CurrentEntry.Url);
            StatusMessage = $"URLをコピーしました: {CurrentEntry.SaveTime:yyyy/MM/dd HH:mm:ss}";
        }
        catch (ExternalException)
        {
            StatusMessage = "クリップボードへのコピーに失敗しました";
        }
    }

    [RelayCommand(CanExecute = nameof(HasEntry))]
    private async Task SaveAsync()
    {
        var entry = CurrentEntry;
        if (entry == null) return;

        var msg = $"以下のデータでセーブしますか:\n\nユーザー: {SelectedUserId}\n日時: {entry.SaveTime:yyyy/MM/dd HH:mm:ss}\n総獲得: {entry.CreditAll:N0}";
        if (!_dialog.Confirm(msg, "確認"))
            return;

        var result = await _saveService.SaveAsync(entry.Url);
        switch (result)
        {
            case SaveResult.Success:
                StatusMessage = $"セーブしました: {entry.SaveTime:yyyy/MM/dd HH:mm:ss}";
                _dialog.ShowInfo("セーブに成功しました。", "結果");
                break;
            case SaveResult.Duplicate:
                StatusMessage = $"セーブ済みのレコードです: {entry.SaveTime:yyyy/MM/dd HH:mm:ss}";
                _dialog.ShowWarning("このレコードは既にセーブ済みです。", "結果");
                break;
            case SaveResult.NetworkError:
                StatusMessage = "サーバーに接続できませんでした";
                _dialog.ShowError("サーバーに接続できませんでした。ネットワークを確認してください。", "結果");
                break;
            default:
                StatusMessage = "セーブに失敗しました";
                _dialog.ShowError("セーブに失敗しました。", "結果");
                break;
        }
    }

    private bool CanBrowseLogDir() => !IsLoading;

    partial void OnIsLoadingChanged(bool value) => BrowseLogDirCommand.NotifyCanExecuteChanged();

    [RelayCommand(CanExecute = nameof(CanBrowseLogDir))]
    private async Task BrowseLogDirAsync()
    {
        var folder = _dialog.SelectFolder("ログディレクトリを選択", CurrentLogDir);
        if (folder == null) return;

        CurrentLogDir = folder;
        await LoadDataAsync();
    }
}
