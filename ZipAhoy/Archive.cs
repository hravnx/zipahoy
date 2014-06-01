using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;

namespace ZipAhoy
{
    public class Archive
    {
        public static Task CreateFromFolder(string folderPath, string archiveFilePath)
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
            return Task.Run(() => 
            {
                ZipFile.CreateFromDirectory(folderPath, archiveFilePath);
            });
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
            if(!File.Exists(archiveFilePath))
            {
                throw new ArgumentException(String.Format("'{0}' does not exist", archiveFilePath), "archiveFilePath");
            }

            if(!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }
            return Task.Run(() => 
            {
                ZipFile.ExtractToDirectory(archiveFilePath, folderPath);
            });
        }
    }
}
