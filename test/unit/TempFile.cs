using System;
using System.IO;
using System.Threading;
using Helpers;

namespace ZipAhoy.Tests
{
    using static ActionHelper;

    public sealed class TempFile : IDisposable
    {
        /// <summary>
        /// Get the full name of the temp file
        /// </summary>
        public string FilePath { get; private set; }

        private TempFile(string root, string prefix, string extension)
        {
            var baseName = Path.Combine(root, prefix + Guid.NewGuid().ToString("D"));
            FilePath = Path.ChangeExtension(baseName, extension);
        }

        /// <summary>
        /// Creates a temp file name in the temp folder of the current user with an optional prefix
        /// </summary>
        /// <param name="prefix">The prefix to prepend the name with</param>
        /// <param name="extension">The options extension (starting with a <c>.</c>) to append to the name</param>
        /// <returns>A newly created <c>TempFile</c> instance</returns>
        public static TempFile Create(string prefix = "", string extension = ".tmp") =>
            new TempFile(Path.GetTempPath(), prefix, extension);

        /// <summary>
        /// Creates a temp file name in a specified folder with an optional prefix
        /// </summary>
        /// <param name="prefix">The prefix to prepend the name with</param>
        /// <param name="extension">The options extension (starting with a <c>.</c>) to append to the name</param>
        /// <returns>A newly created <c>TempFile</c> instance</returns>
        public static TempFile CreateInFolder(string rootFolder, string prefix = "", string extension = ".tmp")
        {
            Require.IsNotBlank(rootFolder, nameof(rootFolder));
            Require.FolderExists(rootFolder, nameof(rootFolder));
            return new TempFile(rootFolder, prefix, extension);
        }

        public FileInfo GetInfo() => new FileInfo(FilePath);

        public void Dispose() =>
            Run(() => File.Delete(FilePath)).WithRetriesOn<IOException>();
    }
}
