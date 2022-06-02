//
// Author:
//   Aaron Bockover <abock@microsoft.com>
//
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Linq;

var credentialProviders = new NuGet.CommandLine.ExtensionLocator()
    .FindCredentialProviders()
    .ToList();

var exitCode = NuGet.CommandLine.Program.Main(args);

if (args.Length == 0)
{
    Console.Error.WriteLine();
    Console.Error.WriteLine("Discovered Credential Providers:");
    if (credentialProviders.Count == 0)
    {
        Console.Error.WriteLine("  <none>");
    }
    else
    {
        foreach (var providerPath in credentialProviders)
            Console.Error.WriteLine($"  {providerPath}");
    }
}

return exitCode;
