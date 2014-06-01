using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace ZipAhoy.Tests
{

    public class ExtractToFolderTests
    {
        [Fact]
        public void Extract_with_bad_arguments_throws()
        {
            foreach (var arg in new[] { null, "", "   \t   " })
            {
                var ex = Assert.Throws<ArgumentNullException>(() => Archive.ExtractToFolder(arg, "some stuff", null));
                Assert.Equal("archiveFilePath", ex.ParamName);

                ex = Assert.Throws<ArgumentNullException>(() => Archive.ExtractToFolder("well.zip", arg, null));
                Assert.Equal("folderPath", ex.ParamName);
            }
        }

        [Fact]
        public void Extract_from_non_existing_zip_file_throws()
        {
            var ex = Assert.Throws<ArgumentException>(() => Archive.ExtractToFolder(Guid.NewGuid().ToString("D") + ".zip", "some stuff", null));
            Assert.Equal("archiveFilePath", ex.ParamName);

        }

        [Fact]
        public async Task Extracting_empty_zip_file_leaves_the_destination_unchanged()
        {
            using (var zipFile = TempFile.Create("zip-", ".zip"))
            {
                using (var tempFolder = "zip-".CreateTempFolder())
                {
                    await Archive.CreateFromFolder(tempFolder.FullPath, zipFile.FilePath, null);
                }

                using (var tempFolder = "zip-".CreateTempFolder())
                {
                    await Archive.ExtractToFolder(zipFile.FilePath, tempFolder.FullPath, null);
                    Assert.True(FileUtils.FolderIsEmpty(tempFolder.FullPath));
                }
            }
        }

        [Fact]
        public async Task Extracting_zip_file_extracts_files_to_the_dest_folder()
        {
            using (var tempFolder = "zip-".CreateTempFolder())
            using (var zipFile = TempFile.Create("zip-", ".zip"))
            {
                tempFolder.CreateDummyFile("dar\\dummy.bin", 421);
                tempFolder.CreateDummyFile("dummy.bin", 234);

                await Archive.CreateFromFolder(tempFolder.FullPath, zipFile.FilePath, null);

                using(var destFolder = "zip-".CreateTempFolder())
                {
                    await Archive.ExtractToFolder(zipFile.FilePath, destFolder.FullPath, null);

                    Assert.True(FileUtils.AreFoldersTheSame(tempFolder.FullPath, destFolder.FullPath));
                }
            }
        }

        [Fact]
        public async Task Extract_reports_granular_progress()
        {
            using (var zipFile = TempFile.Create("zip-", ".zip"))
            {
                using (var tempFolder = "zip-".CreateTempFolder())
                {
                    tempFolder.CreateDummyFile("dummy.bin", 234);
                    tempFolder.CreateDummyFile("dummy2.bin", 143000);
                    await Archive.CreateFromFolder(tempFolder.FullPath, zipFile.FilePath, null);
                }

                var progress = new TestProgressReporter();

                using (var destFolder = "zip-".CreateTempFolder())
                {
                    await Archive.ExtractToFolder(zipFile.FilePath, destFolder.FullPath, progress);

                    Assert.Equal(3, progress.ReportedProgress.Count);
                    Assert.Equal(1.0f, progress.ReportedProgress.Last());
                }

            }

        }
        

    }
}
