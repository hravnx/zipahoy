using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Helpers
{

    public static class Require
    {
        public static void IsNotBlank(string s, string nameOfArg)
        {
            if (string.IsNullOrWhiteSpace(s))
            {
                throw new ArgumentNullException(nameOfArg, "must not be null or blank");
            }
        }

        public static void FolderExists(string folderPath, string nameOfArg)
        {
            if (!Directory.Exists(folderPath))
            {
                throw new ArgumentException($"Folder '{nameOfArg}' does not exist", nameOfArg);
            }
        }

        public static void FileExists(string filePath, string nameOfArg)
        {
            if (!File.Exists(filePath))
            {
                throw new ArgumentException($"File '{nameOfArg}' does not exist", nameOfArg);
            }
        }
    }

    public static class FileSystemHelpers
    {
        public static bool IsEmpty(this DirectoryInfo dirInfo)
        {
            return !dirInfo.EnumerateFileSystemInfos().Any();
        }

        public static bool IsEmptyDirectory(this string directoryPath)
        {
            Require.IsNotBlank(directoryPath, nameof(directoryPath));
            return !Directory.EnumerateFileSystemEntries(directoryPath, "*").Any();
        }

        public static IEnumerable<FileInfo> EnumAllFiles(this DirectoryInfo dirInfo)
        {
            return dirInfo.EnumerateFiles("*", SearchOption.AllDirectories);
        }

        public static IEnumerable<FileSystemInfo> EnumAllEntries(this DirectoryInfo dirInfo)
        {
            return dirInfo.EnumerateFileSystemInfos("*", SearchOption.AllDirectories);
        }

        public static DateTimeOffset GetLastWriteTime(this FileInfo fileInfo)
        {
            var lastWriteTime = new DateTimeOffset(fileInfo.LastWriteTimeUtc, TimeSpan.Zero);
            if (lastWriteTime.Year < 1980 || lastWriteTime.Year > 2107)
            {
                return new DateTimeOffset(1980, 1, 1, 0, 0, 0, TimeSpan.Zero);
            }
            return lastWriteTime;
        }
    }

}