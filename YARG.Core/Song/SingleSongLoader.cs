using System;
using System.IO;
using System.Linq;
using YARG.Core.IO;
using YARG.Core.Logging;
using YARG.Core.Song.Cache;

namespace YARG.Core.Song
{
    public enum SingleSongError
    {
        None,
        /// <summary>Not a folder or an .sng file (CON, unknown extension, corrupt .sng).</summary>
        UnsupportedFormat,
        /// <summary>No chart file, or a .mid/.chart without the song.ini that the scan requires.</summary>
        NoChart,
        NoAudio,
        /// <summary>The chart was found but failed to scan. <see cref="SingleSongResult.Reason"/> says why.</summary>
        ParseFailed,
    }

    public readonly struct SingleSongResult
    {
        public readonly SongEntry? Entry;
        public readonly SingleSongError Error;

        /// <summary>For <see cref="SingleSongError.ParseFailed"/>: the scan result or exception message.</summary>
        public readonly string? Reason;

        public bool Success => Entry != null;

        private SingleSongResult(SongEntry? entry, SingleSongError error, string? reason)
        {
            Entry = entry;
            Error = error;
            Reason = reason;
        }

        public static SingleSongResult Ok(SongEntry entry) => new(entry, SingleSongError.None, null);

        public static SingleSongResult Fail(SingleSongError error, string? reason = null) => new(null, error, reason);
    }

    /// <summary>
    /// Builds one <see cref="SongEntry"/> from a path without a library scan or cache, following the same
    /// rules as <see cref="CacheHandler"/> for unpacked ini folders and .sng files. Used by the web build,
    /// where the player picks a single song.
    /// </summary>
    public static class SingleSongLoader
    {
        /// <summary>How deep to descend through folders that only hold one subfolder (zips with a root folder).</summary>
        private const int MAX_WRAPPER_DEPTH = 4;

        private static readonly string[] IGNORED_ENTRIES = { "__macosx", ".ds_store", "thumbs.db", "desktop.ini" };

        public static SingleSongResult Load(string path, string defaultPlaylist = "")
        {
            try
            {
                if (File.Exists(path))
                {
                    return LoadFile(new FileInfo(path), defaultPlaylist);
                }

                if (Directory.Exists(path))
                {
                    return LoadDirectory(new DirectoryInfo(path), defaultPlaylist, 0);
                }
            }
            catch (Exception e)
            {
                YargLogger.LogException(e, $"Error while loading song at {path}!");
                return SingleSongResult.Fail(SingleSongError.ParseFailed, e.Message);
            }

            return SingleSongResult.Fail(SingleSongError.UnsupportedFormat);
        }

        private static SingleSongResult LoadFile(FileInfo info, string defaultPlaylist)
        {
            string ext = info.Extension.ToLowerInvariant();
            if (ext != ".sng" && ext != ".yargsong")
            {
                return SingleSongResult.Fail(SingleSongError.UnsupportedFormat);
            }

            using var sngFile = SngFile.TryLoadFromFile(info.FullName, true);
            if (!sngFile.IsLoaded)
            {
                return SingleSongResult.Fail(SingleSongError.UnsupportedFormat);
            }

            int i = !sngFile.Modifiers.IsEmpty() ? 0 : 2;
            for (; i < 3; ++i)
            {
                if (!sngFile.TryGetListing(IniSubEntry.CHART_FILE_TYPES[i].Filename, out var chart))
                {
                    continue;
                }

                if (!sngFile.ContainsAudio())
                {
                    return SingleSongResult.Fail(SingleSongError.NoAudio);
                }

                var entry = SngEntry.ProcessNewEntry(in sngFile, in chart, info, IniSubEntry.CHART_FILE_TYPES[i].Format, defaultPlaylist);
                return entry.HasValue
                    ? SingleSongResult.Ok(entry.Value)
                    : SingleSongResult.Fail(SingleSongError.ParseFailed, entry.Error.ToString());
            }

            return SingleSongResult.Fail(SingleSongError.NoChart);
        }

        private static SingleSongResult LoadDirectory(DirectoryInfo directory, string defaultPlaylist, int depth)
        {
            var collection = new FileCollection(directory);

            bool hasIni = collection.FindFile("song.ini", out var ini);
            for (int i = hasIni ? 0 : 3; i < 4; ++i)
            {
                if (!collection.FindFile(IniSubEntry.CHART_FILE_TYPES[i].Filename, out var chart))
                {
                    continue;
                }

                if (!collection.ContainsAudio())
                {
                    return SingleSongResult.Fail(SingleSongError.NoAudio);
                }

                var entry = UnpackedIniEntry.ProcessNewEntry(collection.Directory, chart, IniSubEntry.CHART_FILE_TYPES[i].Format, hasIni ? ini : null, defaultPlaylist);
                return entry.HasValue
                    ? SingleSongResult.Ok(entry.Value)
                    : SingleSongResult.Fail(SingleSongError.ParseFailed, entry.Error.ToString());
            }

            // A zip or a dropped folder may wrap the song in one more folder, or hold a single .sng
            var children = collection
                .Where(pair => !IGNORED_ENTRIES.Contains(pair.Key))
                .Select(pair => pair.Value)
                .ToList();

            if (depth < MAX_WRAPPER_DEPTH && children.Count == 1)
            {
                switch (children[0])
                {
                    case DirectoryInfo child:
                        return LoadDirectory(child, defaultPlaylist, depth + 1);
                    case FileInfo file when IsSngFile(file):
                        return LoadFile(file, defaultPlaylist);
                }
            }

            return SingleSongResult.Fail(children.Count > 0 ? SingleSongError.NoChart : SingleSongError.UnsupportedFormat);
        }

        private static bool IsSngFile(FileInfo file)
        {
            string ext = file.Extension.ToLowerInvariant();
            return ext == ".sng" || ext == ".yargsong";
        }
    }
}
