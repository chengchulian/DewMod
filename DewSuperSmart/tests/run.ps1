$ErrorActionPreference = 'Stop'
$utf8 = [System.Text.UTF8Encoding]::new($false)
[Console]::InputEncoding = $utf8
[Console]::OutputEncoding = $utf8
$OutputEncoding = $utf8

# 直接编译链接的生产源码，生成文件留在测试目录的 bin/obj 中。
$msbuild = (Get-Command 'MSBuild.exe' -ErrorAction Stop).Source
$projectPath = Join-Path $PSScriptRoot 'RegressionHarness.csproj'
$buildArguments = @($projectPath, '/t:Rebuild', '/p:Configuration=Release', '/v:minimal')
& $msbuild @buildArguments
if ($LASTEXITCODE -ne 0) {
    throw "回归测试编译失败：$LASTEXITCODE"
}

$testExecutable = Join-Path $PSScriptRoot 'bin/Release/DewSuperSmart.RegressionTests.exe'
& $testExecutable
if ($LASTEXITCODE -ne 0) {
    throw "回归测试失败：$LASTEXITCODE"
}
