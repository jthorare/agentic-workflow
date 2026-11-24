namespace Aw.Sindri.Core.Connections.OAuth;

/// <summary>
/// One device-flow sign-in in progress (RFC 8628, first-run spec §4.2 "Device code shown"): the code the owner types at the provider, where, and until when. A class, not a record, so nothing that prints it
/// shows <see cref="DeviceCode"/>, which is a secret for the length of the sign-in (decision 17): it lives in memory only and is dropped after the answer.
/// </summary>
/// <param name="deviceCode">The provider's device_code, sent back on each poll.</param>
/// <param name="userCode">The short code the owner types, such as ABCD-1234.</param>
/// <param name="verificationUri">The page the owner types it on (https).</param>
/// <param name="expiresAt">When the provider stops taking the code.</param>
/// <param name="interval">How long to wait between polls, as the provider asked.</param>
public sealed class DeviceAuthorization(string deviceCode, string userCode, Uri verificationUri, DateTimeOffset expiresAt, TimeSpan interval)
{
    /// <summary>Gets the provider's device_code. A secret for the sign-in's length: never logged, shown or returned.</summary>
    public string DeviceCode { get; } = deviceCode;

    /// <summary>Gets the short code the owner types.</summary>
    public string UserCode { get; } = userCode;

    /// <summary>Gets the page the owner types it on.</summary>
    public Uri VerificationUri { get; } = verificationUri;

    /// <summary>Gets when the provider stops taking the code.</summary>
    public DateTimeOffset ExpiresAt { get; } = expiresAt;

    /// <summary>Gets the wait between polls the provider asked for.</summary>
    public TimeSpan Interval { get; } = interval;

    /// <inheritdoc />
    public override string ToString() => $"DeviceAuthorization({UserCode}, device code redacted)";
}
