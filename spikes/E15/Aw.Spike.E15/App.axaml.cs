using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace Aw.Spike.E15;

internal sealed class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var window = new MainWindow();
            desktop.MainWindow = window;
            if (Environment.GetEnvironmentVariable("AW_SPIKE_E15") == "1")
            {
                window.Opened += (_, _) => _ = Measure.RunAndExitAsync(window, desktop);
            }
        }
        base.OnFrameworkInitializationCompleted();
    }
}
