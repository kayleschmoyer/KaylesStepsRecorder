using System.Windows;
using System.Windows.Controls;
using KaylesStepsRecorder.App.ViewModels;
using KaylesStepsRecorder.Core.Enums;
using Microsoft.Win32;

namespace KaylesStepsRecorder.App.Views;

public partial class ExportView : UserControl
{
    public ExportView() => InitializeComponent();

    private void FormatCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is not ExportViewModel vm) return;
        vm.Format = FormatCombo.SelectedIndex switch
        {
            0 => ExportFormat.HtmlFull,
            1 => ExportFormat.HtmlCompact,
            2 => ExportFormat.Markdown,
            _ => ExportFormat.HtmlFull,
        };
    }

    private void BrowseClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not ExportViewModel vm) return;

        string filter = vm.Format switch
        {
            ExportFormat.Markdown => "Markdown (*.md)|*.md|All files (*.*)|*.*",
            _ => "HTML (*.html)|*.html|All files (*.*)|*.*",
        };

        string defaultExt = vm.Format == ExportFormat.Markdown ? ".md" : ".html";

        var dlg = new SaveFileDialog
        {
            Filter = filter,
            DefaultExt = defaultExt,
            FileName = System.IO.Path.GetFileName(vm.OutputPath ?? $"session{defaultExt}"),
            InitialDirectory = string.IsNullOrEmpty(vm.OutputPath) ? null : System.IO.Path.GetDirectoryName(vm.OutputPath)
        };

        if (dlg.ShowDialog() == true)
            vm.OutputPath = dlg.FileName;
    }
}
