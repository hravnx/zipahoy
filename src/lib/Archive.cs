using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ZipAhoy
{
    using Helpers;

    public static class Archive
    {
        /// <summary>
        /// Zips the file sin a folder, including sub-folders.
        /// </summary>
        /// <param name="folderPath">The directory to zip</param>
        /// <param name="archiveFilePath">The path of the resulting zip archive</param>
        /// <param name="progress">Optional progress action</param>
        /// <param name="token">Optional cancellation token</param>
        /// <returns>Nothing</returns>
        public static Task CreateFromFolderAsync(string folderPath, string archiveFilePath,
                                                 Action<float> progress = default, CancellationToken token = default)
        {
            Require.IsNotBlank(folderPath, nameof(folderPath));
            Require.IsNotBlank(archiveFilePath, nameof(archiveFilePath));
            Require.FolderExists(folderPath, nameof(folderPath));

            return Task.Run(() => CreateFromDirectoryHelper(folderPath, archiveFilePath, progress, token));
        }

        /// <summary>
        /// Unzips a zip archive to a specified folder
        /// </summary>
        /// <param name="archiveFilePath">The zip archive to unzip</param>
        /// <param name="destFolderPath">The folder to unpack the zip file in</param>
        /// <param name="progress">Optional progress action</param>
        /// <param name="token">Optional cancellation token</param>
        /// <returns>Nothing</returns>
        public static Task ExtractToFolderAsync(string archiveFilePath, string destFolderPath,
                                                Action<float> progress = default, CancellationToken token = default)
        {
            Require.IsNotBlank(archiveFilePath, nameof(archiveFilePath));
            Require.IsNotBlank(destFolderPath, nameof(destFolderPath));
            Require.FileExists(archiveFilePath, nameof(archiveFilePath));
            Require.FolderExists(destFolderPath, nameof(destFolderPath));

            return Task.Run(() => ExtractToDirectoryHelper(archiveFilePath, destFolderPath, progress, token));
        }

        private static void CreateFromDirectoryHelper(string sourcePath, string archiveFilePath, Action<float> progress,
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

            const int BUFFER_SIZE = 81920; // just below the LOH threshold
            var buffer = new byte[BUFFER_SIZE];

            var currentBytes = 0L;
            using var zipArchive = ZipFile.Open(archiveFilePath, ZipArchiveMode.Create, Encoding.UTF8);
            foreach (var info in directoryInfo.EnumAllEntries())
            {
                token.ThrowIfCancellationRequested();

                int length = info.FullName.Length - sourcePath.Length;
                var entryName = info.FullName.Substring(sourcePath.Length, length).TrimStart(folderSeparators);
                if (info is FileInfo sourceInfo)
                {
                    using var source = sourceInfo.OpenRead();
                    var entry = zipArchive.CreateEntry(entryName);
                    entry.LastWriteTime = sourceInfo.GetLastWriteTime();
                    using var destination = entry.Open();
                    currentBytes += StreamCopyHelper(source, destination, buffer, progress, totalBytes, currentBytes,
                                                     token);
                }
                else if (info is DirectoryInfo dirInfo && dirInfo.IsEmpty())
                {
                    // create entry for empty folder
                    zipArchive.CreateEntry(string.Concat(entryName, Path.DirectorySeparatorChar));
                }
            }
        }

        private static void ExtractToDirectoryHelper(string archiveFilePath, string destFolderPath,
                                                     Action<float> progress, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            using var zipArchive = ZipFile.Open(archiveFilePath, ZipArchiveMode.Read, Encoding.UTF8);
            var currentBytes = 0L;

            // initial run through of the directory, getting the total number of bytes to extract
            var totalBytes = zipArchive.Entries.Sum(entry => entry.Length);
            const int BUFFER_SIZE = 81920; // just below the LOH threshold
            var buffer = new byte[BUFFER_SIZE];

            // second run through, actually extracting entries
            foreach (var entry in zipArchive.Entries)
            {
                token.ThrowIfCancellationRequested();
                var destPath = Path.Combine(Path.GetFullPath(destFolderPath), entry.FullName);

                // file or directory?
                if (Path.GetFileName(destPath).Length != 0)
                {
                    // it's a file, so create its containing folder(s) ...
                    Directory.CreateDirectory(Path.GetDirectoryName(destPath));
                    // ... and extract it
                    using (var dest = File.Open(destPath, FileMode.Create, FileAccess.Write, FileShare.None))
                    using (var source = entry.Open())
                    {
                        currentBytes += StreamCopyHelper(source, dest, buffer, progress, totalBytes, currentBytes, token);
                    }
                    File.SetLastWriteTime(destPath, entry.LastWriteTime.DateTime);
                }
                else
                {
                    // it's an empty directory, so simply create it
                    Directory.CreateDirectory(destPath);
                    Directory.SetLastWriteTime(destPath, entry.LastWriteTime.DateTime);
                }
            }
        }

        private static long StreamCopyHelper(Stream source, Stream destination, byte[] buffer, Action<float> progress,
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
