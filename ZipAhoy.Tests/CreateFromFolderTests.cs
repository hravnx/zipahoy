using System;
using System.Threading.Tasks;
using Xunit;

namespace ZipAhoy.Tests
{
    public class CreateFromFolderTests
    {
        [Fact]
        public void Create_with_bad_arguments_throws()
        {
            foreach (var arg in new[] { null, "", "   \t   " })
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
        public async Task Create_from_empty_folder_results_in_minimal_zip_file()
        {
            using (var tempFolder = "zip-".CreateTempFolder())
            using (var zipFile = TempFile.Create("zip-", ".zip"))
            {
                await Archive.CreateFromFolder(tempFolder.FullPath, zipFile.FilePath);
                
                var info = zipFile.GetInfo();
                Assert.True(info.Exists);
                Assert.Equal(22, info.Length);
            }
        }

        [Fact]
        public async Task Create_from_non_empty_folder_results_in_nonempty_zip_file()
        {
            using (var tempFolder = "zip-".CreateTempFolder())
            using (var zipFile = TempFile.Create("zip-", ".zip"))
            {
                tempFolder.CreateDummyFile("dummy.bin", 234);
                await Archive.CreateFromFolder(tempFolder.FullPath, zipFile.FilePath);

                var info = zipFile.GetInfo();
                Assert.True(info.Exists);
                Assert.True(info.Length > 0);
            }
        }



    }
}
