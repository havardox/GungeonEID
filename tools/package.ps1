param(
    [switch]$Install,
    [string]$Profile = "$env:APPDATA\Thunderstore Mod Manager\DataFolder\ETG\profiles\Default"
)
$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent

dotnet build "$root\GungeonEID.csproj" -c Release
if ($LASTEXITCODE -ne 0) { throw "build failed" }

$manifest = Get-Content "$root\package\manifest.json" -Raw | ConvertFrom-Json
$stage = "$root\dist\stage"
if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
New-Item -ItemType Directory -Force $stage | Out-Null
if (-not (Test-Path "$root\package\icon.png")) { python -I "$root\tools\make_icon.py" "$root\package\icon.png" }
Copy-Item "$root\package\manifest.json", "$root\package\icon.png", "$root\README.md" $stage
Copy-Item "$root\bin\Release\GungeonEID.dll", "$root\bin\Release\eid_data.json" $stage

$zip = "$root\dist\$($manifest.name)-$($manifest.version_number).zip"
if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path "$stage\*" -DestinationPath $zip
Write-Host "Package: $zip"

if ($Install) {
    $dest = "$Profile\BepInEx\plugins\GungeonEID"
    New-Item -ItemType Directory -Force $dest | Out-Null
    Copy-Item "$root\bin\Release\GungeonEID.dll", "$root\bin\Release\eid_data.json" $dest -Force
    Write-Host "Installed to $dest"
}
