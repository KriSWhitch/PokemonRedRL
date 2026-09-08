using System.Windows;
using PokemonRedRL.ControlPanel.Services;

namespace PokemonRedRL.ControlPanel.Views;

/// <summary>
/// Explicit, user-driven window-to-tile attachment dialog. See WindowEnumerationService remarks:
/// there is no reliable automatic port-to-window correlation, so the user must pick the correct
/// mGBA window themselves to avoid showing the wrong preview under a tile.
/// </summary>
public partial class WindowPickerDialog : Window
{
    public CandidateWindow? SelectedWindow { get; private set; }

    public WindowPickerDialog(List<CandidateWindow> candidates)
    {
        InitializeComponent();
        WindowList.ItemsSource = candidates;
    }

    private void OnAttachClick(object sender, RoutedEventArgs e)
    {
        SelectedWindow = WindowList.SelectedItem as CandidateWindow;
        DialogResult = SelectedWindow is not null;
    }
}
