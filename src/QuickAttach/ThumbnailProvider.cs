using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace QuickAttach;

/// <summary>Shell thumbnail as IPictureDisp; the shell returns the file type icon when no thumbnail exists.</summary>
internal static class ThumbnailProvider
{
    public static object? Get(string path, int size)
    {
        var iid = typeof(IShellItemImageFactory).GUID;
        SHCreateItemFromParsingName(path, IntPtr.Zero, ref iid, out var factory);
        try
        {
            factory.GetImage(new NativeSize { Width = size, Height = size }, 0, out var bitmap);
            try
            {
                using var image = Image.FromHbitmap(bitmap);
                return PictureConverter.ToPictureDisp(image);
            }
            finally
            {
                DeleteObject(bitmap);
            }
        }
        finally
        {
            Marshal.ReleaseComObject(factory);
        }
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
    private static extern void SHCreateItemFromParsingName(string path, IntPtr bindContext, ref Guid riid, out IShellItemImageFactory factory);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr handle);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeSize
    {
        public int Width;
        public int Height;
    }

    [ComImport]
    [Guid("BCC18B79-BA16-442F-80C4-8A59C30C463B")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItemImageFactory
    {
        void GetImage(NativeSize size, int flags, out IntPtr bitmap);
    }

    private sealed class PictureConverter : AxHost
    {
        private PictureConverter()
            : base("")
        {
        }

        public static object ToPictureDisp(Image image) => GetIPictureDispFromPicture(image);
    }
}
