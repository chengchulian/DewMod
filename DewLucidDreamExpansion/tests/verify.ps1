param(
    [string]$GameHome = 'D:/appdata/Steam/steamapps/common/Shape of Dreams',
    [string]$InstalledModRoot
)

$ErrorActionPreference = 'Stop'
$modRoot = Split-Path $PSScriptRoot -Parent
$repositoryRoot = Split-Path $modRoot -Parent
$cecilPath = Join-Path $repositoryRoot 'GameSource/asset/AuxiliaryFiles/GameAssemblies/Mono.CecilX.dll'
Add-Type -LiteralPath $cecilPath
$modules = @{}
foreach ($name in @('Dew.Core', 'Dew.UI', 'Mirror')) {
    $path = Join-Path $GameHome "Shape of Dreams_Data/Managed/$name.dll"
    $modules[$name] = [Mono.CecilX.ModuleDefinition]::ReadModule($path)
}
$modModule = [Mono.CecilX.ModuleDefinition]::ReadModule((Join-Path $modRoot 'bin/Release/DewLucidDreamExpansion.dll'))
$assertions = 0

function Assert-Condition([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
    $script:assertions++
}

try {
    # 从编译产物读取 Harmony 特性，核对实际游戏版本的方法与参数名。
    foreach ($type in $modModule.Types) {
        $attribute = $type.CustomAttributes | Where-Object AttributeType -Match 'HarmonyLib.HarmonyPatch' | Select-Object -First 1
        if (-not $attribute -or $attribute.ConstructorArguments.Count -eq 0) { continue }
        $targetType = $attribute.ConstructorArguments[0].Value
        $methodName = $attribute.ConstructorArguments[1].Value
        $arguments = $attribute.ConstructorArguments
        $expectedParameters = $null
        if ($arguments.Count -gt 2) {
            if ($arguments[2].Type.FullName -eq 'HarmonyLib.MethodType') {
                Assert-Condition ($arguments[2].Value -eq 2) 'Expected property setter patch'
                $methodName = 'set_' + $methodName
            }
            else {
                $expectedParameters = @($arguments[2].Value | ForEach-Object { $_.Value.FullName })
            }
        }
        $target = $modules[$targetType.Scope.Name].Types | Where-Object FullName -EQ $targetType.FullName
        Assert-Condition ($null -ne $target) "Missing target type: $targetType"
        $methods = @($target.Methods | Where-Object Name -EQ $methodName)
        if ($null -ne $expectedParameters) {
            $methods = @($methods | Where-Object {
                ($_.Parameters.ParameterType.FullName -join ',') -eq ($expectedParameters -join ',')
            })
        }
        Assert-Condition ($methods.Count -eq 1) "Ambiguous or missing patch target: $targetType::$methodName"
        $original = $methods[0]
        foreach ($patchMethod in $type.Methods | Where-Object Name -In @('Prefix', 'Postfix', 'Finalizer')) {
            foreach ($parameter in $patchMethod.Parameters) {
                if ($parameter.Name.StartsWith('__')) { continue }
                Assert-Condition ($original.Parameters.Name -contains $parameter.Name) "Missing injected argument: $type/$($parameter.Name)"
            }
        }
    }

    # 诅咒只作用于战利品生成，防止再次通过全局属性或初始化钩子压低升级结果。
    foreach ($type in $modModule.Types) {
        foreach ($attribute in $type.CustomAttributes | Where-Object AttributeType -Match 'HarmonyLib.HarmonyPatch') {
            if ($attribute.ConstructorArguments.Count -lt 2) { continue }
            $targetName = $attribute.ConstructorArguments[0].Value.FullName
            $memberName = $attribute.ConstructorArguments[1].Value
            Assert-Condition (-not (($targetName -eq 'SkillTrigger' -and $memberName -eq 'Network_level') -or
                ($targetName -eq 'Gem' -and $memberName -eq 'Network_quality'))) 'Global item value interception would block normal upgrades'
        }
    }
    $preparePatch = $modModule.Types | Where-Object Name -EQ 'PreparePatch'
    $prepareCalls = @($preparePatch.Methods | Where-Object HasBody | ForEach-Object { $_.Body.Instructions })
    Assert-Condition (@($prepareCalls | Where-Object { $_.Operand.FullName -like '*LucidDream_PrimordialCurse*' }).Count -eq 0) 'Curse must not reset initialized or restored items'

    $playerType = $modules['Dew.Core'].Types | Where-Object Name -EQ 'DewPlayer'
    foreach ($name in @('AddGold', 'AddDreamDust', 'EarnGold', 'EarnDreamDust')) {
        $method = @($playerType.Methods | Where-Object Name -EQ $name)
        Assert-Condition ($method.Count -eq 1 -and $method[0].Parameters.Count -eq 1 -and
            $method[0].Parameters[0].Name -eq 'amount' -and $method[0].Parameters[0].ParameterType.FullName -eq 'System.Int32') "Income target changed: $name"
    }
    $identity = $modules['Mirror'].Types | Where-Object FullName -EQ 'Mirror.NetworkIdentity'
    Assert-Condition ($identity.Fields.Name -contains '_assetId') 'Mirror asset ID field changed'
    Assert-Condition ($identity.Methods.Name -contains 'InitializeNetworkBehaviours') 'Mirror network initialization changed'

    $dreams = @($modModule.Types | Where-Object { $_.BaseType.FullName -eq 'LucidDream' })
    Assert-Condition ($dreams.Count -eq 6) 'Expected six new lucid dreams'
    $english = Get-Content -Raw -LiteralPath (Join-Path $modRoot 'i18n/en-US.json') -Encoding utf8 | ConvertFrom-Json -AsHashtable
    $chinese = Get-Content -Raw -LiteralPath (Join-Path $modRoot 'i18n/zh-CN.json') -Encoding utf8 | ConvertFrom-Json -AsHashtable
    Assert-Condition ((($english.Keys | Sort-Object) -join ',') -eq (($chinese.Keys | Sort-Object) -join ',')) 'Locale keys differ'
    $languages = @('de-DE', 'en-US', 'es-MX', 'fr-FR', 'it-IT', 'ja-JP', 'ko-KR',
        'pl-PL', 'pt-BR', 'ru-RU', 'tr-TR', 'zh-CN', 'zh-TW')
    $localeFiles = @(Get-ChildItem -LiteralPath (Join-Path $modRoot 'i18n') -Filter '*.json')
    Assert-Condition ((($localeFiles.BaseName | Sort-Object) -join ',') -eq (($languages | Sort-Object) -join ',')) 'Expected all 13 game languages'
    [xml]$project = Get-Content -LiteralPath (Join-Path $modRoot 'DewLucidDreamExpansion.csproj') -Raw -Encoding utf8
    $contentPaths = @($project.Project.ItemGroup.Content.Include)
    foreach ($language in $languages) {
        $locale = Get-Content -LiteralPath (Join-Path $modRoot "i18n/$language.json") -Raw -Encoding utf8 | ConvertFrom-Json -AsHashtable
        Assert-Condition ((($locale.Keys | Sort-Object) -join ',') -eq (($english.Keys | Sort-Object) -join ',')) "Locale keys differ: $language"
        Assert-Condition ($contentPaths -contains "i18n\$language.json") "Locale missing from project: $language"
        foreach ($key in $english.Keys) {
            Assert-Condition ($locale[$key] -is [string] -and -not [string]::IsNullOrWhiteSpace($locale[$key])) "Empty translation: $language/$key"
        }
        foreach ($key in @('LucidDream_PrimordialCurse_Description', 'LucidDream_FallenStarGod_Description',
            'LucidDream_HeavyFootsteps_Description', 'Limbo_Penalty_Depth7', 'Limbo_Penalty_Depth8', 'Limbo_Penalty_Depth9')) {
            Assert-Condition (([regex]::Matches($locale[$key], '\d+').Value -join ',') -eq
                ([regex]::Matches($english[$key], '\d+').Value -join ',')) "Translated mechanics differ: $language/$key"
        }
    }
    foreach ($dream in $dreams) {
        Assert-Condition ($english.ContainsKey($dream.Name + '_Name') -and $english.ContainsKey($dream.Name + '_Description')) "Missing dream text: $dream"
    }
    # 原生完整窗口通过类型名资源索引发现内容，而非固定的名称列表。
    $window = $modules['Dew.UI'].Types | Where-Object Name -EQ 'UI_Lobby_LucidDreamsWindow'
    $start = $window.Methods | Where-Object Name -EQ 'Start'
    Assert-Condition (@($start.Body.Instructions | Where-Object { $_.Operand.Name -eq 'FindAllByTypeSubstring' }).Count -eq 1) 'Native discovery path changed'
    $depthIcon = $modules['Dew.UI'].Types | Where-Object Name -EQ 'UI_LimboDepthIcon'
    Assert-Condition (@($depthIcon.Methods | Where-Object { $_.Name -eq 'UpdateState' -and $_.Parameters.Count -eq 0 }).Count -eq 1) 'Limbo icon state refresh method changed'

    Add-Type -AssemblyName System.Drawing
    foreach ($spec in @(@('icon.png', 512, 512), @('preview.png', 1024, 1024), @('main.png', 1920, 1080))) {
        $artPath = Join-Path $modRoot ('about/' + $spec[0])
        $art = [Drawing.Bitmap]::new($artPath)
        try {
            Assert-Condition ($art.Width -eq $spec[1] -and $art.Height -eq $spec[2]) "Artwork dimensions differ: $($spec[0])"
        }
        finally { $art.Dispose() }
        Assert-Condition ($contentPaths -contains ('about\' + $spec[0])) "Artwork missing from project: $($spec[0])"
        if ($spec[0] -eq 'preview.png') {
            Assert-Condition ((Get-Item -LiteralPath $artPath).Length -lt 1MB) 'Workshop preview exceeds 1 MB'
        }
    }
    $hashes = @()
    foreach ($dream in $dreams) {
        $iconPath = Join-Path $modRoot "assets/icons/$($dream.Name).png"
        $iconBytes = [IO.File]::ReadAllBytes($iconPath)
        $resource = @($modModule.Resources | Where-Object Name -EQ "DewLucidDreamExpansion.Icons.$($dream.Name).png")
        Assert-Condition ($resource.Count -eq 1) "Missing embedded icon: $dream"
        Assert-Condition ([Convert]::ToBase64String($resource[0].GetResourceData()) -eq [Convert]::ToBase64String($iconBytes)) "Embedded icon differs: $dream"
        $bitmap = [Drawing.Bitmap]::new($iconPath)
        try {
            Assert-Condition ($bitmap.Width -eq 128 -and $bitmap.Height -eq 128) "Icon size changed: $dream"
            $visible = 0; $clear = 0; $soft = 0; $border = 0
            for ($y = 0; $y -lt 128; $y++) {
                for ($x = 0; $x -lt 128; $x++) {
                    $pixel = $bitmap.GetPixel($x, $y)
                    if ($pixel.A -eq 0) { $clear++; continue }
                    $visible++
                    if ($pixel.A -lt 255) { $soft++ }
                    if ($x -eq 0 -or $y -eq 0 -or $x -eq 127 -or $y -eq 127) { $border++ }
                    if ($pixel.R -lt 240 -or $pixel.G -lt 240 -or $pixel.B -lt 240) { throw "Icon should be white: $dream" }
                }
            }
            Assert-Condition ($visible -gt 1500 -and $clear -gt 7000 -and $soft -gt 100) "Blank, solid or aliased icon: $dream"
            Assert-Condition ($border -eq 0) "Icon touches canvas edge: $dream"
        }
        finally { $bitmap.Dispose() }
        $hashes += (Get-FileHash -LiteralPath $iconPath -Algorithm SHA256).Hash
    }
    Assert-Condition (@($hashes | Select-Object -Unique).Count -eq 6) 'Expected six distinct icons'
    foreach ($depth in 7..9) {
        Assert-Condition ($english.ContainsKey("Limbo_Penalty_Depth$depth")) "Missing depth tooltip: $depth"
    }
    $metadata = Get-Content -Raw -LiteralPath (Join-Path $modRoot 'about/metadata.json') -Encoding utf8 | ConvertFrom-Json
    foreach ($glob in $metadata.assemblies) {
        $assemblies = @(Get-ChildItem -Path (Join-Path $modRoot $glob))
        Assert-Condition ($assemblies.Count -eq 1 -and $assemblies[0].Name -eq 'DewLucidDreamExpansion.dll') 'Unexpected packaged assembly'
    }
    if ($InstalledModRoot) {
        $installedMetadata = Get-Content -Raw -LiteralPath (Join-Path $InstalledModRoot 'about/metadata.json') -Encoding utf8 | ConvertFrom-Json
        foreach ($glob in $installedMetadata.assemblies) {
            $installed = @(Get-ChildItem -Path (Join-Path $InstalledModRoot $glob) -File)
            Assert-Condition ($installed.Count -eq 1) 'Installed assembly manifest does not resolve to one DLL'
            Assert-Condition ((Get-FileHash -LiteralPath $installed[0].FullName).Hash -eq
                (Get-FileHash -LiteralPath (Join-Path $modRoot 'bin/Release/DewLucidDreamExpansion.dll')).Hash) 'Installed DLL is stale'
        }
        $installedFiles = @($languages | ForEach-Object { "i18n/$_.json" }) +
            @('about/icon.png', 'about/preview.png', 'about/main.png', 'about/description.txt', 'about/artwork.md')
        foreach ($relativePath in $installedFiles) {
            Assert-Condition ((Get-FileHash -LiteralPath (Join-Path $InstalledModRoot $relativePath)).Hash -eq
                (Get-FileHash -LiteralPath (Join-Path $modRoot $relativePath)).Hash) "Installed content differs: $relativePath"
        }
    }
    $utf8 = [Text.UTF8Encoding]::new($false, $true)
    foreach ($file in Get-ChildItem -LiteralPath $modRoot -Recurse -File | Where-Object {
        $_.FullName -notmatch '[\\/](bin|obj)[\\/]' -and $_.Extension -in @('.cs', '.json', '.csproj', '.txt', '.ps1', '.md')
    }) {
        $null = $utf8.GetString([IO.File]::ReadAllBytes($file.FullName))
        $assertions++
    }
    Write-Output "PASS: $assertions assembly, localization, packaging and UTF-8 checks"
}
finally {
    $modModule.Dispose()
    foreach ($module in $modules.Values) { $module.Dispose() }
}
