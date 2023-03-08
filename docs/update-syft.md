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
3. Install [Skopeo][3]
4. Use [`skopeo`][4] to copy the manifest and images to our Azure Container Registry:

    ```
    skopeo copy --all docker://docker.io/anchore/syft:v0.74.0 docker://governancecontainerregistry.azurecr.io/syft:v0.74.0
    ```

5. Update the container reference in [`LinuxScanner`][5]
6. Update [the models][6] that map the Syft output

[1]: https://github.com/anchore/syft
[2]: https://github.com/anchore/syft/releases/latest
[3]: https://github.com/containers/skopeo/blob/main/install.md
[4]: https://github.com/containers/skopeo
[5]: https://github.com/microsoft/component-detection/blob/aaf865e38112fb2448f5866ab06d5898358403f6/src/Microsoft.ComponentDetection.Detectors/linux/LinuxScanner.cs#L20
[6]: https://github.com/microsoft/component-detection/blob/main/src/Microsoft.ComponentDetection.Detectors/linux/Contracts/SyftOutput.cs
