using System;
using System.IO;
using Microsoft.ComponentDetection.Contracts;

namespace Microsoft.ComponentDetection.Common
{
    public class LazyComponentStream : IComponentStream
    {
        private readonly FileInfo fileInfo;
        private readonly Lazy<byte[]> fileBuffer;
        private readonly ILogger logger;

        private byte[] SafeOpenFile()
        {
            try
            {
                using var fs = fileInfo.OpenRead();

                var buffer = new byte[fileInfo.Length];
                fs.Read(buffer, 0, (int)fileInfo.Length);

                return buffer;
            }
            catch (UnauthorizedAccessException)
            {
                logger?.LogWarning($"Unauthorized access exception caught when trying to open {fileInfo.FullName}");
            }
            catch (Exception e)
            {
                logger?.LogWarning($"Unhandled exception caught when trying to open {fileInfo.FullName}");
                logger?.LogException(e, isError: false);
            }

            return new byte[0];
        }

        public LazyComponentStream(FileInfo fileInfo, string pattern, ILogger logger)
        {
            Pattern = pattern;
            Location = fileInfo.FullName;
            this.fileInfo = fileInfo;
            this.logger = logger;
            fileBuffer = new Lazy<byte[]>(SafeOpenFile);
        }

        public Stream Stream => new MemoryStream(fileBuffer.Value);

        public string Pattern { get; set; }

        public string Location { get; set; }
    }
}
