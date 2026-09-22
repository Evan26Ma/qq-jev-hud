using System.Runtime.InteropServices;
using System.Text;

namespace QQJevHud.Services;

/// <summary>
/// Stores API keys in the Windows Credential Manager only — never in settings.json, source, logs,
/// or config. The target-name convention is <c>QQJevHud.&lt;Thing&gt;.ApiKey</c> (cmdkey-compatible).
/// </summary>
public static class CredentialStore
{
    public const string TypeSafeTarget = "QQJevHud.TypeSafe.ApiKey";
    public const string OpenAiTarget = "QQJevHud.OpenAI.ApiKey";

    public static void SaveTypeSafeKey(string apiKey) => Save(TypeSafeTarget, apiKey);
    public static string? ReadTypeSafeKey() => Read(TypeSafeTarget);

    public static void SaveOpenAiKey(string apiKey) => Save(OpenAiTarget, apiKey);
    public static string? ReadOpenAiKey() => Read(OpenAiTarget);

    /// <summary>True when a non-empty key is present (without revealing it).</summary>
    public static bool HasKey(string target) => !string.IsNullOrWhiteSpace(Read(target));

    private static void Save(string target, string apiKey)
    {
        var bytes = Encoding.UTF8.GetBytes(apiKey);
        var blob = Marshal.AllocCoTaskMem(bytes.Length);
        try
        {
            Marshal.Copy(bytes, 0, blob, bytes.Length);
            var credential = new NativeCredential
            {
                Type = 1,
                TargetName = target,
                CredentialBlobSize = (uint)bytes.Length,
                CredentialBlob = blob,
                Persist = 2,
                UserName = "QQJevHud"
            };
            if (!CredWrite(ref credential, 0)) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(), "无法保存 API Key。");
        }
        finally { Marshal.FreeCoTaskMem(blob); }
    }

    private static string? Read(string target)
    {
        if (!CredRead(target, 1, 0, out var pointer)) return null;
        try
        {
            var credential = Marshal.PtrToStructure<NativeCredential>(pointer);
            if (credential.CredentialBlob == IntPtr.Zero || credential.CredentialBlobSize == 0) return null;
            var bytes = new byte[credential.CredentialBlobSize];
            Marshal.Copy(credential.CredentialBlob, bytes, 0, bytes.Length);
            // The blob may be UTF-8 (as Save writes it) or UTF-16 LE (e.g. when the
            // entry was created by the Credential Manager UI or cmdkey). Decode accordingly, then
            // drop any stray control characters that would corrupt the Authorization header.
            var text = bytes.Length >= 2 && bytes[1] == 0
                ? Encoding.Unicode.GetString(bytes)
                : Encoding.UTF8.GetString(bytes);
            var key = new string(text.Where(ch => !char.IsControl(ch)).ToArray()).Trim();
            return key.Length == 0 ? null : key;
        }
        finally { CredFree(pointer); }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NativeCredential
    {
        public uint Flags;
        public uint Type;
        public string TargetName;
        public string? Comment;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
        public uint CredentialBlobSize;
        public IntPtr CredentialBlob;
        public uint Persist;
        public uint AttributeCount;
        public IntPtr Attributes;
        public string? TargetAlias;
        public string UserName;
    }

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredWrite([In] ref NativeCredential credential, uint flags);
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredRead(string target, uint type, uint flags, out IntPtr credential);
    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern void CredFree(IntPtr credential);
}
