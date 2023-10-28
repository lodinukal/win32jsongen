// <copyright file="Program.cs" company="https://github.com/marlersoft">
// Copyright (c) https://github.com/marlersoft. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using JsonWin32Generator;

internal class Program
{
    private static int Main()
    {
        var args = Environment.GetCommandLineArgs();
        string? outputDir = null;
        var extensions = new List<string>();
        if (args.Length > 1)
        {
            if (args[1].Equals("-help", StringComparison.CurrentCultureIgnoreCase) ||
                args[1].Equals("-?", StringComparison.CurrentCultureIgnoreCase))
            {
                Console.WriteLine("Usage: Generator.exe");
                Console.WriteLine("  [optional] -output:<output directory> - The directory to output the generated files to.");
                Console.WriteLine("  <extension path> - The path of a metadata file to process.");
                return 0;
            }
        }

        foreach (string arg in args[1..])
        {
            if (arg.StartsWith("-", StringComparison.CurrentCultureIgnoreCase))
            {
                // property
                string[] parts = arg.Split(':', 2);
                if (parts.Length != 2)
                {
                    Console.WriteLine("Invalid argument: {0}", arg);
                    return 1;
                }

                string propertyName = parts[0].Substring(1);
                string propertyValue = parts[1];
                switch (propertyName.ToLower(null))
                {
                    case "output":
                        outputDir = propertyValue;

                        // resolve output path
                        if (!Path.IsPathRooted(outputDir))
                        {
                            outputDir = Path.Combine(Directory.GetCurrentDirectory(), outputDir);
                        }

                        // create
                        Directory.CreateDirectory(outputDir);
                        break;
                    default:
                        Console.WriteLine("Invalid argument: {0}", arg);
                        return 1;
                }

                continue;
            }

            // extension
            extensions.Add(arg);
        }

        string? apiDir = outputDir;
        if (apiDir == null)
        {
            string repoDir = JsonWin32Common.FindWin32JsonRepo();
            apiDir = JsonWin32Common.GetAndVerifyWin32JsonApiDir(repoDir);
        }

        CleanDir(apiDir);
        var generateTimer = Stopwatch.StartNew();
        {
            List<ReaderInfo> readers = new();
            List<IDisposable> disposables = new();

            using FileStream metadataFileStream = File.OpenRead(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location!)!, "Windows.Win32.winmd"));
            using PEReader peReader = new PEReader(metadataFileStream);
            var reader = peReader.GetMetadataReader();
            Console.WriteLine("OutputDirectory: {0}", apiDir);
            readers.Add(new(reader, new() { "Windows.Win32.Foundation.Metadata" }));

            foreach (string extension in extensions)
            {
                Console.WriteLine("Processing extension: {0}", extension);
                using FileStream extensionMetadataFileStream = File.OpenRead(extension);
                PEReader extensionPeReader = new(extensionMetadataFileStream);
                disposables.Add(extensionPeReader);
                readers.Add(new(extensionPeReader.GetMetadataReader()));
            }

            Console.WriteLine("Generating for {0} readers", readers.Count);

            JsonGenerator.Generate(readers, apiDir, PatchConfig.CreateApiMap());

            foreach (IDisposable disposable in disposables)
            {
                disposable.Dispose();
            }
        }

        Console.WriteLine("Generation time: {0}", generateTimer.Elapsed);
        return 0;
    }

    private static void CleanDir(string dir)
    {
        if (Directory.Exists(dir))
        {
            foreach (string file in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
            {
                File.Delete(file);
            }
        }
        else
        {
            Directory.CreateDirectory(dir);
        }
    }
}
