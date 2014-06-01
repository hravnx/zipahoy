## Zip Ahoy
Zip Ahoy is a small convenience library for creating/extracting directories to/from zip-files in C#.

### Why
Although there are already high-level helper methods for this in the System.IO.Compression.* namespaces, they lack support for cancellation, and a way to get meaningful progress reporting at a more fine-grained level than per-file.

ZipAhoy adds these things in two simple high-level static methods.

### How to install
Just grab the source, and add the ZipAhoy class library to your project. A nuget package may be forthcoming. If you want to run the tests, you need to install the XUnit test runner Visual Studio extension.

### Examples
There are usage examples in the ZipAhoy.Tests project.

### License
ZipAhoy is under the MIT license. See LICENSE.md for details.