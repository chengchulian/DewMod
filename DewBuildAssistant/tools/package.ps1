param([string]$OutputName = '')

$ErrorActionPreference = 'Stop'
$modRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$releaseRoot = [IO.Path]::GetFullPath((Join-Path $modRoot 'bin/Release'))
$assembly = Join-Path $releaseRoot 'DewBuildAssistant.dll'
if (-not (Test-Path -LiteralPath $assembly -PathType Leaf)) { throw 'Build DewBuildAssistant in Release first' }
$metadata = Get-Content -LiteralPath (Join-Path $modRoot 'about/metadata.json') -Raw -Encoding UTF8 | ConvertFrom-Json
if ([string]::IsNullOrWhiteSpace($OutputName)) { $OutputName = 'BD-Assistant-{0}.zip' -f $metadata.modVer }
if ([IO.Path]::GetFileName($OutputName) -ne $OutputName -or -not $OutputName.EndsWith('.zip', [StringComparison]::OrdinalIgnoreCase)) {
    throw 'OutputName must be a ZIP filename'
}
$output = [IO.Path]::GetFullPath((Join-Path $releaseRoot $OutputName))
if (-not $output.StartsWith($releaseRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Unexpected package output path'
}

# ZIP 内沿用 metadata 的 obj/Release 路径，只打包自身 DLL 与运行需要的文案。
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$stream = [IO.File]::Open($output, [IO.FileMode]::Create, [IO.FileAccess]::Write)
try {
    $archive = [IO.Compression.ZipArchive]::new($stream, [IO.Compression.ZipArchiveMode]::Create, $true, [Text.UTF8Encoding]::new($false))
    try {
        $files = @('about/metadata.json', 'about/description.txt', 'about/icon.png', 'about/preview.png')
        $files += @(Get-ChildItem -LiteralPath (Join-Path $modRoot 'i18n') -Filter '*.json' -File | Sort-Object Name | ForEach-Object { 'i18n/' + $_.Name })
        foreach ($relativePath in $files) {
            $null = [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, (Join-Path $modRoot $relativePath), ('DewBuildAssistant/' + $relativePath))
        }
        $null = [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $assembly, 'DewBuildAssistant/obj/Release/DewBuildAssistant.dll')
    }
    finally { $archive.Dispose() }
}
finally { $stream.Dispose() }
Write-Output $output
