using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ZipAhoy
{
    public static class Archive
    {
        public static Task CreateFromFolder(string folderPath, string archiveFilePath, IProgress<float> progress)
        {
            if (String.IsNullOrWhiteSpace(folderPath))
            {
                throw new ArgumentNullException("folderPath");
            }

            if (String.IsNullOrWhiteSpace(archiveFilePath))
            {
                throw new ArgumentNullException("archiveFilePath");
            }

            if (!Directory.Exists(folderPath))
            {
                throw new ArgumentException(String.Format("'{0}' does not exist", folderPath), "folderPath");
            }

            return CreateFromDirectoryHelper(folderPath, archiveFilePath, progress);
        }

        public static Task ExtractToFolder(string archiveFilePath, string folderPath)
        {
            if (String.IsNullOrWhiteSpace(archiveFilePath))
            {
                throw new ArgumentNullException("archiveFilePath");
            }
            if (String.IsNullOrWhiteSpace(folderPath))
            {
                throw new ArgumentNullException("folderPath");
            }
            if (!File.Exists(archiveFilePath))
            {
                throw new ArgumentException(String.Format("'{0}' does not exist", archiveFilePath), "archiveFilePath");
            }

            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }
            return Task.Run(() =>
            {
                ZipFile.ExtractToDirectory(archiveFilePath, folderPath);
            });
        }

        private static Task CreateFromDirectoryHelper(string sourcePath, string archiveFilePath, IProgress<float> progress)
        {
            sourcePath = Path.GetFullPath(sourcePath);
            archiveFilePath = Path.GetFullPath(archiveFilePath);

            var folderSeparators = new char[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar };

            return Task.Run(() =>
            {
                using (var zipArchive = ZipFile.Open(archiveFilePath, ZipArchiveMode.Create, Encoding.UTF8))
                {
                    var directoryInfo = new DirectoryInfo(sourcePath);

                    // this code results in two run-throughs of the source folder, but
                    // because of windows file system caching, the second run-through below, 
                    // will be much faster than the first
                    var totalBytes = directoryInfo
                        .EnumerateFiles("*", SearchOption.AllDirectories)
                        .Sum(fi => fi.Length);
                    var currentBytes = 0L;

                    foreach (var info in directoryInfo.EnumerateFileSystemInfos("*", SearchOption.AllDirectories))
                    {
                        int length = info.FullName.Length - sourcePath.Length;
                        var entryName = info.FullName.Substring(sourcePath.Length, length).TrimStart(folderSeparators);
                        if (info is FileInfo)
                        {
                            var sourceInfo = (FileInfo)info;
                            using (var source = sourceInfo.OpenRead())
                            {
                                var entry = zipArchive.CreateEntry(entryName);
                                entry.LastWriteTime = sourceInfo.GetLastWriteTime();
                                using (var destination = entry.Open())
                                {
                                    currentBytes += StreamCopyHelper(source, destination, progress, totalBytes, currentBytes);
                                }
                            }
                        }
                        else if (info is DirectoryInfo)
                        {
                            if (IsFolderEmpty(info.FullName))
                            {
                                // create entry for empty folder
                                zipArchive.CreateEntry(string.Concat(entryName, Path.DirectorySeparatorChar));
                            }
                        }
                    }
                }
            });
        }

        private static long StreamCopyHelper(Stream source, Stream destination, IProgress<float> progress, long totalBytes, long currentBytes)
        {
            const int BUFFER_SIZE = 81920; // just below the LOH threshold
            var buffer = new byte[BUFFER_SIZE];
            var totalCopied = 0L;
            var bytesRead = source.Read(buffer, 0, BUFFER_SIZE);
            while (bytesRead > 0)
            {
                destination.Write(buffer, 0, bytesRead);
                totalCopied += bytesRead;
                if (progress != null)
                {
                    progress.Report((currentBytes + totalCopied) / (float)totalBytes);
                }
                bytesRead = source.Read(buffer, 0, BUFFER_SIZE);
            }

            return totalCopied;
        }

        private static DateTimeOffset GetLastWriteTime(this FileInfo fileInfo)
        {
            var lastWriteTime = new DateTimeOffset(fileInfo.LastWriteTimeUtc, TimeSpan.Zero);
            if (lastWriteTime.Year < 1980 || lastWriteTime.Year > 2107)
            {
                return new DateTimeOffset(1980, 1, 1, 0, 0, 0, TimeSpan.Zero);
            }
            return lastWriteTime;
        }

        private static bool IsFolderEmpty(string folderPath)
        {
            foreach (var entry in Directory.EnumerateFileSystemEntries(folderPath, "*"))
            {
                return false;
            }
            return true;
        }
    }
}
