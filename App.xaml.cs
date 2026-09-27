namespace ValheimServerLauncher;

public partial class App : System.Windows.Application
{
    protected override void OnStartup(System.Windows.StartupEventArgs e)
    {
        Localization.Initialize(ValheimConfig.Load().Language);
        base.OnStartup(e);
    }
}
