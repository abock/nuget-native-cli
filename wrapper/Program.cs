//
// Author:
//   Aaron Bockover <abock@microsoft.com>
//
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.IO;

static class Program
{
    public static int Main(string[] args)
    {
        var osCertsFile = "/etc/ssl/certs/ca-certificates.crt";
        if (File.Exists(osCertsFile))
        {
            var sync = new CertSync(osCertsFile);
            foreach (var result in sync.ImportCertificates())
            {
                if (result.Added.Count > 0 || result.Removed.Count > 0)
                    Console.WriteLine("Mono certificate store updated [{0}]: added {1}, removed {2}",
                        result.StoreId,
                        result.Added.Count,
                        result.Removed.Count);
            }
        }

        return NuGet.CommandLine.Program.Main(args);
    }
}