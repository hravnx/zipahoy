using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace ZipAhoy.Tests
{
    public class CreateFromFolderTests
    {
        [Fact]
        public async Task Create_from_empty_folder_results_in_empty_zip_file()
        {
            using(var tempFolder = "zip-".CreateTempFolder())
            {
                var zipPath = FileUtils.GetTempFilename(".zip");
                await Archive.CreateFromFolder(tempFolder.FullPath, zipPath);

                var info = new FileInfo(zipPath);
                Assert.True(info.Exists);
                Assert.Equal(0, info.Length);
            }
        }
        
    }
}
