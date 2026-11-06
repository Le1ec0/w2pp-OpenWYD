namespace WydCdk.Protocol;

/// <summary>
/// Admission contract for the single client produced by this project.
/// Parsing accepts any well-formed legacy frame; the listener uses this policy
/// to decide whether that frame belongs to the supported 7.670 release.
/// </summary>
public static class ClientReleasePolicy
{
    public static int RequiredClientVersion => WydCdk.Release.ClientReleaseVersion.Required;

    public static bool IsSupported(int clientVersion) => clientVersion == RequiredClientVersion;
}
