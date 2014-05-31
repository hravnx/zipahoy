using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace ZipAhoy.Tests
{
    public class CreateFromFolderTests
    {
        [Fact]
        public void Create_with_bad_arguments_throws()
        {
            foreach(var arg in new [] {null, "", "   \t   "})
            {
                var ex = Assert.Throws<ArgumentNullException>(() => Archive.CreateFromFolder(arg, "well.zip"));
                Assert.Equal("folderPath", ex.ParamName);

                ex = Assert.Throws<ArgumentNullException>(() => Archive.CreateFromFolder("somestuff", arg));
                Assert.Equal("archiveFilePath", ex.ParamName);
            }
        }
        
        [Fact]
        public void Create_from_nonexisting_folder_throws()
        {
            var name = FileUtils.GetTempFilename(".tmp");
            Assert.Throws<ArgumentException>(() => Archive.CreateFromFolder(name, "well.zip"));
        }

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

                info.Delete();
            }
        }
        
    }
}
