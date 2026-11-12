namespace Microsoft.ComponentDetection.Detectors.Composer.Parsers;

using System;
using System.Text.Json;
using System.Threading;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.ComponentDetection.Detectors.Composer.Contracts;
using Microsoft.Extensions.Logging;

internal class ComposerPlatformParser(JsonSerializerOptions serializerOptions, ILogger logger)
{
    internal void RegisterComponents(string output, ISingleFileComponentRecorder recorder, CancellationToken cancellationToken)
    {
        try
        {
            var inventory = JsonSerializer.Deserialize<ComposerPlatformInventory>(output, serializerOptions);
            if (inventory?.Packages is not { } entries)
            {
                logger.LogWarning("Composer platform inventory did not contain a platform array; no environment components will be reported.");
                return;
            }

            foreach (var entry in entries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (entry is null || string.IsNullOrWhiteSpace(entry.Name) || string.IsNullOrWhiteSpace(entry.Version))
                {
                    logger.LogWarning("Composer platform inventory contained an invalid package identity; the entry is omitted.");
                    continue;
                }

                var packageName = entry.Name;
                if (packageName.Contains('/', StringComparison.Ordinal) ||
                    (packageName is not ("php" or "hhvm" or "composer") &&
                    !packageName.StartsWith("ext-", StringComparison.Ordinal) && !packageName.StartsWith("lib-", StringComparison.Ordinal)))
                {
                    continue;
                }

                if (entry.Description?.Contains("via config.platform", StringComparison.Ordinal) == true)
                {
                    logger.LogDebug("Composer platform entry {Name} is simulated or disabled by config.platform; its configured version is not reported as installed.", packageName);
                    continue;
                }

                recorder.RegisterUsage(
                    new DetectedComponent(new PhpSdkComponent(packageName, entry.Version)),
                    isExplicitReferencedDependency: entry.DirectDependency == true,
                    isDevelopmentDependency: true);
            }
        }
        catch (JsonException exception)
        {
            logger.LogWarning(exception, "Composer platform inventory was not valid JSON; no environment components will be reported.");
        }
    }
}
