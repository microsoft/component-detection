namespace Microsoft.ComponentDetection.Detectors.Rust;

#pragma warning disable IDE0007 // Use implicit type
#pragma warning disable SA1402 // File may only contain a single type

using System;
using System.Runtime.InteropServices;
using System.Text;

public sealed class LockfileLoader : IDisposable
{
    private readonly LockfileHandle lockfile;
    private string lockfileString;

    public LockfileLoader(string lockfilePath) => this.lockfile = Native.Json(lockfilePath);

    public override string ToString()
    {
        this.lockfileString ??= this.lockfile.AsString();
        return this.lockfileString;
    }

    public void Dispose()
    {
        this.lockfile.Dispose();
    }
}

/// <summary>
/// Provides interop methods for working with the Rust-produced native cargo_c_lock library.
/// </summary>
internal partial class Native
{
    [LibraryImport("cargo_c_lock", EntryPoint = "json", StringMarshalling = StringMarshalling.Utf8)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static partial LockfileHandle Json(string lockfilePath);

    /// <summary>
    /// Frees the memory associated with the specified JSON handle.
    /// </summary>
    /// <param name="jsonHandle">The handle to the JSON memory to free.</param>
    [LibraryImport("cargo_c_lock", EntryPoint = "free")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static partial void Free(IntPtr jsonHandle);
}

internal class LockfileHandle : SafeHandle
{
    public LockfileHandle()
        : base(IntPtr.Zero, true)
    {
    }

    public override bool IsInvalid
    {
        get { return this.handle == IntPtr.Zero; }
    }

    public string AsString()
    {
        int len = 0;
        while (Marshal.ReadByte(this.handle, len) != 0)
        {
            ++len;
        }

        byte[] buffer = new byte[len];
        Marshal.Copy(this.handle, buffer, 0, buffer.Length);
        return Encoding.UTF8.GetString(buffer);
    }

    protected override bool ReleaseHandle()
    {
        if (!this.IsInvalid)
        {
            Native.Free(this.handle);
        }

        return true;
    }
}

#pragma warning restore SA1402 // File may only contain a single type
#pragma warning restore IDE0007 // Use implicit type
