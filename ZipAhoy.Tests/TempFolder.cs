using System;
using System.IO;
using System.Threading;

namespace ZipAhoy.Tests
{
    public class TempFolder : IDisposable
    {
        public string FullPath { get; private set; }

        public TempFolder(string prefix)
        {
            if(String.IsNullOrWhiteSpace(prefix))
            {
                throw new ArgumentNullException("prefix");
            }

            var folderName = Guid.NewGuid().ToString("D");
            FullPath = Path.Combine(Path.GetTempPath(), prefix + folderName);
            Directory.CreateDirectory(FullPath);
        }

        public void CreateDummyFile(string relativePath, int size)
        {
            FileUtils.CreateDummyFile(Path.Combine(FullPath, relativePath), size);
        }

        public void Dispose()
        {
            // allow for retries, if the file system is still 
            // holding on to files in here
            for (int i = 0; i < 10; ++i)
            {
                try
                {
                    Directory.Delete(FullPath, true);
                }
                catch (IOException)
                {
                    Thread.Sleep(10);
                }
            }
        }


    }
}
