using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace NuGet.NetFxStubs.NuGet.CommandLine;

public static class ExtensionLocator
{
    // A fixed version of FindAll that uses AppContext.BaseDirectory to work in a single file deployment scenario
    // https://github.com/NuGet/NuGet.Client/blob/release-5.7.x/src/NuGet.Clients/NuGet.CommandLine/ExtensionLocator.cs#L76
    // https://github.com/NuGet/NuGet.Client/blob/release-6.2.x/src/NuGet.Clients/NuGet.CommandLine/ExtensionLocator.cs#L76
    public static IEnumerable<string> FindAll(
        string globalRootDirectory,
        IEnumerable<string> customPaths,
        string assemblyPattern,
        string nugetDirectoryAssemblyPattern)
    {
        var directories = new List<string>();

        // Add all directories from the environment variable if available.
        directories.AddRange(customPaths);

        // add the global root
        directories.Add(globalRootDirectory);

        var paths = new List<string>();
        foreach (var directory in directories.Where(Directory.Exists))
        {
            paths.AddRange(Directory.EnumerateFiles(directory, assemblyPattern, SearchOption.AllDirectories));
        }

        // Add the nuget.exe directory, but be more careful since it contains non-extension assemblies.
        // Ideally we want to look for all files. However, using MEF to identify imports results in assemblies
        // being loaded and locked by our App Domain which could be slow, might affect people's build systems
        // and among other things breaks our build.
        // Consequently, we'll use a convention - only binaries ending in the name Extensions would be loaded.
        var nugetDirectory = AppContext.BaseDirectory;
        if (nugetDirectory == null)
        {
            return paths;
        }

        paths.AddRange(Directory.EnumerateFiles(nugetDirectory, nugetDirectoryAssemblyPattern));

        return paths;
    }
}
