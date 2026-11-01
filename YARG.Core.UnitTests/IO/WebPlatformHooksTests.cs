using System;
using System.IO;
using NUnit.Framework;
using YARG.Core.IO;

namespace YARG.Core.UnitTests.IO
{
    public class WebPlatformHooksTests
    {
        [TestCase(1, true)]
        [TestCase(1, false)]
        [TestCase(31, true)]
        [TestCase(31, false)]
        [TestCase(256, true)]
        [TestCase(256, false)]
        [TestCase(4096 + 77, true)]
        [TestCase(4096 + 77, false)]
        public void SngDecrypt_ParallelAndSequentialRecoverTheFile(int length, bool parallel)
        {
            var random = new Random(length);
            var keys = new byte[16];
            random.NextBytes(keys);
            var original = new byte[length];
            random.NextBytes(original);

            string path = Path.Combine(Path.GetTempPath(), $"yarg-decrypt-{Guid.NewGuid():N}.sng");
            bool previous = SngFileStream.UseParallelDecrypt;
            try
            {
                Song.SingleSongLoaderTests.WriteSng(path, new(), new() { new("data.bin", original) }, keys);
                SngFileStream.UseParallelDecrypt = parallel;

                using var sng = SngFile.TryLoadFromFile(path, true);
                Assert.That(sng.TryGetListing("data.bin", out var listing), Is.True);
                using var data = sng.LoadAllBytes(in listing);

                Assert.That(data.ReadOnlySpan.ToArray(), Is.EqualTo(original));
            }
            finally
            {
                SngFileStream.UseParallelDecrypt = previous;
                File.Delete(path);
            }
        }

        [Test]
        public void FromPixels_WrapsThePixels()
        {
            var pixels = FixedArray<byte>.Alloc(2 * 3 * 4);

            using var image = YARGImage.FromPixels(pixels, 2, 3, ImageFormat.RGBA);

            Assert.That(image.IsValid, Is.True);
            Assert.That(image.Width, Is.EqualTo(2));
            Assert.That(image.Height, Is.EqualTo(3));
            Assert.That(image.Format, Is.EqualTo(ImageFormat.RGBA));
        }

        [Test]
        public void FromPixels_RejectsShortBuffers()
        {
            using var pixels = FixedArray<byte>.Alloc(10);
            Assert.Throws<ArgumentException>(() => YARGImage.FromPixels(pixels, 2, 3, ImageFormat.RGBA));
        }

        [Test]
        public void DecoderOverride_IsUsedByLoad()
        {
            FixedArray<byte>? seen = null;
            YARGImage.DecoderOverride = file =>
            {
                seen = file;
                return YARGImage.FromPixels(FixedArray<byte>.Alloc(3), 1, 1, ImageFormat.RGB);
            };
            try
            {
                using var file = FixedArray<byte>.Alloc(4);
                using var image = YARGImage.Load(file);

                Assert.That(seen, Is.Not.Null);
                Assert.That(image, Is.Not.Null);
                Assert.That(image!.Width, Is.EqualTo(1));
            }
            finally
            {
                YARGImage.DecoderOverride = null;
            }
        }
    }
}
