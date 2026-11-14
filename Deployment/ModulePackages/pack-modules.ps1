<#
.SYNOPSIS
    Builds the module clients, zips them into module packages and (optionally) uploads and publishes them.

.DESCRIPTION
    1. dotnet build of every src\Modules\*\Client project (stages dll + manifest.json into artifacts\modules\<id>\)
    2. zip artifacts\modules\<id>\* into Deployment\ModulePackages\out\<id>-<version>.zip
    3. -Upload:  sign in at the gateway and PUT the zip to /api/packages/<id>/<version>
                 (the ModuleDelivery service hashes and signs it)
    4. -Publish: POST the manifest to /api/modules (registry; registers permissions, runs the workflow impact
                 analysis and starts a rollout for new versions)

.EXAMPLE
    .\pack-modules.ps1
    .\pack-modules.ps1 -Upload -Password 'GreenHive!2026'
    .\pack-modules.ps1 -Module document-management -Upload -Publish -Password 'GreenHive!2026'
#>
[CmdletBinding()]
param(
    [string] $Configuration = 'Release',
    [ValidateSet('x64', 'x86', 'ARM64')] [string] $Platform = 'x64',
    [string] $Module,
    [string] $Server = 'http://localhost:8080',
    [string] $User = 'admin',
    [string] $Password,
    [switch] $Upload,
    [switch] $Publish,
    [string] $Channel = 'stable'
)

$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$stage = Join-Path $root 'artifacts\modules'
$out = Join-Path $PSScriptRoot 'out'
New-Item -ItemType Directory -Force -Path $out | Out-Null

function Read-Manifest([string] $path) {
    # manifest.json may contain // comments (GreenHiveJson allows them); strip them for ConvertFrom-Json.
    $text = (Get-Content $path -Raw) -replace '(?m)^\s*//.*$', ''
    return $text | ConvertFrom-Json
}

$projects = Get-ChildItem (Join-Path $root 'src\Modules') -Directory | ForEach-Object {
    $csproj = Get-ChildItem (Join-Path $_.FullName 'Client') -Filter *.csproj -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($csproj) {
        [pscustomobject]@{ Folder = $_.FullName; Project = $csproj.FullName; Manifest = Read-Manifest (Join-Path $_.FullName 'manifest.json') }
    }
} | Where-Object { -not $Module -or $_.Manifest.moduleId -eq $Module }

if (-not $projects) { throw "No module found$(if ($Module) { " with id '$Module'" })." }

$token = $null
if ($Upload -or $Publish) {
    if (-not $Password) { $Password = Read-Host "Password for $User" -AsSecureString | ConvertFrom-SecureString -AsPlainText }
    $login = Invoke-RestMethod -Method Post -Uri "$Server/api/identity/login" -ContentType 'application/json' `
        -Body (@{ userName = $User; password = $Password } | ConvertTo-Json)
    $token = $login.accessToken
    Write-Host "Signed in as $($login.user.displayName)" -ForegroundColor DarkGray
}
$headers = @{ Authorization = "Bearer $token" }

foreach ($p in $projects) {
    $id = $p.Manifest.moduleId
    $version = $p.Manifest.version
    Write-Host "== $id $version" -ForegroundColor Cyan

    dotnet build $p.Project -c $Configuration -p:Platform=$Platform --nologo -v minimal
    if ($LASTEXITCODE -ne 0) { throw "Build of $id failed." }

    $files = Join-Path $stage $id
    if (-not (Test-Path (Join-Path $files 'manifest.json'))) { throw "Staged files of $id not found in $files." }

    $zip = Join-Path $out "$id-$version.zip"
    if (Test-Path $zip) { Remove-Item $zip }
    Compress-Archive -Path (Join-Path $files '*') -DestinationPath $zip
    $hash = (Get-FileHash $zip -Algorithm SHA256).Hash.ToLowerInvariant()
    Write-Host "   package  $zip"
    Write-Host "   sha256   $hash"

    if ($Upload) {
        $result = Invoke-RestMethod -Method Put -Uri "$Server/api/packages/$id/$version`?fileName=$id-$version.zip" -Headers $headers `
            -ContentType 'application/zip' -InFile $zip
        Write-Host "   uploaded ($($result.sizeBytes) bytes, signed: $([bool]$result.signature))" -ForegroundColor Green
    }

    if ($Publish) {
        $body = @{ manifest = $p.Manifest; channel = $Channel } | ConvertTo-Json -Depth 32
        $published = Invoke-RestMethod -Method Post -Uri "$Server/api/modules" -Headers $headers -ContentType 'application/json' -Body $body
        Write-Host "   published $($published.moduleId) $($published.version)" -ForegroundColor Green
    }
}

Write-Host "Done. Packages: $out"
