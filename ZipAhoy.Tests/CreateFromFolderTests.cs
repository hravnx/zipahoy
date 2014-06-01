using System;
using System.Linq;
using System.Threading;
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
                var ex = Assert.Throws<ArgumentNullException>(() => Archive.CreateFromFolder(arg, "well.zip", null, CancellationToken.None));
                Assert.Equal("folderPath", ex.ParamName);

                ex = Assert.Throws<ArgumentNullException>(() => Archive.CreateFromFolder("somestuff", arg, null, CancellationToken.None));
                Assert.Equal("archiveFilePath", ex.ParamName);
            }
        }

        [Fact]
        public void Create_from_nonexisting_folder_throws()
        {
            var name = FileUtils.GetTempFilename(".tmp");
            Assert.Throws<ArgumentException>(() => Archive.CreateFromFolder(name, "well.zip", null, CancellationToken.None));
        }

        [Fact]
        public async Task Create_from_empty_folder_results_in_minimal_zip_file()
        {
            using (var tempFolder = "zip-".CreateTempFolder())
            using (var zipFile = TempFile.Create("zip-", ".zip"))
            {
                await Archive.CreateFromFolder(tempFolder.FullPath, zipFile.FilePath, null, CancellationToken.None);
                
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
                await Archive.CreateFromFolder(tempFolder.FullPath, zipFile.FilePath, null, CancellationToken.None);

                var info = zipFile.GetInfo();
                Assert.True(info.Exists);
                Assert.True(info.Length > 0);
            }
        }

        [Fact]
        public async Task Create_reports_granular_progress()
        {
            var progress = new TestProgressReporter();

            using (var tempFolder = "zip-".CreateTempFolder())
            using (var zipFile = TempFile.Create("zip-", ".zip"))
            {
                tempFolder.CreateDummyFile("dummy.bin", 234);
                tempFolder.CreateDummyFile("dummy2.bin", 143000);
                await Archive.CreateFromFolder(tempFolder.FullPath, zipFile.FilePath, progress, CancellationToken.None);

                Assert.Equal(3, progress.ReportedProgress.Count);

                Assert.Equal(1.0f, progress.ReportedProgress.Last());
            }
        }

        [Fact]
        public void Create_can_be_canceled()
        {
            var cts = new CancellationTokenSource();
            var progress = new TestCancelProgressReporter(2, cts);

            using (var tempFolder = "zip-".CreateTempFolder())
            using (var zipFile = TempFile.Create("zip-", ".zip"))
            {
                tempFolder.CreateDummyFile("dummy1.bin", 234);
                tempFolder.CreateDummyFile("dummy2.bin", 345);
                tempFolder.CreateDummyFile("dummy3.bin", 456);

                var ex = Assert.Throws<AggregateException>(() =>
                        Archive.CreateFromFolder(tempFolder.FullPath, zipFile.FilePath, progress, cts.Token).Wait());
                Assert.Equal(1, ex.InnerExceptions.Count);
                Assert.IsType<OperationCanceledException>(ex.InnerExceptions[0]);

                Assert.Equal(2, progress.ReportCount);
            }
        }
        
    }
}
