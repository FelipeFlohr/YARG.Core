using System;
using System.Runtime.InteropServices;

namespace YARG.Core.IO
{
    public enum ImageFormat
    {
        Grayscale = 1,
        GrayScale_Alpha = 2,
        RGB = 3,
        RGBA = 4,
        DXT1,
        DXT5,
    }

    public class YARGImage : IDisposable
    {
        private FixedArray<byte>? _handle;

        public unsafe byte*       Data { get; private set; }

        public        int         Width { get; private set; }

        public        int         Height { get; private set; }

        public        ImageFormat Format { get; private set; }

        public        bool        IsDisposed { get; private set; }

        public unsafe bool        IsValid => !IsDisposed && Data != null;

        public static YARGImage? Load(string path)
        {
            using var bytes = FixedArray.LoadFile(path);
            return Load(bytes);
        }

        /// <summary>
        /// Decodes encoded image files (png/jpg) instead of the native STB2CSharp library, for platforms
        /// where it is not available (the web). Returns null when the data can't be decoded.
        /// </summary>
        public static Func<FixedArray<byte>, YARGImage?>? DecoderOverride { get; set; }

        public static unsafe YARGImage? Load(FixedArray<byte> file)
        {
            var decoder = DecoderOverride;
            if (decoder != null)
            {
                return decoder(file);
            }

            var result = LoadNative(file.Ptr, (int) file.Length, out int width, out int height, out int components);
            if (result == null)
            {
                return null;
            }
            return new YARGImage
            {
                Data = result,
                Width = width,
                Height = height,
                Format = (ImageFormat) components
            };
        }

        /// <summary>
        /// Wraps already decoded pixels, rows top to bottom like STB2CSharp returns them.
        /// The image takes ownership of <paramref name="pixels"/>.
        /// </summary>
        public static unsafe YARGImage FromPixels(FixedArray<byte> pixels, int width, int height, ImageFormat format)
        {
            if (format != ImageFormat.Grayscale && format != ImageFormat.GrayScale_Alpha &&
                format != ImageFormat.RGB && format != ImageFormat.RGBA)
            {
                throw new ArgumentException("Only uncompressed formats can be wrapped", nameof(format));
            }

            long expected = (long) width * height * (int) format;
            if (width <= 0 || height <= 0 || pixels.Length < expected)
            {
                throw new ArgumentException($"Expected {expected} bytes for a {width}x{height} {format} image", nameof(pixels));
            }

            return new YARGImage
            {
                _handle = pixels.TransferOwnership(),
                Data = pixels.Ptr,
                Width = width,
                Height = height,
                Format = format
            };
        }

        public static YARGImage LoadDXT(string path)
        {
            using var data = FixedArray.LoadFile(path);
            return TransferDXT(data);
        }
        public static YARGImage LoadPS3DXT(string path)
        {
            using var data = FixedArray.LoadFile(path);
            // .png_ps3 files are identical to the .png_xbox ones, just with every pair of bytes swapped in the raw image data.
            for (var i = 32; i < data.Length; i += 2) {
                (data[i], data[i + 1]) = (data[i + 1], data[i]);
            }
            return TransferDXT(data);
        }

        public static unsafe YARGImage TransferDXT(FixedArray<byte> file)
        {
            byte bitsPerPixel = file[1];
            int format = *(int*) (file.Ptr + 2);
            bool isDXT1 = bitsPerPixel == 0x04 && format == 0x08;
            return new YARGImage()
            {
                _handle = file.TransferOwnership(),
                Data = file.Ptr + 32,
                Width = *(short*) (file.Ptr + 7),
                Height = *(short*) (file.Ptr + 9),
                Format = isDXT1 ? ImageFormat.DXT1 : ImageFormat.DXT5
            };
        }

        private void _Dispose(bool disposing)
        {
            unsafe
            {
                if (Data == null)
                {
                    return;
                }

                if (_handle == null)
                {
                    FreeNative(Data);
                }
                else if (disposing)
                {
                    _handle.Dispose();
                }
                Data = null;
                IsDisposed = true;
            }
        }

        public void Dispose()
        {
            _Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~YARGImage()
        {
            _Dispose(false);
        }

        [DllImport("STB2CSharp", EntryPoint = "load_image_from_memory")]
        private static extern unsafe byte* LoadNative(byte* data, int length, out int width, out int height, out int components);

        [DllImport("STB2CSharp", EntryPoint = "free_image")]
        private static extern unsafe void FreeNative(byte* image);
    }
}
