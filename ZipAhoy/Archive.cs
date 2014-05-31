using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;

namespace ZipAhoy
{
    public class Archive
    {

        public static Task<bool> CreateFromFolder(string folderPath, string archiveFilePath)
        {
            if (String.IsNullOrWhiteSpace(folderPath))
            {
                throw new ArgumentNullException("folderPath");
            }

            if (String.IsNullOrWhiteSpace(archiveFilePath))
            {
                throw new ArgumentNullException("archiveFilePath");
            }

            if(!Directory.Exists(folderPath))
            {
                throw new ArgumentException(String.Format("'{0}' does not exist", folderPath), "folderPath");
            }

            ZipFile.CreateFromDirectory(folderPath, archiveFilePath);

            return Task.FromResult(true);
        }
    }
}
