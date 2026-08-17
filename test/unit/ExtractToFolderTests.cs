using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ZipAhoy.Tests
{
    public class ExtractToFolderTests
    {
        [Theory]
        [InlineData(null), InlineData(""), InlineData("    \t   ")]
        public async Task Extract_with_bad_arguments_throws(string? arg)
        {
            var ex = await Assert.ThrowsAsync<ArgumentNullException>(
                () => Archive.ExtractToFolderAsync(arg!, "some stuff", null, CancellationToken.None));
            Assert.Equal("archiveFilePath", ex.ParamName);

            ex = await Assert.ThrowsAsync<ArgumentNullException>(
                () => Archive.ExtractToFolderAsync("well.zip", arg!, null, CancellationToken.None));
            Assert.Equal("destFolderPath", ex.ParamName);
        }

        [Fact]
        public async Task Extract_from_non_existing_zip_file_throws()
        {
            var ex = await Assert.ThrowsAsync<ArgumentException>(
                () => Archive.ExtractToFolderAsync(
                    Guid.NewGuid().ToString("D") + ".zip",
                    "some stuff", null, CancellationToken.None));
            Assert.Equal("archiveFilePath", ex.ParamName);
        }

        [Fact]
        public async Task Extracting_empty_zip_file_leaves_the_destination_unchanged()
        {
            using var zipFile = TempFile.Create("zip-", ".zip");
            using (var tempFolder = "zip-".CreateTempFolder())
            {
                await Archive.CreateFromFolderAsync(tempFolder.FullPath, zipFile.FilePath, null, CancellationToken.None);
            }

            using (var tempFolder = "zip-".CreateTempFolder())
            {
                await Archive.ExtractToFolderAsync(zipFile.FilePath, tempFolder.FullPath, null, CancellationToken.None);
                Assert.Empty(Directory.EnumerateFileSystemEntries(tempFolder.FullPath));
            }
        }

        [Fact]
        public async Task Extracting_zip_file_extracts_files_to_the_dest_folder()
        {
            using var zipFile = TempFile.Create("zip-", ".zip");

            using var tempFolder = "zip-".CreateTempFolder();
            tempFolder.CreateDummyFile("dar/dummy.bin", 421);
            tempFolder.CreateDummyFile("dummy.bin", 234);
            await Archive.CreateFromFolderAsync(tempFolder.FullPath, zipFile.FilePath, null, CancellationToken.None);

            using var destFolder = "zip-".CreateTempFolder();
            await Archive.ExtractToFolderAsync(zipFile.FilePath, destFolder.FullPath, null, CancellationToken.None);

            Assert.True(FileUtils.AreFoldersTheSame(tempFolder.FullPath, destFolder.FullPath));
        }

        [Fact]
        public async Task Extract_reports_granular_progress()
        {
            using var zipFile = TempFile.Create("zip-", ".zip");
            using (var tempFolder = "zip-".CreateTempFolder())
            {
                tempFolder.CreateDummyFile("dummy.bin", 234);
                tempFolder.CreateDummyFile("dummy2.bin", 143000);
                await Archive.CreateFromFolderAsync(tempFolder.FullPath, zipFile.FilePath, null, CancellationToken.None);
            }

            var progress = new TestProgressReporter();

            using var destFolder = "zip-".CreateTempFolder();
            await Archive.ExtractToFolderAsync(zipFile.FilePath, destFolder.FullPath, progress, CancellationToken.None);

            Assert.Equal(3, progress.ReportedProgress.Count);
            Assert.Equal(1.0f, progress.ReportedProgress.Last());
        }

        [Fact]
        public async Task Extract_can_be_canceled()
        {
            using var zipFile = TempFile.Create("zip-", ".zip");
            using (var tempFolder = "zip-".CreateTempFolder())
            {
                tempFolder.CreateDummyFile("dummy1.bin", 234);
                tempFolder.CreateDummyFile("dummy2.bin", 345);
                tempFolder.CreateDummyFile("dummy3.bin", 456);
                await Archive.CreateFromFolderAsync(tempFolder.FullPath, zipFile.FilePath, null, CancellationToken.None);
            }

            var cts = new CancellationTokenSource();
            var progress = new TestCancelProgressReporter(2, cts);

            using var destFolder = "zip-".CreateTempFolder();
            await Assert.ThrowsAsync<OperationCanceledException>(() =>
                    Archive.ExtractToFolderAsync(zipFile.FilePath,
                                            destFolder.FullPath,
                                            progress,
                                            cts.Token));

            Assert.Equal(2, progress.ReportCount);
        }

        [Fact]
        public async Task Extract_creates_the_destination_folder_if_it_is_missing()
        {
            using var zipFile = TempFile.Create("zip-", ".zip");
            using var tempFolder = "zip-".CreateTempFolder();
            tempFolder.CreateDummyFile("dummy.bin", 234);
            await Archive.CreateFromFolderAsync(tempFolder.FullPath, zipFile.FilePath, null, CancellationToken.None);

            using var destRoot = "zip-".CreateTempFolder();
            var destFolder = Path.Combine(destRoot.FullPath, "nested", "output");

            await Archive.ExtractToFolderAsync(zipFile.FilePath, destFolder, null, CancellationToken.None);

            Assert.True(File.Exists(Path.Combine(destFolder, "dummy.bin")));
        }

        [Fact]
        public async Task Extract_rejects_entries_pointing_outside_the_dest_folder()
        {
            using var zipFile = TempFile.Create("zip-", ".zip");
            using (var archive = ZipFile.Open(zipFile.FilePath, ZipArchiveMode.Create))
            {
                using var stream = archive.CreateEntry("../pwned.txt").Open();
                stream.Write(new byte[] { 1, 2, 3 }, 0, 3);
            }

            using var destRoot = "zip-".CreateTempFolder();
            var destFolder = Path.Combine(destRoot.FullPath, "sub");

            await Assert.ThrowsAsync<InvalidDataException>(
                () => Archive.ExtractToFolderAsync(zipFile.FilePath, destFolder, null, CancellationToken.None));

            Assert.False(File.Exists(Path.Combine(destRoot.FullPath, "pwned.txt")));
        }

        [Fact]
        public async Task Extract_round_trips_file_timestamps_in_utc()
        {
            // a stamp that is not on a whole hour, so a local-vs-utc mixup can't accidentally match
            var stamp = new DateTime(2020, 6, 15, 12, 34, 0, DateTimeKind.Utc);

            using var zipFile = TempFile.Create("zip-", ".zip");
            using (var tempFolder = "zip-".CreateTempFolder())
            {
                tempFolder.CreateDummyFile("dummy.bin", 234);
                File.SetLastWriteTimeUtc(Path.Combine(tempFolder.FullPath, "dummy.bin"), stamp);
                await Archive.CreateFromFolderAsync(tempFolder.FullPath, zipFile.FilePath, null, CancellationToken.None);
            }

            using var destFolder = "zip-".CreateTempFolder();
            await Archive.ExtractToFolderAsync(zipFile.FilePath, destFolder.FullPath, null, CancellationToken.None);

            var extracted = File.GetLastWriteTimeUtc(Path.Combine(destFolder.FullPath, "dummy.bin"));

            // zip timestamps have a 2 second resolution
            Assert.Equal(stamp, extracted, TimeSpan.FromSeconds(2));
        }

        [Fact]
        public async Task Extract_round_trips_empty_folders()
        {
            using var zipFile = TempFile.Create("zip-", ".zip");
            using (var tempFolder = "zip-".CreateTempFolder())
            {
                Directory.CreateDirectory(Path.Combine(tempFolder.FullPath, "emptydir"));
                await Archive.CreateFromFolderAsync(tempFolder.FullPath, zipFile.FilePath, null, CancellationToken.None);
            }

            using var destFolder = "zip-".CreateTempFolder();
            await Archive.ExtractToFolderAsync(zipFile.FilePath, destFolder.FullPath, null, CancellationToken.None);

            Assert.True(Directory.Exists(Path.Combine(destFolder.FullPath, "emptydir")));
        }
    }
}
