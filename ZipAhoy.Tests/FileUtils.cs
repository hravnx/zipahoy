using System;
using System.IO;

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
            if(ext[0] != '.')
            {
                ext = "." + ext;
            }
            return Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("D") + ext);
        }



    }
}
