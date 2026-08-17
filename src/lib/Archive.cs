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
        /// <param name="progress">Optional progress action, called on a thread pool thread</param>
        /// <param name="token">Optional cancellation token</param>
        /// <returns>Nothing</returns>
        public static Task CreateFromFolderAsync(string folderPath, string archiveFilePath,
                                                 Action<float>? progress = null, CancellationToken token = default)
        {
            // validated up front rather than inside the async method below, so that a caller passing
            // nonsense gets told about it right away instead of on the first await
            Require.IsNotBlank(folderPath, nameof(folderPath));
            Require.IsNotBlank(archiveFilePath, nameof(archiveFilePath));
            Require.FolderExists(folderPath, nameof(folderPath));

            return CreateFromDirectoryAsync(folderPath, archiveFilePath, progress, token);
        }

        /// <summary>
        /// Unzips a zip archive to a specified folder, creating the folder if it does not already exist.
        /// </summary>
        /// <param name="archiveFilePath">The zip archive to unzip</param>
        /// <param name="destFolderPath">The folder to unpack the zip file in</param>
        /// <param name="progress">Optional progress action, called on a thread pool thread</param>
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

            return ExtractToDirectoryAsync(archiveFilePath, destFolderPath, progress, token);
        }

        private static async Task CreateFromDirectoryAsync(string sourcePath, string archiveFilePath,
                                                           Action<float>? progress, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            sourcePath = Path.GetFullPath(sourcePath);
            archiveFilePath = Path.GetFullPath(archiveFilePath);

            var directoryInfo = new DirectoryInfo(sourcePath);

            // the folder is walked twice - once to size up the work for progress reporting, and once to
            // do it. The BCL has no asynchronous directory enumeration, so this walk is synchronous, but
            // it only reads metadata; file contents are never touched here.
            var totalBytes = directoryInfo.EnumAllFiles().Sum(fi => fi.Length);
            token.ThrowIfCancellationRequested();

            // opened before the cleanup handler below on purpose: if this throws because the archive
            // already exists, that file is not ours to delete
            var fileStream = OpenArchiveForWriting(archiveFilePath);
            var failed = false;
            try
            {
                var zipArchive = await OpenArchiveAsync(fileStream, ZipArchiveMode.Create, token).ConfigureAwait(false);
                try
                {
                    await WriteEntriesAsync(zipArchive, directoryInfo, sourcePath, totalBytes, progress, token)
                        .ConfigureAwait(false);
                }
                finally
                {
                    // disposing the archive writes the central directory
                    await DisposeAsync(zipArchive).ConfigureAwait(false);
                }
            }
            catch
            {
                failed = true;
                throw;
            }
            finally
            {
                await DisposeAsync(fileStream).ConfigureAwait(false);
                if (failed)
                {
                    // a truncated zip file is indistinguishable from a finished one to a caller that
                    // just checks whether the archive is there
                    TryDelete(archiveFilePath);
                }
            }
        }

        private static async Task WriteEntriesAsync(ZipArchive zipArchive, DirectoryInfo directoryInfo,
                                                    string sourcePath, long totalBytes, Action<float>? progress,
                                                    CancellationToken token)
        {
            var folderSeparators = new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar };
            var buffer = new byte[BufferSize];
            var currentBytes = 0L;

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
                    var entry = zipArchive.CreateEntry(entryName);
                    entry.LastWriteTime = sourceInfo.GetLastWriteTime();
                    currentBytes += await WriteEntryAsync(sourceInfo, entry, buffer, progress, totalBytes,
                                                          currentBytes, token).ConfigureAwait(false);
                }
                else if (info is DirectoryInfo dirInfo && dirInfo.IsEmpty())
                {
                    // create entry for empty folder
                    zipArchive.CreateEntry(entryName + "/");
                }
            }
        }

        private static async Task<long> WriteEntryAsync(FileInfo sourceInfo, ZipArchiveEntry entry, byte[] buffer,
                                                        Action<float>? progress, long totalBytes, long currentBytes,
                                                        CancellationToken token)
        {
            // the source is only read from, so there is nothing to flush on the way out
            using var source = OpenFileForReading(sourceInfo.FullName);

            var destination = await OpenEntryAsync(entry, token).ConfigureAwait(false);
            try
            {
                return await CopyStreamAsync(source, destination, buffer, progress, totalBytes, currentBytes, token)
                    .ConfigureAwait(false);
            }
            finally
            {
                await DisposeAsync(destination).ConfigureAwait(false);
            }
        }

        private static async Task ExtractToDirectoryAsync(string archiveFilePath, string destFolderPath,
                                                          Action<float>? progress, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            var destRootPath = Directory.CreateDirectory(destFolderPath).FullName;

            using var fileStream = OpenFileForReading(archiveFilePath);

            var zipArchive = await OpenArchiveAsync(fileStream, ZipArchiveMode.Read, token).ConfigureAwait(false);
            try
            {
                await ExtractEntriesAsync(zipArchive, destRootPath, progress, token).ConfigureAwait(false);
            }
            finally
            {
                await DisposeAsync(zipArchive).ConfigureAwait(false);
            }
        }

        private static async Task ExtractEntriesAsync(ZipArchive zipArchive, string destRootPath,
                                                      Action<float>? progress, CancellationToken token)
        {
            var buffer = new byte[BufferSize];
            var currentBytes = 0L;

            // initial run through of the archive, getting the total number of bytes to extract
            var totalBytes = zipArchive.Entries.Sum(entry => entry.Length);

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
                    currentBytes += await ExtractEntryAsync(entry, destPath, buffer, progress, totalBytes,
                                                            currentBytes, token).ConfigureAwait(false);
                    File.SetLastWriteTimeUtc(destPath, ToUtc(entry.LastWriteTime));
                }
            }
        }

        private static async Task<long> ExtractEntryAsync(ZipArchiveEntry entry, string destPath, byte[] buffer,
                                                          Action<float>? progress, long totalBytes, long currentBytes,
                                                          CancellationToken token)
        {
            // the entry is only read from, so there is nothing to flush on the way out
            using var source = await OpenEntryAsync(entry, token).ConfigureAwait(false);

            var destination = OpenFileForWriting(destPath);
            try
            {
                return await CopyStreamAsync(source, destination, buffer, progress, totalBytes, currentBytes, token)
                    .ConfigureAwait(false);
            }
            finally
            {
                // the write stream flushes on the way out, so let that happen asynchronously as well
                await DisposeAsync(destination).ConfigureAwait(false);
            }
        }

        private static async Task<long> CopyStreamAsync(Stream source, Stream destination, byte[] buffer,
                                                        Action<float>? progress, long totalBytes, long currentBytes,
                                                        CancellationToken token)
        {
            var totalCopied = 0L;
            var bytesRead = await source.ReadAsync(buffer, 0, buffer.Length, token).ConfigureAwait(false);
            while (bytesRead > 0)
            {
                await destination.WriteAsync(buffer, 0, bytesRead, token).ConfigureAwait(false);

                totalCopied += bytesRead;
                progress?.Invoke((currentBytes + totalCopied) / (float)totalBytes);

                // checked explicitly rather than left to the token on the next read, so that cancelling
                // from inside the progress callback takes effect before any more of the file is copied
                token.ThrowIfCancellationRequested();
                bytesRead = await source.ReadAsync(buffer, 0, buffer.Length, token).ConfigureAwait(false);
            }

            return totalCopied;
        }

        private static FileStream OpenFileForReading(string path) =>
            new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize, useAsync: true);

        private static FileStream OpenFileForWriting(string path) =>
            new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, BufferSize, useAsync: true);

        /// <summary>
        /// <see cref="FileMode.CreateNew"/> matches what <see cref="ZipFile"/> does for
        /// <see cref="ZipArchiveMode.Create"/>: writing an archive over an existing file is an error
        /// rather than a silent overwrite.
        /// </summary>
        private static FileStream OpenArchiveForWriting(string path) =>
            new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, BufferSize, useAsync: true);

        /// <summary>
        /// .NET 10 can read and write an archive's central directory without blocking; netstandard2.0 has
        /// no such API and falls back to the synchronous constructor. Entry contents - the bulk of the
        /// work by far - are transferred asynchronously either way.
        /// </summary>
        private static Task<ZipArchive> OpenArchiveAsync(Stream stream, ZipArchiveMode mode, CancellationToken token)
        {
#if NET10_0_OR_GREATER
            return ZipArchive.CreateAsync(stream, mode, leaveOpen: true, Encoding.UTF8, token);
#else
            token.ThrowIfCancellationRequested();
            return Task.FromResult(new ZipArchive(stream, mode, leaveOpen: true, Encoding.UTF8));
#endif
        }

        private static Task<Stream> OpenEntryAsync(ZipArchiveEntry entry, CancellationToken token)
        {
#if NET10_0_OR_GREATER
            return entry.OpenAsync(token);
#else
            token.ThrowIfCancellationRequested();
            return Task.FromResult(entry.Open());
#endif
        }

        private static Task DisposeAsync(IDisposable disposable)
        {
#if NET10_0_OR_GREATER
            if (disposable is IAsyncDisposable asyncDisposable)
            {
                return asyncDisposable.DisposeAsync().AsTask();
            }
#endif
            disposable.Dispose();
            return Task.CompletedTask;
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

    }
}
