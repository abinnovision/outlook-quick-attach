using System;
using System.Runtime.InteropServices;
using System.Text;

namespace ScanAttach.Core.Update;

/// <summary>Reads properties of an MSI file through msi.dll (Windows only).</summary>
public static class MsiPackage
{
    private const int IgnoreMachineState = 1;

    /// <summary>Returns the ProductVersion of the package, or null when it cannot be read.</summary>
    public static string? ReadProductVersion(string msiPath)
    {
        if (MsiOpenPackageEx(msiPath, IgnoreMachineState, out var handle) != 0)
        {
            return null;
        }

        try
        {
            var length = 64;
            var value = new StringBuilder(length);
            return MsiGetProductProperty(handle, "ProductVersion", value, ref length) == 0 ? value.ToString() : null;
        }
        finally
        {
            MsiCloseHandle(handle);
        }
    }

    [DllImport("msi.dll", CharSet = CharSet.Unicode)]
    private static extern uint MsiOpenPackageEx(string packagePath, int options, out IntPtr product);

    [DllImport("msi.dll", CharSet = CharSet.Unicode)]
    private static extern uint MsiGetProductProperty(IntPtr product, string property, StringBuilder value, ref int length);

    [DllImport("msi.dll")]
    private static extern uint MsiCloseHandle(IntPtr handle);
}
