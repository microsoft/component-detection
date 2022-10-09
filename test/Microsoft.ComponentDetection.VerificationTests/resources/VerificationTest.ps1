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

    Write-Progress "cloning released component-detection at $CDRelease"
    git clone "https://github.com/microsoft/component-detection.git" $CDRelease

    mkdir $output
    mkdir $releaseOutput

    Write-Progress "Running detection....."
    Set-Location (Get-Item  $repoPath).FullName
    dotnet restore
    Set-Location ((Get-Item  $repoPath).FullName + "\src\Microsoft.ComponentDetection")
    dotnet run scan --SourceDirectory $verificationTestRepo --Output $output

    Set-Location $CDRelease
    dotnet restore
    Set-Location ($CDRelease + "\src\Microsoft.ComponentDetection")
    dotnet run scan --SourceDirectory $verificationTestRepo --Output $releaseOutput

    

    $env:GITHUB_OLD_ARTIFACTS_DIR = $releaseOutput
    $env:GITHUB_NEW_ARTIFACTS_DIR = $output
    $env:ALLOWED_TIME_DRIFT_RATIO = "0.75"

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