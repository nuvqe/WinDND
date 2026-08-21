param(
    [switch]$SkipTests
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

Push-Location $PSScriptRoot
try {
    $utcNow = (Get-Date).ToUniversalTime()
    $year = $utcNow.ToString("yy")
    $date = ("{0}{1}" -f $utcNow.Month.ToString("D2"), $utcNow.Day.ToString("D2")).TrimStart('0')
    $time = ("{0}{1}" -f $utcNow.Hour.ToString("D2"), $utcNow.Minute.ToString("D2")).TrimStart('0')
    if ([string]::IsNullOrEmpty($time)) { $time = "0" }

    $gitHashValue = $null
    $gitSucceeded = $false
    $gitCommand = Get-Command git -ErrorAction SilentlyContinue
    if ($null -ne $gitCommand) {
        $gitHashValue = (& $gitCommand.Source rev-parse --short=10 HEAD 2>$null | Select-Object -First 1)
        $gitSucceeded = $?
    }

    if (-not $gitSucceeded -or [string]::IsNullOrWhiteSpace($gitHashValue)) {
        $gitHash = "no-g"
    } else {
        $gitHash = "g$($gitHashValue.Trim())"
    }

    $version = "$year.$date.$time-$gitHash"
    $msiVersion = "{0}.{1}.{2}" -f `
        ($utcNow.Year - 2000), `
        $utcNow.Month, `
        (($utcNow.Day - 1) * 1440 + $utcNow.Hour * 60 + $utcNow.Minute)

    dotnet restore .\WinDND.slnx
    if ($LASTEXITCODE -ne 0) { throw "WinDND dependency restore failed." }

    if (-not $SkipTests) {
        dotnet test .\tests\WinDND.Core.Tests\WinDND.Core.Tests.csproj -c Release
        if ($LASTEXITCODE -ne 0) { throw "WinDND tests failed." }
    }

    dotnet build .\installer\WinDND.Installer\WinDND.Installer.wixproj -c Release `
        -p:ArtifactVersion=$version -p:MsiVersion=$msiVersion
    if ($LASTEXITCODE -ne 0) { throw "WinDND installer build failed." }

    $packageRoot = Join-Path $PSScriptRoot "artifacts\installer\x64\Release"
    $installerName = "WinDND-$version-x64.msi"
    $installerPath = Join-Path $packageRoot $installerName
    if (-not (Test-Path -LiteralPath $installerPath)) {
        throw "Expected installer was not created at [$installerPath]."
    }

    foreach ($noticeName in @("LICENSE", "NOTICE", "THIRD-PARTY-NOTICES.txt")) {
        Copy-Item -LiteralPath (Join-Path $PSScriptRoot $noticeName) -Destination $packageRoot -Force
    }

    $file = Get-Item -LiteralPath $installerPath
    $sha256 = (Get-FileHash -LiteralPath $installerPath -Algorithm SHA256).Hash.ToLowerInvariant()
    @"
applications:
  windnd:
    version: $version
    packageVersion: $msiVersion
    artifactVersion: $version
    fileName: $installerName
    contentType: application/x-msi
    size: $($file.Length)
    sha256: $sha256
"@ | Set-Content -LiteralPath (Join-Path $packageRoot "versions-windnd.yaml") -Encoding utf8

    Write-Host "WinDND version: $version"
    Write-Host "MSI ProductVersion: $msiVersion"
    Write-Host "Installer: $installerPath"
    Write-Host "Build metadata: $(Join-Path $packageRoot 'versions-windnd.yaml')"
} finally {
    Pop-Location
}
