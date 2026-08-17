using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ZipAhoy.Internal;

namespace ZipAhoy
{
    /// <summary>
    /// Creates and extracts zip archives, with progress reporting and cancellation support.
    /// </summary>
    public static class Archive
    {
        private const int BufferSize = 81920; // just below the LOH threshold

        /// <summary>
        /// Zips the files in a folder, including sub-folders.
        /// </summary>
        /// <param name="folderPath">The directory to zip</param>
        /// <param name="archiveFilePath">The path of the resulting zip archive</param>
        /// <param name="progress">Optional progress action</param>
        /// <param name="token">Optional cancellation token</param>
        /// <returns>Nothing</returns>
        public static Task CreateFromFolderAsync(string folderPath, string archiveFilePath,
                                                 Action<float>? progress = null, CancellationToken token = default)
        {
            Require.IsNotBlank(folderPath, nameof(folderPath));
            Require.IsNotBlank(archiveFilePath, nameof(archiveFilePath));
            Require.FolderExists(folderPath, nameof(folderPath));

            return Task.Run(() => CreateFromDirectoryHelper(folderPath, archiveFilePath, progress, token), token);
        }

        /// <summary>
        /// Unzips a zip archive to a specified folder, creating the folder if it does not already exist.
        /// </summary>
        /// <param name="archiveFilePath">The zip archive to unzip</param>
        /// <param name="destFolderPath">The folder to unpack the zip file in</param>
        /// <param name="progress">Optional progress action</param>
        /// <param name="token">Optional cancellation token</param>
        /// <returns>Nothing</returns>
        /// <exception cref="InvalidDataException">
        /// Thrown if the archive contains an entry that resolves to a path outside
        /// <paramref name="destFolderPath"/>.
        /// </exception>
        public static Task ExtractToFolderAsync(string archiveFilePath, string destFolderPath,
                                                Action<float>? progress = null, CancellationToken token = default)
        {
            Require.IsNotBlank(archiveFilePath, nameof(archiveFilePath));
            Require.IsNotBlank(destFolderPath, nameof(destFolderPath));
            Require.FileExists(archiveFilePath, nameof(archiveFilePath));

            return Task.Run(() => ExtractToDirectoryHelper(archiveFilePath, destFolderPath, progress, token), token);
        }

        private static void CreateFromDirectoryHelper(string sourcePath, string archiveFilePath, Action<float>? progress,
                                                      CancellationToken token)
        {
            sourcePath = Path.GetFullPath(sourcePath);
            archiveFilePath = Path.GetFullPath(archiveFilePath);

            var folderSeparators = new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar };
            var directoryInfo = new DirectoryInfo(sourcePath);

            // this code results in two run-throughs of the source folder, but
            // because of file system caching, the second run-through will be much faster than the first
            var totalBytes = directoryInfo.EnumAllFiles().Sum(fi => fi.Length);
            token.ThrowIfCancellationRequested();

            var buffer = new byte[BufferSize];
            var currentBytes = 0L;

            try
            {
                using (var zipArchive = ZipFile.Open(archiveFilePath, ZipArchiveMode.Create, Encoding.UTF8))
                {
                    foreach (var info in directoryInfo.EnumAllEntries())
                    {
                        token.ThrowIfCancellationRequested();

                        int length = info.FullName.Length - sourcePath.Length;
                        var entryName = info.FullName.Substring(sourcePath.Length, length)
                                                     .TrimStart(folderSeparators)
                                                     // the zip format mandates '/' as the separator
                                                     // (APPNOTE 4.4.17.1), so archives written on Windows
                                                     // stay readable everywhere else
                                                     .Replace(Path.DirectorySeparatorChar, '/');
                        if (info is FileInfo sourceInfo)
                        {
                            using var source = sourceInfo.OpenRead();
                            var entry = zipArchive.CreateEntry(entryName);
                            entry.LastWriteTime = sourceInfo.GetLastWriteTime();
                            using var destination = entry.Open();
                            currentBytes += StreamCopyHelper(source, destination, buffer, progress, totalBytes,
                                                             currentBytes, token);
                        }
                        else if (info is DirectoryInfo dirInfo && dirInfo.IsEmpty())
                        {
                            // create entry for empty folder
                            zipArchive.CreateEntry(entryName + "/");
                        }
                    }
                }
            }
            catch
            {
                // the archive is only half-written at this point, and a truncated zip file is
                // indistinguishable from a finished one to a caller that just checks for existence
                TryDelete(archiveFilePath);
                throw;
            }
        }

