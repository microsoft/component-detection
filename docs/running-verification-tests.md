# Running Verification Tests On Your Local Machine
Verification tests are used to confirm that no detectors are lost when changes are made to the project. The tests are run on every PR build. They work by comparing the detection results from the main branch to detection results from the new changes on your PR. You can follow the steps below to run them locally.

## Step 1 : Run Detection on the main branch

- Checkout the main branch in your local repo
- Create a folder to store detection results (e.g C:\old-output-folder)
- Use command line to run detection on the main branch with the code below:

```dotnet run scan --Verbosity Verbose --SourceDirectory {path to your local repo} --Output {path to the output folder you created}```

For Example:

```dotnet run scan --Verbosity Verbose --SourceDirectory C:\componentdetection --Output C:\old-output-folder```


## Step 2 : Run Detection on your new branch

- Checkout the branch with the new changes you are trying to merge
- Create a folder to store detection results. This folder should be seperate from the one you used in Step 1 (e.g C:\new-output-folder)
- Use command line to run detection on the main branch with the code below:

```dotnet run scan --Verbosity Verbose --SourceDirectory {path to your local repo} --Output {path to the output folder you created}```

For Example:

```dotnet run scan --Verbosity Verbose --SourceDirectory C:\componentdetection --Output C:\new-output-folder```

## Step 3 : Update variables in the test

- Open the Microsoft.ComponentDetection.VerificationTests project in VS Studio
- Navigate to  `GatherResources()`  in `ComponentDetectionIntegrationTests.cs`
- Update the following variables:
    -  `oldGithubArtifactsDir` : This should be the output folder you created in Step 1
    - `newGithubArtifactsDir` : This should be the output folder you created in Step 2
    - `allowedTimeDriftRatioString`:  This should be ".75"


## Step 4: Run The tests
You can run the tests in two ways:
- Run or Debug the tests in test explorer.
- Use the command line to navigate to the Microsoft.ComponentDetection.VerificationTests folder, and run `dotnet test`.