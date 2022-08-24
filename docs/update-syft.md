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
3. Pull the latest container image

    ```
    $ docker pull docker.io/anchore/syft:v{LATEST}
    v0.53.4: Pulling from anchore/syft
    0d60d5ab2113: Pull complete
    26136f3e3dd3: Pull complete
    497aa7f04842: Pull complete
    Digest: sha256:37e85e8efdeaabb1b6f65c5bc175b664cb05d1aaddd0d922130b8e25d6e49726
    Status: Downloaded newer image for anchore/syft:v{LATEST}
    docker.io/anchore/syft:v{LATEST}
    ```

4. Retag the container image

    ```
    $ docker tag docker.io/anchore/syft:v{LATEST} governancecontainerregistry.azurecr.io/syft:v{LATEST}
    ```
   
5. Push the new image to the registry

    ```
    $ docker push governancecontainerregistry.azurecr.io/syft:v{LATEST}
    The push refers to repository [governancecontainerregistry.azurecr.io/syft]
    9c858c120b14: Pushed
    840f3b941d62: Pushed
    21ce82bb7448: Pushed
    v{LATEST: digest: sha256:04ed9c717a814fdccf52758b67333632a0ff16840fc393f5fba5864285eaebbe size: 945
    ```

6. Update the container reference in [`LinuxScanner`][3]
7. Update [the models][4] that map the Syft output

[1]: https://github.com/anchore/syft
[2]: https://github.com/anchore/syft/releases/latest
[3]: https://github.com/microsoft/component-detection/blob/aaf865e38112fb2448f5866ab06d5898358403f6/src/Microsoft.ComponentDetection.Detectors/linux/LinuxScanner.cs#L20
[4]: https://github.com/microsoft/component-detection/blob/main/src/Microsoft.ComponentDetection.Detectors/linux/Contracts/SyftOutput.cs