        private static void ExtractToDirectoryHelper(string archiveFilePath, string destFolderPath,
                                                     Action<float>? progress, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            var destRootPath = Directory.CreateDirectory(destFolderPath).FullName;

            using var zipArchive = ZipFile.Open(archiveFilePath, ZipArchiveMode.Read, Encoding.UTF8);
            var currentBytes = 0L;

            // initial run through of the directory, getting the total number of bytes to extract
            var totalBytes = zipArchive.Entries.Sum(entry => entry.Length);
            var buffer = new byte[BufferSize];

            // second run through, actually extracting entries
            foreach (var entry in zipArchive.Entries)
            {
                token.ThrowIfCancellationRequested();
                var destPath = ResolveEntryPath(destRootPath, entry.FullName);

                // file or directory?
                if (IsDirectoryEntry(entry.FullName))
                {
                    // it's an empty directory, so simply create it
                    Directory.CreateDirectory(destPath);
                    Directory.SetLastWriteTimeUtc(destPath, ToUtc(entry.LastWriteTime));
                }
                else
                {
                    // it's a file, so create its containing folder(s) ...
                    var parentPath = Path.GetDirectoryName(destPath);
                    if (!string.IsNullOrEmpty(parentPath))
                    {
                        Directory.CreateDirectory(parentPath!);
                    }
                    // ... and extract it
                    using (var dest = File.Open(destPath, FileMode.Create, FileAccess.Write, FileShare.None))
                    using (var source = entry.Open())
                    {
                        currentBytes += StreamCopyHelper(source, dest, buffer, progress, totalBytes, currentBytes, token);
                    }
                    File.SetLastWriteTimeUtc(destPath, ToUtc(entry.LastWriteTime));
                }
            }
        }

        /// <summary>
        /// A zip entry timestamp is a bare clock reading with no time zone (APPNOTE 4.4.6), and ZipAhoy
        /// writes the UTC reading there (see <see cref="FileSystemHelpers.GetLastWriteTime"/>). The
        /// framework getter re-attaches the *local* offset to that reading, so the offset has to be
        /// dropped rather than converted away - otherwise every round-trip shifts by it.
        /// </summary>
        private static DateTime ToUtc(DateTimeOffset entryLastWriteTime) =>
            DateTime.SpecifyKind(entryLastWriteTime.DateTime, DateTimeKind.Utc);

        /// <summary>
        /// Maps a zip entry name onto a path below <paramref name="destRootPath"/>, rejecting entries that
        /// try to escape it (a.k.a. "zip slip").
        /// </summary>
        private static string ResolveEntryPath(string destRootPath, string entryName)
        {
            var relativePath = entryName.Replace('/', Path.DirectorySeparatorChar)
                                        .TrimEnd(Path.DirectorySeparatorChar);
            var destPath = Path.GetFullPath(Path.Combine(destRootPath, relativePath));

            var root = destRootPath.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!destPath.StartsWith(root, StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    $"Zip entry '{entryName}' resolves to a path outside of '{destRootPath}'");
            }

            return destPath;
        }

        /// <summary>
        /// Directory entries are marked by a trailing separator. Archives written by ZipAhoy 0.3.0 and
        /// earlier on Windows used a backslash here, so accept that as well where it is a separator.
        /// </summary>
        private static bool IsDirectoryEntry(string entryName)
        {
            if (entryName.Length == 0)
            {
                return false;
            }
            var last = entryName[entryName.Length - 1];
            return last == '/' || last == Path.DirectorySeparatorChar;
        }

        private static void TryDelete(string filePath)
        {
            try
            {
                File.Delete(filePath);
            }
            catch (IOException)
            {
                // nothing sensible to do here - we're already on our way out with the original exception
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        private static long StreamCopyHelper(Stream source, Stream destination, byte[] buffer, Action<float>? progress,
                                             long totalBytes, long currentBytes, CancellationToken token)
        {
            var totalCopied = 0L;
            var bytesRead = source.Read(buffer, 0, buffer.Length);
            while (bytesRead > 0)
            {
                token.ThrowIfCancellationRequested();
                destination.Write(buffer, 0, bytesRead);

                totalCopied += bytesRead;
                progress?.Invoke((currentBytes + totalCopied) / (float)totalBytes);

                token.ThrowIfCancellationRequested();
                bytesRead = source.Read(buffer, 0, buffer.Length);
            }

            return totalCopied;
        }

    }
}
