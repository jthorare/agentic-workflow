using System.Globalization;

namespace Aw.Sindri.Ui.ViewModels;

/// <summary>The Not running page's start states (first-run spec §4.1).</summary>
internal static partial class Labels
{
    /// <summary>The sentence under "Sindri isn't running", before any start.</summary>
    internal const string StartSentence = "Start it and Home loads your reviews, runs and messages. This takes about 10 seconds.";

    /// <summary>The Start Sindri button.</summary>
    internal const string StartSindri = "Start Sindri";

    /// <summary>The state while the app waits for Sindri.</summary>
    internal const string StartingSindri = "Starting Sindri…";

    /// <summary>The heading and the pill's word while a start runs (#3672).</summary>
    internal const string StartingWord = "Starting";

    /// <summary>The details line while a start runs: "Waiting for Sindri · 4 s" (#3663).</summary>
    /// <param name="seconds">Whole seconds since the start.</param>
    internal static string WaitingFor(int seconds) => string.Create(CultureInfo.InvariantCulture, $"Waiting for Sindri · {seconds} s");

    /// <summary>The details line from 15 s on: "Still starting · 17 s" (#3663).</summary>
    /// <param name="seconds">Whole seconds since the start.</param>
    internal static string StillStarting(int seconds) => string.Create(CultureInfo.InvariantCulture, $"Still starting · {seconds} s");

    /// <summary>The Retry button after a start didn't answer.</summary>
    internal const string Retry = "Retry";

    /// <summary>A second serve was refused.</summary>
    internal const string AlreadyStarting = "Sindri is already starting on this machine.";

    /// <summary>A broken install: no 'sindri' beside the app.</summary>
    internal const string NotInstalled = "The Sindri program isn't installed next to this app.";

    /// <summary>The broken install's details.</summary>
    internal const string InstallDetails = "scripts/install-cs.sh --link";

    /// <summary>When serve.log holds no error line.</summary>
    internal const string NoErrorLine = "no error line in the log";

    /// <summary>Sindri didn't answer: the log's last error line follows.</summary>
    internal static string DidntStart(string error) => "Sindri didn't start: " + error + ".";

    /// <summary>The started program ended: the log's last error line and its exit code.</summary>
    internal static string DidntStartExit(string error, int code) => string.Create(CultureInfo.InvariantCulture, $"Sindri didn't start: {error} (exit code {code}).");

    /// <summary>The details of a start that didn't answer: the log and the manual command.</summary>
    internal static string StartDetails(string log) => log + " · sindri serve";
}
