using System.Windows;
using System.Windows.Controls;
using PokemonRedRL.ControlPanel.ViewModels;
using PokemonRedRL.ControlPanel.Views;

namespace PokemonRedRL.ControlPanel;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void OnAttachWindowClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel) return;
        if ((sender as Button)?.Tag is not AgentTileViewModel tile) return;

        var candidates = viewModel.GetCandidateWindows();
        var dialog = new WindowPickerDialog(candidates) { Owner = this };
        if (dialog.ShowDialog() == true && dialog.SelectedWindow is { } window)
        {
            viewModel.AttachWindow(tile, window);
        }
    }
}
