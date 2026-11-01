param([string]$MsBuildPath = '')

$ErrorActionPreference = 'Stop'
$modRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if ([string]::IsNullOrWhiteSpace($MsBuildPath)) {
    $command = Get-Command 'MSBuild.exe' -ErrorAction SilentlyContinue
    if ($null -ne $command) { $MsBuildPath = $command.Source }
    else {
        $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe'
        if (Test-Path -LiteralPath $vswhere) {
            $candidates = @(& $vswhere -latest -products '*' -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe')
            if ($LASTEXITCODE -ne 0) { throw 'vswhere failed' }
            if ($candidates.Count -gt 0) { $MsBuildPath = $candidates[0] }
        }
    }
}
if (-not (Test-Path -LiteralPath $MsBuildPath -PathType Leaf)) { throw 'Specify -MsBuildPath for the installed MSBuild.exe' }
if ([string]::IsNullOrWhiteSpace($env:ShapeOfDreamsHome)) { throw 'Set ShapeOfDreamsHome to the game directory' }

# 明确按 UTF-8 校验配置、翻译与当前 Build 结构样例。
$encoding = [Text.UTF8Encoding]::new($false, $true)
$jsonFiles = @(Get-ChildItem -LiteralPath $modRoot -Recurse -Filter '*.json' -File | Where-Object { $_.FullName -notmatch '\\(bin|obj)\\' })
foreach ($file in $jsonFiles) {
    $json = $encoding.GetString([IO.File]::ReadAllBytes($file.FullName))
    $null = $json | ConvertFrom-Json
}
# 覆盖游戏 MainLocalization 中的全部语言，并检查翻译字段和格式参数一致。
$supportedLocales = @('de-DE', 'en-US', 'es-MX', 'fr-FR', 'it-IT', 'ja-JP', 'ko-KR', 'pl-PL', 'pt-BR', 'ru-RU', 'tr-TR', 'zh-CN', 'zh-TW')
$localeFiles = @(Get-ChildItem -LiteralPath (Join-Path $modRoot 'i18n') -Filter '*.json' -File | Sort-Object BaseName)
if (Compare-Object $supportedLocales @($localeFiles.BaseName)) { throw 'Supported game locales do not match packaged locales' }
$english = Get-Content -Raw -LiteralPath (Join-Path $modRoot 'i18n/en-US.json') -Encoding utf8 | ConvertFrom-Json -AsHashtable
foreach ($localeFile in $localeFiles) {
    $localized = Get-Content -Raw -LiteralPath $localeFile.FullName -Encoding utf8 | ConvertFrom-Json -AsHashtable
    if (Compare-Object @($english.Keys | Sort-Object) @($localized.Keys | Sort-Object)) { throw ('Locale key mismatch: ' + $localeFile.Name) }
    foreach ($key in $english.Keys) {
        if ($localized[$key] -isnot [string] -or [string]::IsNullOrWhiteSpace($localized[$key])) { throw ('Empty translation: ' + $localeFile.Name + '/' + $key) }
        $referenceArgs = @([regex]::Matches($english[$key], '\{[0-9]+\}') | ForEach-Object Value | Sort-Object -Unique)
        $translatedArgs = @([regex]::Matches($localized[$key], '\{[0-9]+\}') | ForEach-Object Value | Sort-Object -Unique)
        if (($referenceArgs -join ',') -ne ($translatedArgs -join ',')) { throw ('Locale format mismatch: ' + $localeFile.Name + '/' + $key) }
        $null = [string]::Format($localized[$key], [object[]]@('A', 'B', 'C'))
    }
}
Write-Output ('Validated {0} complete locales, {1} keys each.' -f $localeFiles.Count, $english.Count)
foreach ($project in @((Join-Path $modRoot 'DewBuildAssistant.csproj'), (Join-Path $PSScriptRoot 'RegressionHarness.csproj'))) {
    $arguments = @($project, '/t:Build', '/p:Configuration=Release', '/p:Platform=AnyCPU', '/v:minimal', '/restore:false')
    & $MsBuildPath @arguments
    if ($LASTEXITCODE -ne 0) { throw "Build failed: $project" }
}
$testExe = Join-Path $PSScriptRoot 'bin/Release/DewBuildAssistant.RegressionTests.exe'
$fixtureDirectory = Join-Path $PSScriptRoot 'fixtures'
& $testExe $fixtureDirectory
if ($LASTEXITCODE -ne 0) { throw 'Regression tests failed' }
Write-Output ('Validated {0} UTF-8 JSON files.' -f $jsonFiles.Count)
