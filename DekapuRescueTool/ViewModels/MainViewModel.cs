using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DekapuRescueTool.Models;
using DekapuRescueTool.Services;
using Microsoft.Win32;
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
        IsLoading = true;
        try
        {
            var dir = CurrentLogDir;
            var data = await Task.Run(() => _logReader.ReadAllLogs(dir));
            _allData = data;

            UserIds.Clear();
            SelectedUserId = null;

            foreach (var uid in _allData.Keys.OrderBy(k => k))
                UserIds.Add(uid);

            SelectedUserId = UserIds.FirstOrDefault();
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

        PlotModel = model;
        UpdateHighlight();
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

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (CurrentEntry == null) return;

        var msg = $"以下のデータでセーブしますか:\n\nユーザー: {SelectedUserId}\n日時: {CurrentEntry.SaveTime:yyyy/MM/dd HH:mm:ss}\n総獲得: {CurrentEntry.CreditAll}";
        if (!_dialog.Confirm(msg, "確認"))
            return;

        var result = await _saveService.SaveAsync(CurrentEntry.Url);
        switch (result)
        {
            case SaveResult.Success:
                _dialog.ShowInfo("セーブに成功しました。", "結果");
                break;
            case SaveResult.Duplicate:
                _dialog.ShowWarning("このレコードは既にセーブ済みです。", "結果");
                break;
            default:
                _dialog.ShowError("セーブに失敗しました。", "結果");
                break;
        }
    }

    [RelayCommand]
    private async Task BrowseLogDirAsync()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "ログディレクトリを選択",
            InitialDirectory = CurrentLogDir
        };
        if (dialog.ShowDialog() != true) return;

        CurrentLogDir = dialog.FolderName;
        await LoadDataAsync();
    }
}
