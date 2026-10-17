namespace Aw.Sindri.Core.Onyx;

/// <summary>The values Sindri sets in Onyx's .env and compose override.</summary>
/// <param name="ImageTag">The Onyx release tag, IMAGE_TAG.</param>
/// <param name="Port">The loopback port nginx is published on, HOST_PORT.</param>
/// <param name="AuthSecret">USER_AUTH_SECRET: never logged or printed.</param>
public sealed record EnvSettings(string ImageTag, int Port, string AuthSecret);
