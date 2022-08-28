namespace Microsoft.ComponentDetection.Common
{
    using System;
    using System.IO;
    using Microsoft.ComponentDetection.Contracts;

    public class LazyComponentStream : IComponentStream
    {
        private readonly FileInfo fileInfo;
        private readonly Lazy<byte[]> fileBuffer;
        private readonly ILogger logger;

        public LazyComponentStream(FileInfo fileInfo, string pattern, ILogger logger)
        {
            this.Pattern = pattern;
            this.Location = fileInfo.FullName;
            this.fileInfo = fileInfo;
            this.logger = logger;
            this.fileBuffer = new Lazy<byte[]>(this.SafeOpenFile);
        }

        public Stream Stream => new MemoryStream(this.fileBuffer.Value);

        public string Pattern { get; set; }

        public string Location { get; set; }

        private byte[] SafeOpenFile()
        {
            try
            {
                using var fs = this.fileInfo.OpenRead();

                var buffer = new byte[this.fileInfo.Length];
                fs.Read(buffer, 0, (int)this.fileInfo.Length);

                return buffer;
            }
            catch (UnauthorizedAccessException)
            {
                this.logger?.LogWarning($"Unauthorized access exception caught when trying to open {this.fileInfo.FullName}");
            }
            catch (Exception e)
            {
                this.logger?.LogWarning($"Unhandled exception caught when trying to open {this.fileInfo.FullName}");
                this.logger?.LogException(e, isError: false);
            }

            return new byte[0];
        }
    }
}
