using System;
using System.IO;
using Xunit;

namespace ZipAhoy.Tests
{
    public static class FileUtils
    {

        public static TempFolder CreateTempFolder(this string prefix)
        {
            return new TempFolder(prefix);
        }

        public static string GetTempFilename(string ext)
        {
            if (String.IsNullOrWhiteSpace(ext))
            {
                throw new ArgumentNullException("ext");
            }
            if (ext[0] != '.')
            {
                ext = "." + ext;
            }
            return Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("D") + ext);
        }


        public static bool FolderIsEmpty(string folderPath)
        {
            foreach(var entry in Directory.EnumerateFileSystemEntries(folderPath, "*"))
            {
                return false;
            }
            return true;
        }

        public static void CreateDummyFile(string filepath, int size)
        {
            using (var stream = new FileStream(filepath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
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
        }



        public static bool AreFoldersTheSame(string folderA, string folderB)
        {
            var a = Directory.EnumerateFileSystemEntries(folderA, "*", SearchOption.AllDirectories).GetEnumerator();
            var b = Directory.EnumerateFileSystemEntries(folderB, "*", SearchOption.AllDirectories).GetEnumerator();

            var aMore = a.MoveNext();
            var bMore = b.MoveNext();
            while(aMore && bMore)
            {
                var aName = a.Current;
                var bName = b.Current;
                if(0 != String.Compare(aName.Substring(folderA.Length), bName.Substring(folderB.Length), ignoreCase: true))
                {
                    return false;
                }

                var aLength = GetLength(aName);
                var bLength = GetLength(bName);
                if(aLength != bLength)
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
            if(isDir)
            {
                return -1;
            }
            return (new FileInfo(pathName)).Length;
        }
    }
}
