using System;
using System.IO;
using System.Threading;

using Xunit;

namespace ZipAhoy.Tests
{
    using Helpers;

    public static class ActionHelper
    {
        public static Action Run(Action action) => action;

        public static void WithRetriesOn<T>(this Action tryThis, int count = 10) where T : Exception
        {
            for (int i = 0; i < count; ++i)
            {
                try
                {
                    tryThis();
                    return;
                }
                catch (T)
                {
                    Thread.Sleep(10);
                }
            }
        }
    }

    public static class FileUtils
    {
        public static TempFolder CreateTempFolder(this string prefix) => new TempFolder(prefix);

        public static string GetTempFilename(string ext)
        {
            Require.IsNotBlank(ext, nameof(ext));
            var basePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("D"));
            return Path.ChangeExtension(basePath, ext);
        }

        public static void CreateDummyFile(string filepath, int size)
        {
            using var stream = new FileStream(filepath, FileMode.Create, FileAccess.Write, FileShare.None);
            var buffer = new byte[Math.Min(4096, size)];
            var bytesWritten = 0;
            while (bytesWritten < size)
            {
                var bytesToWrite = Math.Min(buffer.Length, size - bytesWritten);
                stream.Write(buffer, 0, bytesToWrite);
                bytesWritten += bytesToWrite;
            }
            Assert.Equal(size, stream.Length);
        }

        public static bool AreFoldersTheSame(string folderA, string folderB)
        {
            var a = Directory.EnumerateFileSystemEntries(folderA, "*", SearchOption.AllDirectories).GetEnumerator();
            var b = Directory.EnumerateFileSystemEntries(folderB, "*", SearchOption.AllDirectories).GetEnumerator();

            var aMore = a.MoveNext();
            var bMore = b.MoveNext();
            while (aMore && bMore)
            {
                var aName = a.Current;
                var bName = b.Current;

                if (0 != String.Compare(aName.Substring(folderA.Length), bName.Substring(folderB.Length), ignoreCase: true))
                {
                    return false;
                }

                var aLength = GetLength(aName);
                var bLength = GetLength(bName);
                if (aLength != bLength)
                {
                    return false;
                }

                aMore = a.MoveNext();
                bMore = b.MoveNext();
            }
            return aMore == bMore;
        }

        private static long GetLength(string pathName)
        {
            var isDir = (File.GetAttributes(pathName) & FileAttributes.Directory) == FileAttributes.Directory;
            return isDir ? -1 : (new FileInfo(pathName)).Length;
        }
    }
}
