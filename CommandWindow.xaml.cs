using System.Windows;

namespace ValheimServerLauncher;

public partial class CommandWindow : Window
{
    public string CommandText => CommandBox.Text.Trim();
    public CommandWindow()
    {
        InitializeComponent();
        SourceInitialized += (_, _) => DarkTitleBar.Apply(this);
    }
    private void SendClick(object sender, RoutedEventArgs e) { if (!string.IsNullOrWhiteSpace(CommandText)) DialogResult = true; }
}
