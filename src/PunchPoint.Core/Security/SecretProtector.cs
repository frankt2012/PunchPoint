using System.Security.Cryptography;
using System.Text;

namespace PunchPoint.Core.Security;

/// <summary>
/// Optional protection for stored secrets (the SMTP password). On Windows it uses DPAPI
/// tied to the Windows account, so another user on the PC cannot read it. Elsewhere it
/// falls back to base64 so the app still works in development.
///
/// Values are self-describing: "dpapi:..." or "b64:..."; anything else is treated as
/// plain text, which keeps older databases readable.
/// </summary>
public static class SecretProtector
{
    private const string DpapiPrefix = "dpapi:";
    private const string Base64Prefix = "b64:";

    public static bool IsWindowsDpapiAvailable => OperatingSystem.IsWindows();

    public static string Protect(string? value, bool encrypt)
    {
        value ??= "";
        if (!encrypt || value.Length == 0) return value;

        if (OperatingSystem.IsWindows())
        {
            try
            {
                byte[] data = Encoding.UTF8.GetBytes(value);
                byte[] protectedData = ProtectedData.Protect(data, null, DataProtectionScope.CurrentUser);
                return DpapiPrefix + Convert.ToBase64String(protectedData);
            }
            catch
            {
                // fall through to the portable form
            }
        }

        return Base64Prefix + Convert.ToBase64String(Encoding.UTF8.GetBytes(value));
    }

    public static string Unprotect(string? stored)
    {
        if (string.IsNullOrEmpty(stored)) return "";

        if (stored.StartsWith(DpapiPrefix, StringComparison.Ordinal))
        {
            try
            {
                byte[] data = Convert.FromBase64String(stored[DpapiPrefix.Length..]);
                byte[] clear = ProtectedData.Unprotect(data, null, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(clear);
            }
            catch
            {
                // Wrong Windows account or a different machine: treat as unset rather than crash.
                return "";
            }
        }

        if (stored.StartsWith(Base64Prefix, StringComparison.Ordinal))
        {
            try { return Encoding.UTF8.GetString(Convert.FromBase64String(stored[Base64Prefix.Length..])); }
            catch { return ""; }
        }

        return stored;
    }

    public static bool IsProtected(string? stored) =>
        !string.IsNullOrEmpty(stored) &&
        (stored.StartsWith(DpapiPrefix, StringComparison.Ordinal) ||
         stored.StartsWith(Base64Prefix, StringComparison.Ordinal));
}
