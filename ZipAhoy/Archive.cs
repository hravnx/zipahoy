using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
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

            using(var s = File.Create(archiveFilePath))
            {

            }
            return Task.FromResult(true);
        }
    }
}
