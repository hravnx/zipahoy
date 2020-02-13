using System;
using System.IO;

namespace ZipAhoy.Tests
{
    using Helpers;
    using static ActionHelper;

    public class TempFolder : IDisposable
    {
        public string FullPath { get; private set; }

        public TempFolder(string prefix)
        {
            Require.IsNotBlank(prefix, nameof(prefix));

            var folderName = Guid.NewGuid().ToString("D");
            FullPath = Path.Combine(Path.GetTempPath(), prefix + folderName);
            Directory.CreateDirectory(FullPath);
        }

        public void CreateDummyFile(string relativePath, int size)
        {
            var fullPath = Path.GetFullPath(Path.Combine(FullPath, relativePath));
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            FileUtils.CreateDummyFile(fullPath, size);
        }

        public void Dispose() =>
            Run(() => Directory.Delete(FullPath, true)).WithRetriesOn<IOException>();

    }
}
