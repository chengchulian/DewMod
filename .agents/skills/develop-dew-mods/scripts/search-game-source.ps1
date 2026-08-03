[CmdletBinding()]
param(
    [Parameter(Mandatory, Position = 0)]
    [ValidateNotNullOrEmpty()]
    [string]$Query,

    [ValidateSet('Code', 'Docs', 'Assets', 'Assemblies', 'Example', 'All')]
    [string]$Scope = 'Code',

    [ValidateRange(1, 1000)]
    [int]$MaxResults = 100,

    [switch]$FilesOnly,

    [switch]$FixedStrings
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent (Split-Path -Parent (Split-Path -Parent (Split-Path -Parent $PSScriptRoot)))
$gameSource = Join-Path $repoRoot 'GameSource'

if (-not (Test-Path -LiteralPath $gameSource -PathType Container)) {
    throw "GameSource directory not found at: $gameSource"
}

$scopePaths = @{
    Code       = @((Join-Path $gameSource 'code\Dewr.1.3.1.3_s'))
    Docs       = @((Join-Path $gameSource 'doc\api'), (Join-Path $gameSource 'doc\md'), (Join-Path $gameSource 'doc\xrefmap.yml'))
    Assets     = @((Join-Path $gameSource 'asset\ExportedProject\Assets'))
    Assemblies = @((Join-Path $gameSource 'asset\AuxiliaryFiles\GameAssemblies'))
    Example    = @((Join-Path $gameSource 'Gem_E_NewGem'))
    All        = @($gameSource)
}

$paths = @($scopePaths[$Scope] | Where-Object { Test-Path -LiteralPath $_ })
if ($paths.Count -eq 0) {
    throw "No searchable paths exist for scope: $Scope"
}

$rgCommand = Get-Command 'rg' -ErrorAction Stop

if ($FilesOnly -or $Scope -eq 'Assemblies') {
    $fileArgs = @('--files', '--color', 'never', '--') + $paths
    $matches = & $rgCommand.Source @fileArgs |
        Where-Object { $_.IndexOf($Query, [System.StringComparison]::OrdinalIgnoreCase) -ge 0 } |
        Select-Object -First $MaxResults
    $exitCode = $LASTEXITCODE

    if ($exitCode -gt 1) {
        throw "rg failed with exit code $exitCode"
    }

    $matches
    exit 0
}

$searchArgs = @('-n', '--no-heading', '--color', 'never')
if ($FixedStrings) {
    $searchArgs += '--fixed-strings'
}

switch ($Scope) {
    'Code' {
        $searchArgs += @('--glob', '*.cs')
    }
    'Docs' {
        $searchArgs += @('--glob', '*.md', '--glob', '*.yml', '--glob', '*.html')
    }
    'Example' {
        $searchArgs += @('--glob', '*.cs', '--glob', '*.md', '--glob', '*.json', '--glob', '*.csproj')
    }
    'All' {
        $searchArgs += @('--glob', '!asset/ExportedProject/Assets/Texture2D/**', '--glob', '!asset/ExportedProject/Assets/AudioClip/**')
    }
}

$searchArgs += @('--', $Query)
$searchArgs += $paths

$results = & $rgCommand.Source @searchArgs | Select-Object -First $MaxResults
$exitCode = $LASTEXITCODE

if ($exitCode -gt 1) {
    throw "rg failed with exit code $exitCode"
}

$results
exit 0
