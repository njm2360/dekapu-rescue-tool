using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using DekapuRescueTool.ViewModels;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Wpf;

namespace DekapuRescueTool.Views;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        Title = $"{AppInfo.DisplayName} v{AppInfo.Version}";
        DataContext = viewModel;

        var plotView = new PlotView();
        plotView.SetBinding(PlotViewBase.ModelProperty,
            new Binding(nameof(viewModel.PlotModel)));

        var controller = new PlotController();
        controller.UnbindMouseDown(OxyMouseButton.Left);
        controller.UnbindMouseDown(OxyMouseButton.Middle);
        controller.UnbindMouseDown(OxyMouseButton.Right);
        controller.BindMouseDown(OxyMouseButton.Left, OxyPlot.PlotCommands.ZoomRectangle);
        controller.BindMouseDown(OxyMouseButton.Middle, OxyPlot.PlotCommands.PanAt);
        controller.UnbindKeyDown(OxyKey.Left);
        controller.UnbindKeyDown(OxyKey.Right);
        plotView.Controller = controller;

        Point? leftDownPos = null;

        plotView.AddHandler(MouseDownEvent, new MouseButtonEventHandler((_, e) =>
        {
            if (e.ChangedButton == MouseButton.Left)
                leftDownPos = e.GetPosition(plotView);
        }), handledEventsToo: true);

        plotView.AddHandler(MouseUpEvent, new MouseButtonEventHandler((_, e) =>
        {
            if (e.ChangedButton != MouseButton.Left || leftDownPos == null) return;

            var upPos = e.GetPosition(plotView);
            var startPos = leftDownPos.Value;
            leftDownPos = null;

            if ((upPos - startPos).Length > 5) return;

            var model = viewModel.PlotModel;
            if (model == null || viewModel.DayEntries.Count == 0) return;

            var sp = new ScreenPoint(upPos.X, upPos.Y);
            if (!model.PlotArea.Contains(sp)) return;

            var xAxis = model.Axes.OfType<DateTimeAxis>().FirstOrDefault();
            if (xAxis == null) return;

            var clickTime = xAxis.ConvertToDateTime(xAxis.InverseTransform(sp.X));
            var nearest = viewModel.DayEntries
                .Select((entry, idx) => (idx, diff: Math.Abs((entry.SaveTime - clickTime).TotalSeconds)))
                .MinBy(x => x.diff);
            viewModel.CurrentIndex = nearest.idx;
        }), handledEventsToo: true);

        PlotContainer.Content = plotView;
    }
}
