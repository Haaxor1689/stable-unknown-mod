[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw 'dotnet SDK is required to build the release.'
}

if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
    throw 'GitHub CLI is required. Install it from https://cli.github.com/ and run gh auth login.'
}

if (-not (Get-Command git -ErrorAction SilentlyContinue)) {
    throw 'git is required to verify the working tree is clean.'
}

$gitStatus = git status --porcelain
if ($LASTEXITCODE -ne 0) {
    throw 'Failed to check git status.'
}
if (-not [string]::IsNullOrWhiteSpace($gitStatus)) {
    throw 'Working tree has uncommitted changes. Commit or stash them before releasing.'
}

$projectRoot = $PSScriptRoot
$metadataPath = Join-Path $projectRoot 'Metadata.json'
$metadata = Get-Content -Raw -Path $metadataPath | ConvertFrom-Json
$version = $metadata.version

if ([string]::IsNullOrWhiteSpace($version)) {
    throw 'Metadata.json does not contain a version.'
}

$tag = "v$version"
$archive = Join-Path $projectRoot 'bin\ReleasePack\net10.0\stable-unknown-mod.zip'

foreach ($artifactDir in @((Join-Path $projectRoot 'bin'), (Join-Path $projectRoot 'obj'))) {
    if (Test-Path -LiteralPath $artifactDir) {
        Remove-Item -LiteralPath $artifactDir -Recurse -Force
    }
}

Push-Location $projectRoot
try {
    dotnet restore . --configfile NuGet.config
    if ($LASTEXITCODE -ne 0) {
        throw 'Dependency restore failed.'
    }

    dotnet publish . -c ReleasePack --no-restore -v:minimal -tl:off
    if ($LASTEXITCODE -ne 0) {
        throw 'Release build failed. Ensure Allumeria Demo is installed or set ALLUMERIA_GAME_DIR.'
    }

    if (-not (Test-Path -LiteralPath $archive)) {
        throw "Release archive was not created: $archive"
    }

    # gh writes to stderr when the release is missing; keep that from becoming a terminating error.
    $previousErrorAction = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        gh release view $tag 2>&1 | Out-Null
    }
    finally {
        $ErrorActionPreference = $previousErrorAction
    }
    if ($LASTEXITCODE -eq 0) {
        throw "GitHub release $tag already exists. Increase the version in Metadata.json before publishing."
    }

    gh release create $tag $archive --title "Stable Unknown $version" --generate-notes
    if ($LASTEXITCODE -ne 0) {
        throw 'GitHub release creation failed.'
    }
}
finally {
    Pop-Location
}