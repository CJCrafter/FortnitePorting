﻿using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;
using System.Windows.Media.Imaging;
using SixLabors.ImageSharp.Formats.Png;
using SkiaSharp;

namespace FortnitePorting.Views.Extensions;

public static class ImageExtensions
{
    public static BitmapSource ToBitmapSource(this SKBitmap bitmap)
    {
        var source = new BitmapImage { CacheOption = BitmapCacheOption.OnDemand };
        source.BeginInit();
        source.StreamSource = bitmap.Encode(SKEncodedImageFormat.Png, 100).AsStream();
        source.EndInit();
        return source;
    }
    
    public static BitmapSource ToBitmapSource(this SixLabors.ImageSharp.Image bitmap)
    {
        var source = new BitmapImage { CacheOption = BitmapCacheOption.OnDemand };
        source.BeginInit();
        source.StreamSource = new MemoryStream();
        SixLabors.ImageSharp.ImageExtensions.Save(bitmap, source.StreamSource, PngFormat.Instance);
        source.EndInit();
        return source;
    }
    

    public static void SetImage(byte[] pngBytes, string fileName = null)
    {
        Clipboard.Clear();
        var data = new DataObject();
        using var pngMs = new MemoryStream(pngBytes);
        using var image = Image.FromStream(pngMs);
        // As standard bitmap, without transparency support
        data.SetData(DataFormats.Bitmap, image, true);
        // As PNG. Gimp will prefer this over the other two
        data.SetData("PNG", pngMs, false);
        // As DIB. This is (wrongly) accepted as ARGB by many applications
        using var dibMemStream = new MemoryStream(ConvertToDib(image));
        data.SetData(DataFormats.Dib, dibMemStream, false);
        // Optional fileName
        if (!string.IsNullOrEmpty(fileName))
        {
            var htmlFragment = GenerateHTMLFragment($"<img src=\"{fileName}\"/>");
            data.SetData(DataFormats.Html, htmlFragment);
        }
        // The 'copy=true' argument means the MemoryStreams can be safely disposed after the operation
        Clipboard.SetDataObject(data, true);
    }

    public static byte[] ConvertToDib(Image image)
    {
        byte[] bm32bData;
        var width = image.Width;
        var height = image.Height;

        // Ensure image is 32bppARGB by painting it on a new 32bppARGB image.
        using (var bm32b = new Bitmap(width, height, PixelFormat.Format32bppPArgb))
        {
            using (var gr = Graphics.FromImage(bm32b))
            {
                gr.DrawImage(image, new Rectangle(0, 0, width, height));
            }

            // Bitmap format has its lines reversed.
            bm32b.RotateFlip(RotateFlipType.Rotate180FlipX);
            bm32bData = GetRawBytes(bm32b);
        }

        // BITMAPINFOHEADER struct for DIB.
        const int hdrSize = 0x28;
        var fullImage = new byte[hdrSize + 12 + bm32bData.Length];
        //Int32 biSize;
        WriteIntToByteArray(fullImage, 0x00, 4, true, hdrSize);
        //Int32 biWidth;
        WriteIntToByteArray(fullImage, 0x04, 4, true, (uint) width);
        //Int32 biHeight;
        WriteIntToByteArray(fullImage, 0x08, 4, true, (uint) height);
        //Int16 biPlanes;
        WriteIntToByteArray(fullImage, 0x0C, 2, true, 1);
        //Int16 biBitCount;
        WriteIntToByteArray(fullImage, 0x0E, 2, true, 32);
        //BITMAPCOMPRESSION biCompression = BITMAPCOMPRESSION.BITFIELDS;
        WriteIntToByteArray(fullImage, 0x10, 4, true, 3);
        //Int32 biSizeImage;
        WriteIntToByteArray(fullImage, 0x14, 4, true, (uint) bm32bData.Length);
        // These are all 0. Since .net clears new arrays, don't bother writing them.
        //Int32 biXPelsPerMeter = 0;
        //Int32 biYPelsPerMeter = 0;
        //Int32 biClrUsed = 0;
        //Int32 biClrImportant = 0;

        // The aforementioned "BITFIELDS": colour masks applied to the Int32 pixel value to get the R, G and B values.
        WriteIntToByteArray(fullImage, hdrSize + 0, 4, true, 0x00FF0000);
        WriteIntToByteArray(fullImage, hdrSize + 4, 4, true, 0x0000FF00);
        WriteIntToByteArray(fullImage, hdrSize + 8, 4, true, 0x000000FF);

        Unsafe.CopyBlockUnaligned(ref fullImage[hdrSize + 12], ref bm32bData[0], (uint) bm32bData.Length);
        return fullImage;
    }

    private static void WriteIntToByteArray(byte[] data, int startIndex, int bytes, bool littleEndian, uint value)
    {
        var lastByte = bytes - 1;

        if (data.Length < startIndex + bytes)
        {
            throw new ArgumentOutOfRangeException(nameof(startIndex), "Data array is too small to write a " + bytes + "-byte value at offset " + startIndex + ".");
        }

        for (var index = 0; index < bytes; index++)
        {
            var offs = startIndex + (littleEndian ? index : lastByte - index);
            data[offs] = (byte) (value >> 8 * index & 0xFF);
        }
    }

    private static unsafe byte[] GetRawBytes(Bitmap bmp)
    {
        var rect = new Rectangle(0, 0, bmp.Width, bmp.Height);
        var bmpData = bmp.LockBits(rect, ImageLockMode.ReadOnly, bmp.PixelFormat);
        var bytes = (uint) (Math.Abs(bmpData.Stride) * bmp.Height);
        var buffer = new byte[bytes];
        fixed (byte* pBuffer = buffer)
        {
            Unsafe.CopyBlockUnaligned(pBuffer, bmpData.Scan0.ToPointer(), bytes);
        }

        bmp.UnlockBits(bmpData);
        return buffer;
    }

    private static string GenerateHTMLFragment(string html)
    {
        var sb = new StringBuilder();

        const string header = "Version:0.9\r\nStartHTML:<<<<<<<<<1\r\nEndHTML:<<<<<<<<<2\r\nStartFragment:<<<<<<<<<3\r\nEndFragment:<<<<<<<<<4\r\n";
        const string startHTML = "<html>\r\n<body>\r\n";
        const string startFragment = "<!--StartFragment-->";
        const string endFragment = "<!--EndFragment-->";
        const string endHTML = "\r\n</body>\r\n</html>";

        sb.Append(header);

        var startHTMLLength = header.Length;
        var startFragmentLength = startHTMLLength + startHTML.Length + startFragment.Length;
        var endFragmentLength = startFragmentLength + Encoding.UTF8.GetByteCount(html);
        var endHTMLLength = endFragmentLength + endFragment.Length + endHTML.Length;

        sb.Replace("<<<<<<<<<1", startHTMLLength.ToString("D10"));
        sb.Replace("<<<<<<<<<2", endHTMLLength.ToString("D10"));
        sb.Replace("<<<<<<<<<3", startFragmentLength.ToString("D10"));
        sb.Replace("<<<<<<<<<4", endFragmentLength.ToString("D10"));

        sb.Append(startHTML);
        sb.Append(startFragment);
        sb.Append(html);
        sb.Append(endFragment);
        sb.Append(endHTML);

        return sb.ToString();
    }
}