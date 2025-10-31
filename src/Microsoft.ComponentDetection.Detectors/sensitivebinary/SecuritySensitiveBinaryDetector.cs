namespace Microsoft.ComponentDetection.Detectors.Fcib;

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.Internal;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.Extensions.Logging;
using Microsoft.FileAffordance;

/// <summary>
/// Detects executable files and archives that may contain executables.
/// </summary>
public class SecuritySensitiveBinaryDetector : FileComponentDetector, IDefaultOffComponentDetector
{
    private readonly IFileUtilityService fileUtilityService;

    public SecuritySensitiveBinaryDetector(
        IComponentStreamEnumerableFactory componentStreamEnumerableFactory,
        IObservableDirectoryWalkerFactory walkerFactory,
        IFileUtilityService fileUtilityService,
        ILogger<SecuritySensitiveBinaryDetector> logger)
    {
        this.ComponentStreamEnumerableFactory = componentStreamEnumerableFactory;
        this.Scanner = walkerFactory;
        this.fileUtilityService = fileUtilityService;
        this.Logger = logger;
    }

    public override string Id => "SensitiveBinary";

    public override IList<string> SearchPatterns { get; } =
    [
        "*",
    ];

    public override IEnumerable<string> Categories => ["Binary"];

    public override IEnumerable<ComponentType> SupportedComponentTypes { get; } = [ComponentType.SensitiveBinary];

    public override int Version { get; } = 1;

    protected override async Task OnFileFoundAsync(ProcessRequest processRequest, IDictionary<string, string> detectorArgs, CancellationToken cancellationToken = default)
    {
        var file = processRequest.ComponentStream;
        var singleFileComponentRecorder = processRequest.SingleFileComponentRecorder;
        try
        {
            using var stream = this.fileUtilityService.MakeFileStream(file.Location);

            var buffer = ArrayPool<byte>.Shared.Rent(1024 * 64);
            FileFormat format;

            try
            {
                var fileName = Path.GetFileName(file.Location);
                var extension = Path.GetExtension(file.Location);
                var bytesRead = await stream.ReadAsync(buffer, cancellationToken);
                format = FileFormatDetector.Detect(buffer.AsSpan(0, bytesRead), extension);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }

            if (format.IsLowerRisk())
            {
                this.Logger.LogDebug("Skipping lower risk '{Format}' file: {FilePath}", format, file.Location);
                return;
            }

            var gitBlobSha1 = HashBlobSha1(stream);
            var stableBinaryCorrelatingId = GenerateStableCorrelatingId(stream);

            var relativePath = this.GetRelativePath(file.Location);
            var component = new SensitiveBinaryDetector(relativePath, $"{format}", gitBlobSha1, stableBinaryCorrelatingId);
            var detectedComponent = new DetectedComponent(component);

            singleFileComponentRecorder.RegisterUsage(detectedComponent);

            this.Logger.LogDebug("Detected foreign checked-in binary: {FilePath}", file.Location);
        }
        catch (Exception e)
        {
            this.Logger.LogError(e, "Error while detecting foreign checked-in binary: {FilePath}", file.Location);
        }
    }

    private static string HashBlobSha1(Stream stream)
    {
        using var blobSha1 = IncrementalHash.CreateHash(HashAlgorithmName.SHA1); // CodeQL [SM02196] SHA-1 use is required to match Git blob hash. Also note that this is not a straight SHA-1 as the input is prefixed with $"blob {length}\0".
        AppendBlobPrefix(stream.Length, blobSha1);

        // NOTE: Large buffer here can help considerably. 64 KB is largest
        // shared pool size that avoids large object heap and there seem to be
        // significantly diminishing returns beyond that size.
        const int bufferSize = 64 * 1024;
        var buffer = ArrayPool<byte>.Shared.Rent(bufferSize);
        try
        {
            while (stream.Read(buffer, 0, buffer.Length) is int bytesRead && bytesRead > 0)
            {
                blobSha1.AppendData(buffer, 0, bytesRead);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }

        return Hex(blobSha1);
    }

    /// <summary>
    /// Append $"blob {streamLength}\0"` in UTF-8 to the given incremental hash.
    /// BlobSha is defined as SHA-1 of data with this prefix.
    /// </summary>
    private static void AppendBlobPrefix(long streamLength, IncrementalHash hash)
    {
        const int lengthOfBlobSpace = 5; // "blob ".Length
        const int maxFormattedLongLength = 19; // long.MaxValue.ToString(CultureInfo.InvariantCulture).Length;
        const int maxBlobPrefixLength = lengthOfBlobSpace + maxFormattedLongLength + 1; // +1 for the null terminator

        Span<byte> blobPrefix = stackalloc byte[maxBlobPrefixLength];
        "blob "u8.CopyTo(blobPrefix);
        var formatted = streamLength.TryFormat(blobPrefix[lengthOfBlobSpace..], out var bytesWritten, provider: CultureInfo.InvariantCulture);
        var blobPrefixLength = lengthOfBlobSpace + bytesWritten + 1; // +1 for the null terminator
        blobPrefix[blobPrefixLength - 1] = 0; // null terminator
        blobPrefix = blobPrefix[..blobPrefixLength];
        hash.AppendData(blobPrefix);
    }

    private static string Hex(IncrementalHash incrementalHash)
    {
        Span<byte> hash = stackalloc byte[incrementalHash.HashLengthInBytes];
        var bytesWritten = incrementalHash.GetCurrentHash(hash);

        return Hex(hash);
    }

    private static string Hex(Span<byte> hash)
    {
        Span<char> hex = stackalloc char[hash.Length * 2];
        for (var i = 0; i < hash.Length; i++)
        {
            var formatted = hash[i].TryFormat(hex.Slice(i * 2, 2), out var charsWritten, "x2", CultureInfo.InvariantCulture);
        }

        return new string(hex);
    }

    public static string GenerateStableCorrelatingId(Stream stream)
    {
        stream.Position = 0;
        Span<byte> header = stackalloc byte[(int)Math.Min(64 * 1024, stream.Length)];
        var bytesRead = stream.Read(header);

        if (FileFormatDetector.Detect(header, extension: null) != FileFormat.WindowsPE)
        {
            return null;
        }

        stream.Position = 0;
        using var peReader = new PEReader(stream);

        if (PdbGuid.TryGetPdbGuidAndAge(peReader, out var id, out var peError))
        {
            return id;
        }

        stream.Position = 0;
        Hashes.TryComputeWindowsEsrpSigningResilientSha256(peReader, out id, out peError);

        return id;
    }

    private string GetRelativePath(string fullPath)
    {
        try
        {
            var sourceDirectory = this.CurrentScanRequest?.SourceDirectory?.FullName;
            if (!string.IsNullOrEmpty(sourceDirectory))
            {
                return Path.GetRelativePath(sourceDirectory, fullPath);
            }
        }
        catch (Exception ex)
        {
            this.Logger.LogWarning(ex, "Failed to compute relative path for: {FullPath}", fullPath);
        }

        return fullPath;
    }
}
