using System.Windows;
using KaylesStepsRecorder.App.ViewModels;

namespace KaylesStepsRecorder.App.Views;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        Loaded += async (_, _) => await viewModel.CheckForCrashRecoveryAsync();
    }
}
