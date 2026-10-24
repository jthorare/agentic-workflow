using System.Globalization;

namespace Aw.Sindri.Ui.ViewModels;

/// <summary>The app's own copy (<see cref="Labels"/>, continued): the setup checklist's repo form, the coding agents, the map and the Onyx form (setup spec §6). Plain words; a code never shows in a sentence.</summary>
internal static partial class Labels
{
    /// <summary>The coding agents the step connects, in the order the list shows them.</summary>
    internal static readonly IReadOnlyList<string> SetupAgentNames = ["Claude Code", "Codex", "Cursor"];

    /// <summary>The repo form's field label.</summary>
    internal const string SetupFolderLabel = "Folder";

    /// <summary>The folder chooser's button.</summary>
    internal const string SetupBrowse = "Browse";

    /// <summary>The folder chooser's title.</summary>
    internal const string SetupBrowseTitle = "Choose the top folder of a git checkout";

    /// <summary>The line over the repo form.</summary>
    internal const string SetupRepoBody = "Choose the top folder of a git checkout. Adding it changes your settings, so you approve them next.";

    /// <summary>The note under the repo form.</summary>
    internal const string SetupRepoNote = "Linked worktrees resolve to their main checkout. If the folder is already added, or is inside Sindri's own folder, Sindri says so here and changes nothing.";

    /// <summary>The repo row while the add is on its way.</summary>
    internal const string SetupRepoAdding = "Adding the repository.";

    /// <summary>Under the folder field when nothing was entered.</summary>
    internal const string SetupRepoEmpty = "Choose or type a folder first.";

    /// <summary>Under the folder field when the add could not be started for a reason Sindri has no sentence for.</summary>
    internal const string SetupRepoFailed = "Couldn't add that folder just now. Try again in a moment.";

    /// <summary>SND-SETUP-001 in words.</summary>
    internal const string SetupErr001 = "That folder isn't a git checkout, or can't be read.";

    /// <summary>SND-SETUP-002 in words.</summary>
    internal const string SetupErr002 = "That repository is already added, or another repository has its name.";

    /// <summary>SND-SETUP-004 in words.</summary>
    internal const string SetupErr004 = "Add the repository and approve your settings first.";

    /// <summary>The agents list: connected.</summary>
    internal const string SetupAgentConnected = "Connected";

    /// <summary>The agents list: being connected now.</summary>
    internal const string SetupAgentConnecting = "Connecting";

    /// <summary>The agents list: reached but could not be connected.</summary>
    internal const string SetupAgentFailed = "Failed";

    /// <summary>The agent's name for the provider id the API serves ('claude', 'codex', 'cursor'); empty for one this build doesn't know.</summary>
    /// <param name="provider">The provider id.</param>
    /// <returns>The name.</returns>
    internal static string SetupAgentForProvider(string provider) => provider switch
    {
        "claude" => "Claude Code",
        "codex" => "Codex",
        "cursor" => "Cursor",
        _ => string.Empty,
    };

    /// <summary>The agents list: not reached yet.</summary>
    internal const string SetupAgentNotStarted = "Not started";

    /// <summary>The fixed line under the agents list.</summary>
    internal const string SetupTrustOnce = "Codex and Cursor will each ask you to trust the folder once.";

    /// <summary>The second fixed line under the agents list.</summary>
    internal const string SetupAgentsNote = "Only this repository, never your whole account. Skipped automatically if a coding agent isn't installed.";

    /// <summary>The agents row after Connect started a registration.</summary>
    internal const string SetupMcpAgain = "Connecting your coding agents.";

    /// <summary>The agents row when Connect could not start.</summary>
    internal const string SetupMcpFailed = "Couldn't connect your coding agents just now. Try again in a moment.";

    /// <summary>The map's line under its error: Sindri keeps trying.</summary>
    internal const string SetupMapRetries = "Sindri also retries on its own with longer waits.";

    /// <summary>"Stopped at 40 of 100".</summary>
    internal static string SetupMapStopped(int done, int total) => string.Create(CultureInfo.InvariantCulture, $"Stopped at {done} of {total}.");

