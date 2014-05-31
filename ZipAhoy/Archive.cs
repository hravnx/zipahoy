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
            using(var s = File.Create(archiveFilePath))
            {

            }
            return Task.FromResult(true);
        }
    }
}
