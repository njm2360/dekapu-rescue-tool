using System.Windows;
using Microsoft.Win32;

namespace DekapuRescueTool.Services;

public class DialogService : IDialogService
{
    public bool Confirm(string message, string title) =>
        Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;

    public void ShowInfo(string message, string title) =>
        Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);

    public void ShowWarning(string message, string title) =>
        Show(message, title, MessageBoxButton.OK, MessageBoxImage.Warning);

    public void ShowError(string message, string title) =>
        Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);

    private static MessageBoxResult Show(string message, string title, MessageBoxButton button, MessageBoxImage image)
    {
        var owner = Application.Current?.MainWindow;
        return owner != null
            ? MessageBox.Show(owner, message, title, button, image)
            : MessageBox.Show(message, title, button, image);
    }

    public string? SelectFolder(string title, string initialDirectory)
    {
        var dialog = new OpenFolderDialog
        {
            Title = title,
            InitialDirectory = initialDirectory
        };
        return dialog.ShowDialog() == true ? dialog.FolderName : null;
    }
}
