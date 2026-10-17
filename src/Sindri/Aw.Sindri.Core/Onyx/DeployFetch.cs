namespace Aw.Sindri.Core.Onyx;

/// <summary>What a fetch of the deployment files did.</summary>
/// <param name="Downloaded">Files downloaded, verified and written.</param>
/// <param name="Present">Files already there with the pinned hash, left alone and not requested.</param>
public sealed record DeployFetch(int Downloaded, int Present);
