## Zip Ahoy
Zip Ahoy is a small library for creating/extracting directories to/from zip-files in C#. It targets `netstandard2.0` and `net10.0`.

[![NuGet version (ZipAhoy)](https://img.shields.io/nuget/v/ZipAhoy.svg?style=flat-square)](https://www.nuget.org/packages/ZipAhoy/)

### Why
The high-level helpers in the `System.IO.Compression.*` namespaces report no progress at all - an archive either finishes or it doesn't. ZipAhoy reports progress as the bytes are copied, so a single large file still moves the number.

On frameworks older than .NET 10 those helpers are synchronous and cannot be cancelled either. .NET 10 added cancellable async versions of them, so on that target progress reporting is the remaining difference.

ZipAhoy provides two static methods, both returning `Task`:

```csharp
Archive.CreateFromFolderAsync(
    string folderPath, 
    string archiveFilePath, 
    Action<float>? progress = null,      
    CancellationToken token = default)
```

and

```csharp
Archive.ExtractToFolderAsync(
    string archiveFilePath, 
    string destFolderPath,
    Action<float>? progress = null, 
    CancellationToken token = default)
```

`progress` is called with a fraction from `0.0` to `1.0`. Bad arguments are reported synchronously, so an
`ArgumentException` surfaces at the call rather than on the first `await`.

`ExtractToFolderAsync` creates the destination folder if it does not already exist, and throws
`InvalidDataException` if the archive contains an entry that would be written outside of it.

Both methods do real asynchronous I/O rather than wrapping synchronous work in `Task.Run`, so they
don't occupy a thread while a large archive is being read or written. Two things worth knowing:

- Progress callbacks are not guaranteed to run on any particular thread, so marshal to your UI thread
  before touching anything on it.
- `CreateFromFolderAsync` walks the source folder synchronously before its first `await`, to total up
  the bytes it is about to write. For a very large tree, don't call it directly on a UI thread.

### Upgrading from 0.3.0
The two methods keep their signatures, but some behaviour changed. In rough order of how likely you
are to notice:

- Extraction now rejects entries that resolve to a path outside the destination folder, with
  `InvalidDataException`. An archive that relied on writing outside the folder it was extracted to
  will stop working, which is the point.
- `ExtractToFolderAsync` creates the destination folder when it is missing. Previously it threw
  `ArgumentException`, so any workaround for that is no longer needed.
- Entry names are written with `/` as the separator, as the zip format requires. Archives that 0.3.0
  wrote on Windows used `\`, which other tools, and other platforms, read as part of the file name
  rather than as a folder. Reading such an archive still works.
- Timestamps survive a round-trip. 0.3.0 shifted them by the local UTC offset each time, so extracted
  files will now carry different - correct - times than before.
- Cancelling leaves the task in the `Canceled` state rather than `Faulted`, which matters if you
  inspect `Task.Status` or `IsCanceled`. Awaiting still throws `OperationCanceledException`, as it did.
  A cancelled or failed `CreateFromFolderAsync` also deletes its half-written archive instead of
  leaving it behind.
- The `Helpers` namespace is gone. It was public by accident and exported extension methods on
  `DirectoryInfo` and `FileInfo`; if you were using `Require` or `FileSystemHelpers`, you now need your
  own copy.

### Examples
There are usage examples in the test project, under `test/unit`.

### How to install
There is a NuGet package, called ZipAhoy [here](https://www.nuget.org/packages/ZipAhoy/).

Alternatively, just grab the source and add the class library in `src/lib` to your project. 

### License
ZipAhoy is released under the MIT license. See LICENSE.md for details.
