//
// CertSync.cs: Import the root certificates from a certificate store into Mono
//
// Authors:
//    Sebastien Pouliot <sebastien@ximian.com>
//    Jo Shields <jo.shields@xamarin.com>
//    Aaron Bockover <abock@microsoft.com>
//
// Copyright (C) 2005 Novell, Inc (http://www.novell.com)
// Copyright (C) 2014 Xamarin, Inc (http://www.xamarin.com)
// Copyright (C) Microsoft Corporation. All rights reserved.
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
//
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

using Mono.Security.X509;

sealed class CertSync
{
    public enum CertStoreId
    {
        BltsSystem,
        BltsUser,
        LegacySystem,
        LegacyUser
    }

    public readonly struct ImportResult
    {
        public CertStoreId StoreId { get; }
        public IReadOnlyList<X509Certificate> Added { get; }
        public IReadOnlyList<X509Certificate> Removed { get; }

        public ImportResult(
            CertStoreId storeId,
            IReadOnlyList<X509Certificate> added,
            IReadOnlyList<X509Certificate> removed)
        {
            StoreId = storeId;
            Added = added;
            Removed = removed;
        }
    }

    public delegate void LogHandler(string format, params object[] args);

    public string InputFile { get; }
    public bool ImportToUserStore { get; }

    public LogHandler Log { get; }

    public CertSync(
        string inputFile,
        bool importToUserStore = true,
        LogHandler log = null)
    {
        InputFile = inputFile;
        ImportToUserStore = importToUserStore;
        Log = log ?? ((fmt, args) => { });
    }

    static X509Certificate DecodeCertificate(string s)
        => new X509Certificate(Convert.FromBase64String(s));

    X509CertificateCollection DecodeCollection()
    {
        var roots = new X509CertificateCollection();
        var sb = new StringBuilder();
        var processing = false;

        using var s = File.OpenRead(InputFile);
        var sr = new StreamReader(s);
        while (true)
        {
            var line = sr.ReadLine();
            if (line == null)
                break;

            if (processing)
            {
                if (line.StartsWith("-----END CERTIFICATE-----"))
                {
                    processing = false;
                    roots.Add(DecodeCertificate(sb.ToString()));
                    sb.Clear();
                    continue;
                }
                sb.Append(line);
            }
            else
            {
                processing = line.StartsWith("-----BEGIN CERTIFICATE-----");
            }
        }
        return roots;
    }

    public IReadOnlyList<ImportResult> ImportCertificates()
    {
        var results = new List<ImportResult>();

        var roots = DecodeCollection();
        if (roots == null)
            return results;

        if (roots.Count == 0)
        {
            Log("No certificates were found.");
            return results;
        }

        if (ImportToUserStore)
        {
            Log("Importing into legacy user store:");
            results.Add(ImportToStore(CertStoreId.LegacyUser, roots, X509StoreManager.CurrentUser.TrustedRoot));
            if (Mono.Security.Interface.MonoTlsProviderFactory.IsProviderSupported("btls"))
            {
                Log("");
                Log("Importing into BTLS user store:");
                results.Add(ImportToStore(CertStoreId.BltsUser, roots, X509StoreManager.NewCurrentUser.TrustedRoot));
            }
        }
        else
        {
            Log("Importing into legacy system store:");
            results.Add(ImportToStore(CertStoreId.LegacySystem, roots, X509StoreManager.LocalMachine.TrustedRoot));
            if (Mono.Security.Interface.MonoTlsProviderFactory.IsProviderSupported("btls"))
            {
                Log("");
                Log("Importing into BTLS system store:");
                results.Add(ImportToStore(CertStoreId.BltsSystem, roots, X509StoreManager.NewLocalMachine.TrustedRoot));
            }
        }

        return results;
    }

    ImportResult ImportToStore(CertStoreId storeId, X509CertificateCollection roots, X509Store store)
    {
        var addedResult = new List<X509Certificate>();
        var removedResult = new List<X509Certificate>();

        var trusted = store.Certificates;

        Log("I already trust {0}, your new list has {1}", trusted.Count, roots.Count);
        foreach (var root in roots)
        {
            if (!trusted.Contains(root))
            {
                try
                {
                    store.Import(root);
                    Log("Certificate added: {0}", root.SubjectName);
                    addedResult.Add(root);
                }
                catch (Exception e)
                {
                    Log("Warning: Could not import {0}", root.SubjectName);
                    Log(e.ToString());
                }
            }
        }
        if (addedResult.Count > 0)
            Log("{0} new root certificates were added to your trust store.", addedResult.Count);

        var removed = new X509CertificateCollection();
        foreach (var trust in trusted)
        {
            if (!roots.Contains(trust))
            {
                removed.Add(trust);
                removedResult.Add(trust);
            }
        }
        if (removed.Count > 0)
        {
            Log("{0} previously trusted certificates were removed.", removed.Count);

            foreach (var old in removed)
            {
                store.Remove(old);
                Log("Certificate removed: {0}", old.SubjectName);
            }
        }
        Log("Import process completed.");

        return new ImportResult(storeId, addedResult, removedResult);
    }
}