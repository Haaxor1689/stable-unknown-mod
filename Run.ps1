[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw 'dotnet SDK is required to run the mod.'
}

Push-Location $PSScriptRoot
try {
    # -m:1 keeps the target in the entry MSBuild node so the game inherits the real console.
    dotnet build . -t:RunAllumeriaWithMod -m:1 -v:d -tl:off
    if ($LASTEXITCODE -ne 0) {
        throw 'Failed to build or start Allumeria with the mod.'
    }
}
finally {
    Pop-Location
}