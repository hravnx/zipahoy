using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

namespace ZipAhoy.Tests
{
    public sealed class TempFile : IDisposable
    {
        /// <summary>
        /// Get the full name of the temp file
        /// </summary>
        public string FilePath { get; private set; }

        private TempFile(string root, string prefix, string extension)
        {
            FilePath = Path.Combine(root, prefix + Guid.NewGuid().ToString("D") + extension);
        }

        /// <summary>
        /// Creates a temp file name in the temp folder of the current user with an optional prefix
        /// </summary>
        /// <param name="prefix">The prefix to prepend the name with</param>
        /// <param name="extension">The options extension (starting woth a <c>.</c>) to append to the name</param>
        /// <returns>A newly created <c>TempFile</c> instance</returns>
        public static TempFile Create(string prefix = "", string extension = ".tmp")
        {
            return new TempFile(Path.GetTempPath(), prefix, extension);
        }

        /// <summary>
        /// Creates a temp file name in a specified folder with an optional prefix
        /// </summary>
        /// <param name="prefix">The prefix to prepend the name with</param>
        /// <param name="extension">The options extension (starting woth a <c>.</c>) to append to the name</param>
        /// <returns>A newly created <c>TempFile</c> instance</returns>
        public static TempFile CreateInFolder(string rootFolder, string prefix = "", string extension = ".tmp")
        {
            if(String.IsNullOrWhiteSpace(rootFolder))
            {
                throw new ArgumentNullException("rootFolder", "'rootFolder' cannot be null or empty, maybe you want the Create static factory method instead?");
            }
            if(!Directory.Exists(rootFolder))
            {
                throw new ArgumentException(String.Format("Folder '{0}' does not exist", rootFolder), "rootFolder");

            }
            return new TempFile(rootFolder, prefix, extension);
        }

        public FileInfo GetInfo()
        {
            return new FileInfo(FilePath);
        }

        public void Dispose()
        {
            // allow for retries, if the file system is still 
            // holding on to this file
            for (int i = 0; i < 10; ++i)
            {
                try
                {
                    File.Delete(FilePath);
                }
                catch (IOException)
                {
                    Thread.Sleep(10);
                }
            }
        }
    }
}
