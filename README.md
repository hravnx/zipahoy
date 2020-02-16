## Zip Ahoy
Zip Ahoy is a small NetStandard 2.0 library for creating/extracting directories to/from zip-files in C#.

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
Arhive.ExtractToFolderAsync(
    string archiveFilePath, 
    string destFolderPath,
    Action<float> progress = default, 
    CancellationToken token = default)
```

### Examples
There are usage examples in the ZipAhoy.Tests project.

### How to install
There is a NuGet package, called ZipAhoy [here](https://www.nuget.org/packages/ZipAhoy/).

Alternatively, just grab the source and add the ZipAhoy class library to your project. 

### License
ZipAhoy is released under the MIT license. See LICENSE.md for details.
