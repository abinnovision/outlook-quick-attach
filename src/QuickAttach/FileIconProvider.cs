using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using QuickAttach.Core;

namespace QuickAttach;

/// <summary>
/// Shell file type icon as IPictureDisp, looked up by extension only so the file and its share are never touched.
/// Cached per extension, failures included. The icon is copied into a 32bpp premultiplied alpha DIB section,
/// which the ribbon draws with its transparency.
/// </summary>
internal static class FileIconProvider
{
    private const int FileAttributeNormal = 0x80;
    private const int ShgfiIcon = 0x100;
    private const int ShgfiLargeIcon = 0x0;
    private const int ShgfiUseFileAttributes = 0x10;
    private const int PictypeBitmap = 1;
    private static readonly Guid IPictureDispIid = new("7BF80981-BF32-101A-8BBB-00AA00300CAB");
    private static readonly Dictionary<string, object?> Cache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Called on the UI thread only. Returns null when the shell has no icon for the extension.</summary>
    public static object? Get(string path)
    {
        var extension = Path.GetExtension(path);
        if (!Cache.TryGetValue(extension, out var picture))
        {
            try
            {
                picture = Load(extension);
            }
            catch (Exception e)
            {
                Log.Error("FileIcon", "No icon for \"" + extension + "\"", e);
            }

            Cache[extension] = picture;
        }

        return picture;
    }

    private static object Load(string extension)
    {
        var info = new ShFileInfo();
        // With SHGFI_USEFILEATTRIBUTES the name does not have to exist.
        if (SHGetFileInfo("file" + extension, FileAttributeNormal, ref info, Marshal.SizeOf<ShFileInfo>(), ShgfiIcon | ShgfiLargeIcon | ShgfiUseFileAttributes) == IntPtr.Zero
            || info.Icon == IntPtr.Zero)
        {
            throw new InvalidOperationException("SHGetFileInfo returned no icon.");
        }

        try
        {
            // FromHandle does not take ownership; the icon is destroyed below.
            using var icon = Icon.FromHandle(info.Icon);
            using var bitmap = icon.ToBitmap();
            var dib = CreatePremultipliedDib(bitmap);
            var desc = new PictDesc { Size = Marshal.SizeOf<PictDesc>(), Type = PictypeBitmap, Bitmap = dib };
            var pictureIid = IPictureDispIid;
            try
            {
                // The picture owns the bitmap from here on.
                OleCreatePictureIndirect(ref desc, ref pictureIid, true, out var picture);
                return picture;
            }
            catch
            {
                DeleteObject(dib);
                throw;
            }
        }
        finally
        {
            DestroyIcon(info.Icon);
        }
    }

    /// <summary>Copies the bitmap into a top-down 32bpp DIB section with premultiplied alpha.</summary>
    private static IntPtr CreatePremultipliedDib(Bitmap bitmap)
    {
        var header = new BitmapInfoHeader
        {
            Size = Marshal.SizeOf<BitmapInfoHeader>(),
            Width = bitmap.Width,
            Height = -bitmap.Height,
            Planes = 1,
            BitCount = 32,
        };
        var dib = CreateDIBSection(IntPtr.Zero, ref header, 0, out var bits, IntPtr.Zero, 0);
        if (dib == IntPtr.Zero)
        {
            throw new InvalidOperationException("CreateDIBSection failed.");
        }

        var rect = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
        var data = bitmap.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppPArgb);
        try
        {
            var row = new byte[bitmap.Width * 4];
            for (var y = 0; y < bitmap.Height; y++)
            {
                Marshal.Copy(data.Scan0 + y * data.Stride, row, 0, row.Length);
                Marshal.Copy(row, 0, bits + y * row.Length, row.Length);
            }
        }
        finally
        {
            bitmap.UnlockBits(data);
        }

        return dib;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SHGetFileInfo(string path, int fileAttributes, ref ShFileInfo info, int infoSize, int flags);

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr icon);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateDIBSection(IntPtr dc, ref BitmapInfoHeader header, int usage, out IntPtr bits, IntPtr section, int offset);

    [DllImport("oleaut32.dll", PreserveSig = false)]
    private static extern void OleCreatePictureIndirect(ref PictDesc desc, ref Guid riid, bool own, [MarshalAs(UnmanagedType.IDispatch)] out object picture);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr handle);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ShFileInfo
    {
        public IntPtr Icon;
        public int IconIndex;
        public int Attributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string DisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
        public string TypeName;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BitmapInfoHeader
    {
        public int Size;
        public int Width;
        public int Height;
        public short Planes;
        public short BitCount;
        public int Compression;
        public int SizeImage;
        public int XPelsPerMeter;
        public int YPelsPerMeter;
        public int ClrUsed;
        public int ClrImportant;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PictDesc
    {
        public int Size;
        public int Type;
        public IntPtr Bitmap;
        public IntPtr Palette;
    }
}
