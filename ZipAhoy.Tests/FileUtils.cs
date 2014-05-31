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

    }
}
