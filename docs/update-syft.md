# Updating Syft

For container detection we use [Syft][1].
Occasionally, as new versions of Syft are released, we need to update the version we use.
To do this:

1. Ensure you're authenticated to Azure and our Azure Container Registry

    ```
    az login
    az acr login --name governancecontainerregistry
    ```

2. Find the [latest version of Syft][2]
3. Copy the multi-platform image to the registry

    ```
    $ docker buildx imagetools create \
        --tag governancecontainerregistry.azurecr.io/syft:v{LATEST} \
        docker.io/anchore/syft:v{LATEST}
    ```

    This command preserves all platform images and creates a proper multi-platform manifest in the registry.

4. Update the container reference in [`LinuxScanner`][3]
5. Update [the models][4] that map the Syft output

[1]: https://github.com/anchore/syft
[2]: https://github.com/anchore/syft/releases/latest
[3]: https://github.com/microsoft/component-detection/blob/aaf865e38112fb2448f5866ab06d5898358403f6/src/Microsoft.ComponentDetection.Detectors/linux/LinuxScanner.cs#L20
[4]: https://github.com/microsoft/component-detection/blob/main/src/Microsoft.ComponentDetection.Detectors/linux/Contracts/SyftOutput.cs
