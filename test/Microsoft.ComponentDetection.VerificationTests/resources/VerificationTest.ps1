Set-StrictMode -Version 2.0
$Script:ErrorActionPreference = 'Stop'

function main()
{
    Write-Progress "starting verification tests."
    $repoPath = Get-Location
    $workspace = (Get-Item  $repoPath).Parent.FullName

    # Test data directory is cleaned up before each run, we want to avoid the conflict and accidental removal of any other existing directory. 
    # incorporating uid in testdata directory ensures there is no conflict in directory name. 
    $uidForTestData = "1f8835c2" 
    $testDataDir = $workspace + "\cd-verification-testdata-" + $uidForTestData

    if (Test-Path $testDataDir) {
        Write-Progress "Removing existing test data directory from previous runs at $testDataDir"
        Remove-Item $testDataDir -Force -Recurse
    }
    
    Write-Progress "Creating test data directory at $testDataDir"
    mkdir $testDataDir

    $verificationTestRepo = (Get-Item  $repoPath).FullName + "\test\Microsoft.ComponentDetection.VerificationTests\resources"
    $CDRelease =  $testDataDir + "\component-detection-release"
    $output = $testDataDir + "\output"
    $releaseOutput = $testDataDir + "\release-output"
    $dockerImagesToScan = "docker.io/library/debian@sha256:9b0e3056b8cd8630271825665a0613cc27829d6a24906dc0122b3b4834312f7d,mcr.microsoft.com/cbl-mariner/base/core@sha256:c1bc83a3d385eccbb2f7f7da43a726c697e22a996f693a407c35ac7b4387cd59,docker.io/library/alpine@sha256:1304f174557314a7ed9eddb4eab12fed12cb0cd9809e4c28f29af86979a3c870"

    Write-Progress "cloning released component-detection at $CDRelease"
    git clone "https://github.com/microsoft/component-detection.git" $CDRelease

    mkdir $output
    mkdir $releaseOutput

    $env:PipReportSkipFallbackOnFailure = "true"
    $env:PIP_INDEX_URL="https://pypi.python.org/simple"

    Write-Progress "Running detection....."
    Set-Location (Get-Item  $repoPath).FullName
    dotnet restore
    Set-Location ((Get-Item  $repoPath).FullName + "\src\Microsoft.ComponentDetection")
    dotnet run scan --SourceDirectory $verificationTestRepo --Output $output `
                    --DockerImagesToScan $dockerImagesToScan `
                    --DetectorArgs DockerReference=EnableIfDefaultOff,SPDX22SBOM=EnableIfDefaultOff,CondaLock=EnableIfDefaultOff,ConanLock=EnableIfDefaultOff `
                    --MaxDetectionThreads 5 --DebugTelemetry `
                    --DirectoryExclusionList "**/pip/parallel/**;**/pip/roots/**;**/pip/pre-generated/**"

    Set-Location $CDRelease
    dotnet restore
    Set-Location ($CDRelease + "\src\Microsoft.ComponentDetection")
    dotnet run scan --SourceDirectory $verificationTestRepo --Output $releaseOutput `
                    --DockerImagesToScan $dockerImagesToScan `
                    --DetectorArgs DockerReference=EnableIfDefaultOff,SPDX22SBOM=EnableIfDefaultOff,CondaLock=EnableIfDefaultOff,ConanLock=EnableIfDefaultOff `
                    --MaxDetectionThreads 5 --DebugTelemetry `
                    --DirectoryExclusionList "**/pip/parallel/**;**/pip/roots/**;**/pip/pre-generated/**"

    $env:GITHUB_OLD_ARTIFACTS_DIR = $releaseOutput
    $env:GITHUB_NEW_ARTIFACTS_DIR = $output
    $env:ALLOWED_TIME_DRIFT_RATIO = "0.75"

    if ([string]::IsNullOrEmpty($env:GITHUB_WORKSPACE)) {
        $env:GITHUB_WORKSPACE = $repoPath
        Write-Host "Setting GITHUB_WORKSPACE environment variable to $repoPath"
    }

    Write-Progress "Executing verification tests....."
    Set-Location ((Get-Item  $repoPath).FullName + "\test\Microsoft.ComponentDetection.VerificationTests\")
    dotnet restore  
    dotnet test

    Set-Location $repoPath

    Write-Host "Verification tests were completed. The generated testdata can be found at $testDataDir `n To debug test with visual studio, replace values for following variables in ComponentDetectionIntegrationTests.cs"  -ForegroundColor red -BackgroundColor white
    Write-Host "oldGithubArtifactsDir = @`"$releaseOutput`""  -ForegroundColor red -BackgroundColor white
    Write-Host "newGithubArtifactsDir = @`"$output`""  -ForegroundColor red -BackgroundColor white
    Write-Host "allowedTimeDriftRatioString = "`"0.75`"  -ForegroundColor red -BackgroundColor white
}

main
