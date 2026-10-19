using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace QuickAttach;

/// <summary>
/// 32x32 shell file type icon as IPictureDisp, cached per extension. The shell returns a 32bpp premultiplied
/// alpha DIB section, which is wrapped as is so the ribbon keeps the transparency.
/// </summary>
internal static class FileIconProvider
{
    private const int IconSize = 32;
    private const int IconOnly = 0x4;
    private const int PictypeBitmap = 1;
    private static readonly Guid IPictureDispIid = new("7BF80981-BF32-101A-8BBB-00AA00300CAB");
    private static readonly Dictionary<string, object> Cache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Called on the UI thread only.</summary>
    public static object Get(string path)
    {
        var extension = Path.GetExtension(path);
        if (!Cache.TryGetValue(extension, out var picture))
        {
            picture = Load(path);
            Cache[extension] = picture;
        }

        return picture;
    }

    private static object Load(string path)
    {
        var iid = typeof(IShellItemImageFactory).GUID;
        SHCreateItemFromParsingName(path, IntPtr.Zero, ref iid, out var factory);
        try
        {
            factory.GetImage(new NativeSize { Width = IconSize, Height = IconSize }, IconOnly, out var bitmap);
            var desc = new PictDesc { Size = Marshal.SizeOf<PictDesc>(), Type = PictypeBitmap, Bitmap = bitmap };
            var pictureIid = IPictureDispIid;
            try
            {
                // The picture owns the bitmap from here on.
                OleCreatePictureIndirect(ref desc, ref pictureIid, true, out var picture);
                return picture;
            }
            catch
            {
                DeleteObject(bitmap);
                throw;
            }
        }
        finally
        {
            Marshal.ReleaseComObject(factory);
        }
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
    private static extern void SHCreateItemFromParsingName(string path, IntPtr bindContext, ref Guid riid, out IShellItemImageFactory factory);

    [DllImport("oleaut32.dll", PreserveSig = false)]
    private static extern void OleCreatePictureIndirect(ref PictDesc desc, ref Guid riid, bool own, [MarshalAs(UnmanagedType.IDispatch)] out object picture);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr handle);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeSize
    {
        public int Width;
        public int Height;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PictDesc
    {
        public int Size;
        public int Type;
        public IntPtr Bitmap;
        public IntPtr Palette;
    }

    [ComImport]
    [Guid("BCC18B79-BA16-442F-80C4-8A59C30C463B")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItemImageFactory
    {
        void GetImage(NativeSize size, int flags, out IntPtr bitmap);
    }
}
