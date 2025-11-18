namespace Microsoft.ComponentDetection.Detectors.Rpm;

using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.ComponentDetection.Common;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

/// <summary>
/// Detector for RPM packages from SQLite-format RPM databases.
/// Supports Azure Linux, Fedora 33+, RHEL 9+, and other modern RPM-based distributions.
/// </summary>
public sealed class RpmDbDetector : SystemPackageDetector
{
    public RpmDbDetector(
        IComponentStreamEnumerableFactory componentStreamEnumerableFactory,
        IObservableDirectoryWalkerFactory walkerFactory,
        ILogger<RpmDbDetector> logger
    )
    {
        this.ComponentStreamEnumerableFactory = componentStreamEnumerableFactory;
        this.Scanner = walkerFactory;
        this.Logger = logger;
    }

    /// <inheritdoc />
    public override string Id => "RpmDb";

    /// <inheritdoc />
    public override IEnumerable<string> Categories =>
        [nameof(DetectorClass.SystemPackages), nameof(DetectorClass.Linux)];

    /// <inheritdoc />
    public override IEnumerable<ComponentType> SupportedComponentTypes => [ComponentType.Rpm];

    /// <inheritdoc />
    public override int Version => 1;

    /// <inheritdoc />
    public override IList<string> SearchPatterns => ["rpmdb.sqlite"];

    /// <inheritdoc />
    protected override async Task<List<SystemPackageInfo>> ParsePackagesAsync(
        Stream dbStream,
        string location,
        LinuxDistribution distro
    )
    {
        var packages = new List<SystemPackageInfo>();

        // SQLite requires a file path, so copy the stream to a temporary file
        var tempFile = Path.GetTempFileName();
        try
        {
            await using (var fileStream = File.Create(tempFile))
            {
                await dbStream.CopyToAsync(fileStream).ConfigureAwait(false);
            }

            using var connection = new SqliteConnection($"Data Source={tempFile};Mode=ReadOnly");
            await connection.OpenAsync().ConfigureAwait(false);

            // Modern RPM SQLite databases store package data as BLOBs
            // Schema: Packages(hnum INTEGER PRIMARY KEY, blob BLOB)
            var command = connection.CreateCommand();
            command.CommandText = "SELECT blob FROM Packages";

            using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
            while (await reader.ReadAsync().ConfigureAwait(false))
            {
                // Read the BLOB data
                var blobSize = (int)reader.GetBytes(0, 0, null, 0, 0);
                var blob = ArrayPool<byte>.Shared.Rent(blobSize);

                try
                {
                    reader.GetBytes(0, 0, blob, 0, blobSize);

                    // Parse the RPM header from the BLOB (pass only the actual data, not the entire rented array)
                    var pkgInfo = RpmHeaderParser.ParseHeader(blob.AsSpan(0, blobSize));

                    if (string.IsNullOrEmpty(pkgInfo.Name) || string.IsNullOrEmpty(pkgInfo.Version))
                    {
                        this.Logger.LogDebug("Skipping package with missing name or version");
                        continue;
                    }

                    packages.Add(
                        new SystemPackageInfo
                        {
                            Name = pkgInfo.Name,
                            Version = pkgInfo.Version,
                            Provides =
                                pkgInfo.Provides.Count > 0 ? pkgInfo.Provides : [pkgInfo.Name],
                            Requires = pkgInfo.Requires,
                            Metadata = new RpmMetadata
                            {
                                Epoch = pkgInfo.Epoch,
                                Arch = pkgInfo.Arch,
                                Release = pkgInfo.Release,
                                SourceRpm = pkgInfo.SourceRpm,
                                Vendor = pkgInfo.Vendor,
                            },
                        }
                    );
                }
                catch (Exception ex)
                {
                    this.Logger.LogWarning(ex, "Failed to parse RPM header BLOB, skipping package");
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(blob);
                }
            }

            this.Logger.LogInformation(
                "Parsed {PackageCount} RPM packages from {Location}",
                packages.Count,
                location
            );
        }
        catch (SqliteException ex)
        {
            this.Logger.LogError(ex, "Failed to parse RPM database at {Location}", location);
            throw;
        }
        finally
        {
            try
            {
                if (File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                }
            }
            catch (Exception ex)
            {
                this.Logger.LogTrace(ex, "Failed to delete temporary file {TempFile}", tempFile);
            }
        }

        return packages;
    }

    /// <inheritdoc />
    protected override TypedComponent CreateComponent(
        SystemPackageInfo package,
        LinuxDistribution distro
    )
    {
        var metadata = (RpmMetadata)package.Metadata;

        // Create the RPM component
        var component = new RpmComponent(
            name: package.Name,
            version: package.Version,
            arch: metadata.Arch,
            release: metadata.Release,
            epoch: metadata.Epoch,
            sourceRpm: metadata.SourceRpm,
            vendor: metadata.Vendor,
            provides: [.. package.Provides],
            requires: [.. package.Requires]
        );

        return component;
    }

    private sealed class RpmMetadata
    {
        public int? Epoch { get; init; }

        public string Arch { get; init; }

        public string Release { get; init; }

        public string SourceRpm { get; init; }

        public string Vendor { get; init; }
    }
}
