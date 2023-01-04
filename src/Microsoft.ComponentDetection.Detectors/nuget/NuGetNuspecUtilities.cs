using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Microsoft.ComponentDetection.Contracts;
using NuGet.Packaging;

namespace Microsoft.ComponentDetection.Detectors.NuGet
{
    public static class NuGetNuspecUtilities
    {
        // An empty zip archive file is 22 bytes long which is minumum possible for a zip archive file.
        // source: https://en.wikipedia.org/wiki/Zip_(file_format)#Limits
        public const int MinimumLengthForZipArchive = 22;

        public static (string Name, string Version, string[] Authors, HashSet<string> TargetFrameworks) GetNuGetPackageDataFromNupkg(IComponentStream nupkgComponentStream)
        {
            try
            {
                if (nupkgComponentStream.Stream.Length < MinimumLengthForZipArchive)
                {
                    throw new ArgumentException("nupkg is too small");
                }

                string packageName = null;
                string packageVersion = null;
                string[] authors = null;
                HashSet<string> targetFrameworks = null;

                using var archive = new ZipArchive(nupkgComponentStream.Stream, ZipArchiveMode.Read, true);
                var nuspecFound = false;
                foreach (var entry in archive.Entries)
                {
                    if (entry.Name.EndsWith(".nuspec", StringComparison.CurrentCulture) && entry.FullName.IndexOf('/') == -1)
                    {
                        // if we've already found an entry that matches this condition, ignore it.
                        // this matches the semantics of previous use of .FirstOrDefault()
                        if (nuspecFound)
                        {
                            continue;
                        }

                        // note that we've found the nuspec
                        nuspecFound = true;

                        // this is the nuspec file, process it to get the data we need
                        var nuSpecStream = entry.Open();
                        (packageName, packageVersion, authors, var nuspecFrameworks) = GetNuspecDataFromNuspecStream(nuSpecStream);

                        // add any frameworks found in the nuspec
                        if (targetFrameworks is null)
                        {
                            // It should be common to come across the .nuspec before processing other entries,
                            // so opportunistically use the hashset we just got
                            targetFrameworks = nuspecFrameworks;
                        }
                        else
                        {
                            // add the frameworks to what we already had
                            targetFrameworks.AddRange(nuspecFrameworks);
                        }
                    }
                    else
                    {
                        // process any other entries to get framework names according to the rules of NuGet
                        var frameworkName = FrameworkNameUtility.ParseFrameworkNameFromFilePath(entry.FullName, out _);
                        if (frameworkName == null)
                        {
                            continue;
                        }

                        targetFrameworks ??= new HashSet<string>();

                        targetFrameworks.Add(frameworkName.FullName);
                    }
                }

                if (!nuspecFound)
                {
                    throw new FileNotFoundException("No nuspec file was found");
                }

                // TODO: what if we couldn't find the data in nuspec, or some other error. Need to match previous behavior
                return (packageName, packageVersion, authors, targetFrameworks);
            }
            catch (InvalidDataException ex)
            {
                throw ex;
            }
            finally
            {
                // make sure that no matter what we put the stream back to the beginning
                nupkgComponentStream.Stream.Seek(0, SeekOrigin.Begin);
            }
        }

        public static (string Name, string Version, string[] Authors, HashSet<string> TargetFrameworks) GetNuspecDataFromNuspecStream(Stream sourceStream)
        {
            // The stream we get here may not support the operations we need
            // copy it to a more flexible MemoryStream
            // TODO: detect whether this is necessary
            using var nuspecStream = new MemoryStream();
            sourceStream.CopyTo(nuspecStream);
            nuspecStream.Position = 0;

            if (nuspecStream.Length == 0)
            {
                throw new ArgumentException("The provided stream was empty.", nameof(sourceStream));
            }

            var reader = new NuspecReader(nuspecStream);

            var authors = reader.GetAuthors().Split(",").Select(author => author.Trim()).ToArray();

            var targetFrameworks = new HashSet<string>();

            foreach (var dependencyGroup in reader.GetDependencyGroups())
            {
                if (dependencyGroup.TargetFramework != null)
                {
                    targetFrameworks.Add(dependencyGroup.TargetFramework.DotNetFrameworkName);
                }
            }

            return (reader.GetId(), reader.GetVersion().OriginalVersion, authors, targetFrameworks);
        }

        public static (string Name, string Version, string[] Authors, HashSet<string> TargetFrameworks) GetNuGetPackageDataFromNuspec(IComponentStream nuspecComponentStream)
        {
            var (name, version, authors, targetFrameworks) = GetNuspecDataFromNuspecStream(nuspecComponentStream.Stream);

            // since we found a loose .nuspec file, attempt to interpret the surrounding directory as an unzipped NuGet package
            var unzippedPackageLocation = Path.GetFullPath(Path.GetDirectoryName(nuspecComponentStream.Location)) + "\\";
            foreach (var path in Directory.EnumerateFiles(unzippedPackageLocation, "*", new EnumerationOptions { RecurseSubdirectories = true }))
            {
                // Ignore any paths that somehow don't start with the unzipped location.
                // This will make the operation below safe
                if (!path.StartsWith(unzippedPackageLocation, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                // get relative path
                var relativePath = path[unzippedPackageLocation.Length..];

                // process this according to the rules of packages
                var frameworkName = FrameworkNameUtility.ParseFrameworkNameFromFilePath(relativePath, out _);
                if (frameworkName != null)
                {
                    targetFrameworks.Add(frameworkName.FullName);
                }
            }

            return (name, version, authors, targetFrameworks);
        }
    }
}