    /// <summary>The line over the Onyx form.</summary>
    internal const string SetupOnyxIntro = "Create your Onyx account in the browser first if you haven't, then sign in here.";

    /// <summary>The email field's label.</summary>
    internal const string SetupOnyxEmail = "Onyx email";

    /// <summary>The password field's label.</summary>
    internal const string SetupOnyxPassword = "Onyx password";

    /// <summary>The control that shows the password.</summary>
    internal const string SetupOnyxShow = "Show password";

    /// <summary>The control that hides the password.</summary>
    internal const string SetupOnyxHide = "Hide password";

    /// <summary>The note under the Onyx form.</summary>
    internal const string SetupOnyxNote = "Used once to connect Linear and Slack. Never stored, never shown in any log.";

    /// <summary>The footnote under the checklist.</summary>
    internal const string SetupOnyxFoot = "Prefer a terminal? Run";

    /// <summary>The command the footnote names.</summary>
    internal const string SetupOnyxFootCommand = "sindri-app onyx up";

    /// <summary>The Onyx button that sends the login (and opens the form again).</summary>
    internal const string SetupActConnect = "Connect";

    /// <summary>The Onyx button that sets it aside.</summary>
    internal const string SetupOnyxNotNow = "Not now";

    /// <summary>The Onyx row after Set up started the stack.</summary>
    internal const string SetupOnyxStarting = "Starting Onyx.";

    /// <summary>The Onyx row after the login was sent.</summary>
    internal const string SetupOnyxConnecting = "Connecting to Onyx.";

    /// <summary>The Onyx row after Not now.</summary>
    internal const string SetupOnyxLater = "Onyx is set aside for later.";

    /// <summary>The Onyx row when a button could not start its job.</summary>
    internal const string SetupOnyxFailed = "Couldn't reach Onyx just now. Try again in a moment.";

    /// <summary>Under both fields when neither was entered, and SND-SETUP-005 in words.</summary>
    internal const string SetupErr005 = "Enter your Onyx email and password.";

    /// <summary>Under the email field when it is empty.</summary>
    internal const string SetupOnyxEmailNeeded = "Enter your Onyx email.";

    /// <summary>Under the password field when it is empty.</summary>
    internal const string SetupOnyxPasswordNeeded = "Enter your Onyx password.";

    /// <summary>Under the password field when Onyx refused the login (SND-ONYX-007).</summary>
    internal const string SetupOnyxWrong = "Onyx didn't accept that email and password. Check them and try again.";

    /// <summary>Under the form when no Onyx setup is waiting for a login (SND-SETUP-006).</summary>
    internal const string SetupErr006 = "Sindri isn't waiting for a login. Set up Onyx first.";

    /// <summary>Under the form when Docker is missing (SND-ONYX-001).</summary>
    internal const string SetupDockerMissing = "Docker isn't installed.";

    /// <summary>A refusal of one of Sindri's own steps (SND-AUTH-002) in words.</summary>
    internal const string SetupAuthRefused = "Sindri's own steps can't answer this.";

    /// <summary>Under the form when the profile changed while Sindri was writing it (SND-PROFILE-002).</summary>
    internal const string SetupProfileChanged = "The profile changed while Sindri was writing it. Try again.";

    /// <summary>A setup refusal's code as a sentence the person can act on; null for a code with no words here.</summary>
    /// <param name="code">The error code.</param>
    /// <returns>The sentence, or null.</returns>
    internal static string? SetupRefusal(string code) => code switch
    {
        "SND-SETUP-001" => SetupErr001,
        "SND-SETUP-002" => SetupErr002,
        "SND-SETUP-004" => SetupErr004,
        "SND-SETUP-005" => SetupErr005,
        "SND-SETUP-006" => SetupErr006,
        "SND-PROFILE-002" => SetupProfileChanged,
        "SND-ONYX-001" => SetupDockerMissing,
        "SND-ONYX-007" => SetupOnyxWrong,
        "SND-AUTH-002" => SetupAuthRefused,
        _ => null,
    };

    /// <summary>The accessible name of an agent's line.</summary>
    internal static string SetupAgentName(string agent, string word) => agent + ", " + word.ToLowerInvariant();
}
