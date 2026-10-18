namespace Aw.Sindri.Core.Sandbox;

/// <summary>The wire names of the sandbox enums, for text that is built by hand (error extensions, the approve preview). The same names the JSON uses.</summary>
internal static class SandboxWire
{
    internal static string Name(ModelLoginSource value) => value == ModelLoginSource.OwnerCopy ? "owner-copy" : "separate-login";

    internal static string Name(NetworkMode value) => value == NetworkMode.Enforce ? "enforce" : "learn";

    internal static string Name(InnerSandbox value) => value == InnerSandbox.Auto ? "auto" : value == InnerSandbox.On ? "on" : "off";

    internal static string Name(GithubCredentials value) => value == GithubCredentials.None ? "none" : "agent-app";

    internal static string Name(MountMode value) => value == MountMode.ReadOnly ? "ro" : "rw";
}
