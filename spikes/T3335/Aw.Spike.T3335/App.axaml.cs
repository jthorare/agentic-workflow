using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace Aw.Spike.T3335;

internal sealed class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var measuring = Environment.GetEnvironmentVariable("AW_SPIKE_T3335") == "1";
            var window = new MainWindow(autoStart: !measuring);
            desktop.MainWindow = window;
            if (measuring)
            {
                window.Opened += (_, _) => _ = Measure.RunAndExitAsync(window, desktop);
            }
        }
        base.OnFrameworkInitializationCompleted();
    }
}
