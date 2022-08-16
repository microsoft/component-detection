using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.ComponentDetection.Detectors.NuGet
{
    public static class NuGetNuspecUtilities
    {
        // An empty zip archive file is 22 bytes long which is minumum possible for a zip archive file.
        // source: https://en.wikipedia.org/wiki/Zip_(file_format)#Limits
        public const int MinimumLengthForZipArchive = 22;

        public static async Task<byte[]> GetNuspecBytesAsync(Stream nupkgStream)
        {
            try
            {
                if (nupkgStream.Length < MinimumLengthForZipArchive)
                {
                    throw new ArgumentException("nupkg is too small");
                }

                using var archive = new ZipArchive(nupkgStream, ZipArchiveMode.Read, true);

                // get the first entry ending in .nuspec at the root of the package
                var nuspecEntry =
                    archive.Entries.FirstOrDefault(x =>
                        x.Name.EndsWith(".nuspec", StringComparison.OrdinalIgnoreCase)
                        && x.FullName.IndexOf('/') == -1);

                if (nuspecEntry == null)
                {
                    throw new FileNotFoundException("No nuspec file was found");
                }

                using var nuspecStream = nuspecEntry.Open();

                return await GetNuspecBytesFromNuspecStream(nuspecStream, nuspecEntry.Length);
            }
            catch (InvalidDataException ex)
            {
                throw ex;
            }
            finally
            {
                // make sure that no matter what we put the stream back to the beginning
                nupkgStream.Seek(0, SeekOrigin.Begin);
            }
        }

        public static async Task<byte[]> GetNuspecBytesFromNuspecStream(Stream nuspecStream, long nuspecLength)
        {
            var nuspecBytes = new byte[nuspecLength];
            var bytesReadSoFar = 0;
            while (bytesReadSoFar < nuspecBytes.Length)
            {
                bytesReadSoFar += await nuspecStream.ReadAsync(nuspecBytes, bytesReadSoFar, nuspecBytes.Length - bytesReadSoFar);
            }

            return nuspecBytes;
        }
    }
}
