# Developing on the Linux Scanner

The Linux scanner uses a custom container image built from [this repository](https://github.com/Microsoft/LinuxScanning) we created to execute [tern](https://github.com/tern-tools/tern) on a target image.

As of 6/26/2020, tern does not officially support scanning images in Windows so by extension, our Linux scanner does not either.

There are 2 options for working on the Linux scanner:
* Set up and work in a virtual Linux environment
* Work with a limited set of images in Windows

## The Linux way (Recommended)

**NOTE**: Docker + WSL2 is not a shortcut for this :( The bind mount is still not support even if it's created in WSL2. (See the Windows way to udnerstand why it's necessary)

1. [Using Hyper-V Quick Create](https://blogs.windows.com/windowsdeveloper/2018/09/17/run-ubuntu-virtual-machines-made-even-easier-with-hyper-v-quick-create/), create a Ubuntu virtual machine. Make sure you give it at least 30gb of storage, docker and the images use a lot of space.
2. Clone Component Detection in your VM
3. Install docker
4. Start developing!

## The Windows way

There are _some_ things the tern scanner can do on Windows.

Limitations around scanning in Windows:

* The image being scanned is a base OS image (eg: Ubuntu, Fedora, CentOS, etc.). That means no node, no python, definitely no custom Dockerfile images.
* Tern cannot attempt to create a bind mount to a folder

How to do it:

1. Install docker
2. Comment out the [OS platform check](https://github.com/microsoft/componentdetection-bcde/blob/d5743af1bda5a8f3b4eb96c08517b05513bf29d2/src/Detectors/linux/LinuxContainerDetector.cs#L42-L48)
3. Retarget the [scanner image](https://github.com/microsoft/componentdetection-bcde/blob/d5743af1bda5a8f3b4eb96c08517b05513bf29d2/src/Detectors/linux/LinuxScanner.cs#L22) to point to `tevoinea/ternwrapper:windows-latest`
    * 3.1 **Alternatively**:
        * Clone [LinuxScanning](https://github.com/microsoft/linuxscanning)
        * Change the [default args](https://github.com/microsoft/LinuxScanning/blob/main/index.js#L6) to the set that doesn't contain the `-b` argument. `-b` means bind which we don't want on Windows
        * Rebuild the image locally
        * Follow step 3 again but point to your local image
4. Remember to only scan base OS images (scan will fail otherwise)
5. Remember to undo step 2 and 3 before PR

