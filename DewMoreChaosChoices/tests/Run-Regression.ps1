param(
    [string]$MSBuildPath = '',
    [string]$HarmonyCachePath = (Join-Path $env:TEMP 'DewMoreChaosChoices-tests\Harmony-2.4.3')
)

$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)

# 游戏使用 Unity Mono；独立检查使用支持本机 .NET 10 的 Harmony，正式 Mod 仍引用游戏版本。
$harmonyAssembly = Join-Path $HarmonyCachePath '0Harmony.net10.dll'
if (-not (Test-Path -LiteralPath $harmonyAssembly)) {
    [System.IO.Directory]::CreateDirectory($HarmonyCachePath) | Out-Null
    $packagePath = Join-Path $HarmonyCachePath 'lib.harmony.2.4.3.nupkg'
    Invoke-WebRequest -Uri 'https://api.nuget.org/v3-flatcontainer/lib.harmony/2.4.3/lib.harmony.2.4.3.nupkg' -OutFile $packagePath
    $archive = [System.IO.Compression.ZipFile]::OpenRead($packagePath)
    try {
        $entry = $archive.GetEntry('lib/net10.0/0Harmony.dll')
        if ($null -eq $entry) { throw 'Harmony net10.0 assembly missing from package.' }
        [System.IO.Compression.ZipFileExtensions]::ExtractToFile($entry, $harmonyAssembly, $true)
    }
    finally {
        $archive.Dispose()
    }
}

# 优先使用显式路径，其次通过 Visual Studio 安装器发现构建工具。
if ([string]::IsNullOrWhiteSpace($MSBuildPath)) {
    $vswherePath = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
    $MSBuildPath = & $vswherePath '-latest' '-products' '*' '-requires' 'Microsoft.Component.MSBuild' '-find' 'MSBuild\**\Bin\amd64\MSBuild.exe' | Select-Object -First 1
}
if (-not (Test-Path -LiteralPath $MSBuildPath)) { throw 'MSBuild executable not found.' }

$buildArgs = @(
    (Join-Path $PSScriptRoot 'RegressionHarness.csproj')
    '/t:Build'
    '/p:Configuration=Release'
    '/p:Platform=AnyCPU'
    '/v:minimal'
    '/nologo'
)
& $MSBuildPath @buildArgs
if ($LASTEXITCODE -ne 0) { throw "Regression build failed: $LASTEXITCODE" }

# 仅测试输出替换运行时依赖，正式构建和游戏目录均保留原版 Harmony。
Copy-Item -LiteralPath $harmonyAssembly -Destination (Join-Path $PSScriptRoot 'bin\Release\0Harmony.dll') -Force -ErrorAction Stop

$runArgs = @(
    'exec'
    '--runtimeconfig'
    (Join-Path $PSScriptRoot 'RegressionHarness.runtimeconfig.json')
    (Join-Path $PSScriptRoot 'bin\Release\DewMoreChaosChoices.RegressionTests.exe')
)
& dotnet @runArgs
if ($LASTEXITCODE -ne 0) { throw "Regression checks failed: $LASTEXITCODE" }
