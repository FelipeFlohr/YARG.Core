using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NUnit.Framework;
using YARG.Core.Song;

namespace YARG.Core.UnitTests.Song
{
    public class SingleSongLoaderTests
    {
        private const string INI = "[song]\nname = Web Song\nartist = Web Artist\ncharter = Tester\nsong_length = 10000\n";

        private string _root = null!;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), $"yarg-single-{Guid.NewGuid():N}");
            Directory.CreateDirectory(_root);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, true);
            }
        }

        [Test]
        public void Folder_WithIniChartAndAudio_Loads()
        {
            string dir = CreateIniFolder(Path.Combine(_root, "song"));

            var result = SingleSongLoader.Load(dir);

            Assert.That(result.Success, Is.True, $"Expected success, got {result.Error} ({result.Reason})");
            Assert.That(result.Entry!.Name.Original, Is.EqualTo("Web Song"));
            Assert.That(result.Entry.Artist.Original, Is.EqualTo("Web Artist"));
            Assert.That(result.Entry.SubType, Is.EqualTo(EntryType.Ini));
        }

        [Test]
        public void Folder_WrappedInSingleFolders_Loads()
        {
            CreateIniFolder(Path.Combine(_root, "outer", "inner"));
            Directory.CreateDirectory(Path.Combine(_root, "__MACOSX"));

            var result = SingleSongLoader.Load(_root);

            Assert.That(result.Success, Is.True, $"Expected success, got {result.Error} ({result.Reason})");
            Assert.That(result.Entry!.Name.Original, Is.EqualTo("Web Song"));
        }

        [Test]
        public void Folder_WithoutAudio_IsNoAudio()
        {
            string dir = CreateIniFolder(Path.Combine(_root, "song"));
            File.Delete(Path.Combine(dir, "song.opus"));

            Assert.That(SingleSongLoader.Load(dir).Error, Is.EqualTo(SingleSongError.NoAudio));
        }

        [Test]
        public void Folder_WithoutIni_IsNoChart()
        {
            string dir = CreateIniFolder(Path.Combine(_root, "song"));
            File.Delete(Path.Combine(dir, "song.ini"));

            Assert.That(SingleSongLoader.Load(dir).Error, Is.EqualTo(SingleSongError.NoChart));
        }

        [Test]
        public void Folder_WithBrokenChart_IsParseFailedWithReason()
        {
            string dir = Path.Combine(_root, "song");
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "song.ini"), INI);
            File.WriteAllText(Path.Combine(dir, "notes.chart"), "this is not a chart");
            File.WriteAllBytes(Path.Combine(dir, "song.ogg"), new byte[] { 0 });

            var result = SingleSongLoader.Load(dir);

            Assert.That(result.Error, Is.EqualTo(SingleSongError.ParseFailed));
            Assert.That(result.Reason, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public void EmptyFolder_IsUnsupported()
        {
            Assert.That(SingleSongLoader.Load(_root).Error, Is.EqualTo(SingleSongError.UnsupportedFormat));
        }

        [Test]
        public void FolderWithOtherFiles_IsNoChart()
        {
            File.WriteAllText(Path.Combine(_root, "readme.txt"), "hi");
            File.WriteAllText(Path.Combine(_root, "other.txt"), "hi");

            Assert.That(SingleSongLoader.Load(_root).Error, Is.EqualTo(SingleSongError.NoChart));
        }

        [TestCase("song.con")]
        [TestCase("song.zip")]
        [TestCase("song.txt")]
        public void UnknownFile_IsUnsupported(string name)
        {
            string path = Path.Combine(_root, name);
            File.WriteAllBytes(path, new byte[] { 1, 2, 3, 4 });

            Assert.That(SingleSongLoader.Load(path).Error, Is.EqualTo(SingleSongError.UnsupportedFormat));
        }

        [Test]
        public void MissingPath_IsUnsupported()
        {
            Assert.That(SingleSongLoader.Load(Path.Combine(_root, "nope")).Error, Is.EqualTo(SingleSongError.UnsupportedFormat));
        }

        [Test]
        public void CorruptSng_IsUnsupported()
        {
            string path = Path.Combine(_root, "song.sng");
            File.WriteAllText(path, "not an sng file at all");

            Assert.That(SingleSongLoader.Load(path).Error, Is.EqualTo(SingleSongError.UnsupportedFormat));
        }

        [Test]
        public void Sng_WithMetadataChartAndAudio_Loads()
        {
            string path = Path.Combine(_root, "song.sng");
            WriteSng(path, SongMetadata(), SongFiles(true));

            var result = SingleSongLoader.Load(path);

            Assert.That(result.Success, Is.True, $"Expected success, got {result.Error} ({result.Reason})");
            Assert.That(result.Entry!.Name.Original, Is.EqualTo("Sng Song"));
            Assert.That(result.Entry.SubType, Is.EqualTo(EntryType.Sng));
        }

        [Test]
        public void Sng_WithoutAudio_IsNoAudio()
        {
            string path = Path.Combine(_root, "song.sng");
            WriteSng(path, SongMetadata(), SongFiles(false));

            Assert.That(SingleSongLoader.Load(path).Error, Is.EqualTo(SingleSongError.NoAudio));
        }

        [Test]
        public void FolderWithSingleSng_LoadsTheSng()
        {
            WriteSng(Path.Combine(_root, "Song.SNG"), SongMetadata(), SongFiles(true));

            var result = SingleSongLoader.Load(_root);

            Assert.That(result.Success, Is.True, $"Expected success, got {result.Error} ({result.Reason})");
            Assert.That(result.Entry!.SubType, Is.EqualTo(EntryType.Sng));
        }

        private static string CreateIniFolder(string dir)
        {
            Directory.CreateDirectory(dir);
            File.Copy(GetTestMidiPath(), Path.Combine(dir, "notes.mid"));
            File.WriteAllText(Path.Combine(dir, "song.ini"), INI);
            File.WriteAllBytes(Path.Combine(dir, "song.opus"), new byte[] { 0 });
            return dir;
        }

        private static List<KeyValuePair<string, string>> SongMetadata()
        {
            return new List<KeyValuePair<string, string>>
            {
                new("name", "Sng Song"),
                new("artist", "Sng Artist"),
                new("song_length", "10000"),
            };
        }

        private static List<KeyValuePair<string, byte[]>> SongFiles(bool withAudio)
        {
            var files = new List<KeyValuePair<string, byte[]>>
            {
                new("notes.mid", File.ReadAllBytes(GetTestMidiPath())),
            };
            if (withAudio)
            {
                files.Add(new("song.opus", new byte[] { 0, 1, 2, 3 }));
            }
            return files;
        }

        /// <summary>
        /// Writes an SNGPKG file (https://github.com/mdsitton/SngFileFormat). The mask byte at index i is
        /// keys[i % 16] ^ (i & 0xFF); an all-zero key is used when none is given.
        /// </summary>
        internal static void WriteSng(string path, List<KeyValuePair<string, string>> metadata,
            List<KeyValuePair<string, byte[]>> files, byte[]? keys = null)
        {
            keys ??= new byte[16];
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);

            writer.Write(Encoding.ASCII.GetBytes("SNGPKG"));
            writer.Write(1u);
            writer.Write(keys);

            long metadataLength = sizeof(ulong);
            foreach (var pair in metadata)
            {
                metadataLength += 8 + Encoding.UTF8.GetByteCount(pair.Key) + Encoding.UTF8.GetByteCount(pair.Value);
            }
            writer.Write(metadataLength);
            writer.Write((ulong) metadata.Count);
            foreach (var pair in metadata)
            {
                byte[] key = Encoding.UTF8.GetBytes(pair.Key);
                byte[] value = Encoding.UTF8.GetBytes(pair.Value);
                writer.Write(key.Length);
                writer.Write(key);
                writer.Write(value.Length);
                writer.Write(value);
            }

            long indexLength = sizeof(ulong);
            foreach (var file in files)
            {
                indexLength += 1 + Encoding.UTF8.GetByteCount(file.Key) + 16;
            }

            long position = stream.Position + sizeof(long) + indexLength + sizeof(ulong);
            writer.Write(indexLength);
            writer.Write((ulong) files.Count);
            long totalData = 0;
            foreach (var file in files)
            {
                byte[] name = Encoding.UTF8.GetBytes(file.Key);
                writer.Write((byte) name.Length);
                writer.Write(name);
                writer.Write((long) file.Value.Length);
                writer.Write(position);
                position += file.Value.Length;
                totalData += file.Value.Length;
            }

            writer.Write((ulong) totalData);
            foreach (var file in files)
            {
                var masked = new byte[file.Value.Length];
                for (int i = 0; i < masked.Length; ++i)
                {
                    masked[i] = (byte) (file.Value[i] ^ keys[i % 16] ^ (i & 0xFF));
                }
                writer.Write(masked);
            }

            writer.Flush();
            File.WriteAllBytes(path, stream.ToArray());
        }

        private static string GetTestMidiPath()
        {
            string path = Path.GetFullPath(Path.Combine(
                TestContext.CurrentContext.TestDirectory,
                "../../../../Parsing/Test Charts/test.mid"));
            Assert.That(File.Exists(path), Is.True, $"Expected test MIDI fixture at {path}.");
            return path;
        }
    }
}
