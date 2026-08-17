## Zip Ahoy
Zip Ahoy is a small library for creating/extracting directories to/from zip-files in C#. It targets `netstandard2.0` and `net10.0`.

[![NuGet version (ZipAhoy)](https://img.shields.io/nuget/v/ZipAhoy.svg?style=flat-square)](https://www.nuget.org/packages/ZipAhoy/)

### Why
Although there are already high-level helper methods for this in the `System.IO.Compression.*` namespaces, they lack support for cancellation, and a way to get meaningful progress reporting at a more fine-grained level than per-file.

ZipAhoy adds these things in two simple high-level static methods:

```csharp
Archive.CreateFromFolderAsync(
    string folderPath, 
    string archiveFilePath, 
    Action<float> progress = default,      
    CancellationToken token = default)
```

and

```csharp
Archive.ExtractToFolderAsync(
    string archiveFilePath, 
    string destFolderPath,
    Action<float> progress = default, 
    CancellationToken token = default)
```

`ExtractToFolderAsync` creates the destination folder if it does not already exist, and throws
`InvalidDataException` if the archive contains an entry that would be written outside of it.

### Examples
There are usage examples in the ZipAhoy.Tests project.

### How to install
There is a NuGet package, called ZipAhoy [here](https://www.nuget.org/packages/ZipAhoy/).

Alternatively, just grab the source and add the ZipAhoy class library to your project. 

### License
ZipAhoy is released under the MIT license. See LICENSE.md for details.
