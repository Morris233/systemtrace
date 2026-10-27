using System.Windows;
using SystemTrace.App.ViewModels;

namespace SystemTrace.App;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    protected override void OnClosed(EventArgs e)
    {
        ((MainViewModel)DataContext).CancelPending();
        base.OnClosed(e);
    }
}
