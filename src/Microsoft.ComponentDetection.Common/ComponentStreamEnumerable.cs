using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Microsoft.ComponentDetection.Contracts;

namespace Microsoft.ComponentDetection.Common
{
    public class ComponentStreamEnumerable : IEnumerable<IComponentStream>
    {
        private IEnumerable<MatchedFile> ToEnumerate { get; }

        private ILogger Logger { get; }

        public ComponentStreamEnumerable(IEnumerable<MatchedFile> fileEnumerable, ILogger logger)
        {
            ToEnumerate = fileEnumerable;
            Logger = logger;
        }

        public IEnumerator<IComponentStream> GetEnumerator()
        {
            foreach (var filePairing in ToEnumerate)
            {
                if (!filePairing.File.Exists)
                {
                    Logger.LogWarning($"File {filePairing.File.FullName} does not exist on disk.");
                    yield break;
                }

                using var stream = SafeOpenFile(filePairing.File);

                if (stream == null)
                {
                    yield break;
                }

                yield return new ComponentStream { Stream = stream, Pattern = filePairing.Pattern, Location = filePairing.File.FullName };
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        private Stream SafeOpenFile(FileInfo file)
        {
            try
            {
                return file.OpenRead();
            }
            catch (UnauthorizedAccessException)
            {
                Logger.LogWarning($"Unauthorized access exception caught when trying to open {file.FullName}");
                return null;
            }
            catch (Exception e)
            {
                Logger.LogWarning($"Unhandled exception caught when trying to open {file.FullName}");
                Logger.LogException(e, isError: false);
                return null;
            }
        }
    }
}
