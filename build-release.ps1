$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
Push-Location $PSScriptRoot
try {
    $versionMatch = [regex]::Match((Get-Content -Raw -LiteralPath 'VersionInfo.cs'), 'AssemblyInformationalVersion\("([0-9]+\.[0-9]+\.[0-9]+)"\)')
    if (-not $versionMatch.Success) { throw '无法读取发布版本' }
    $version = $versionMatch.Groups[1].Value
    $name = "QingTie-$version"
    $releaseRoot = Join-Path $PSScriptRoot 'releases'
    $packageRoot = Join-Path $releaseRoot $name
    $zipPath = Join-Path $releaseRoot "$name-windows.zip"
    $hashPath = Join-Path $releaseRoot "SHA256SUMS-$version.txt"
    if ((Test-Path -LiteralPath $packageRoot) -or (Test-Path -LiteralPath $zipPath)) {
        throw '该版本发布文件已存在。请先调整版本号，或手动另存已有发布文件。'
    }
    $work = Join-Path $PSScriptRoot ('artifacts\release-' + [guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $work -Force | Out-Null
    $debugExe = Join-Path $work 'QingTie.Debug.exe'
    & .\build.ps1 -Configuration Debug -Output $debugExe
    $debugProcess = Start-Process -FilePath $debugExe -ArgumentList '--smoke-test' -PassThru
    if (-not $debugProcess.WaitForExit(60000)) { $debugProcess.Kill(); throw '开发版功能检查超时' }
    if ($debugProcess.ExitCode -ne 0) { throw (Get-Content -Raw -LiteralPath (Join-Path $work '.test-data\error.txt')) }
    Write-Output '开发版功能检查通过。'

    $shippingExe = Join-Path $work '轻贴.exe'
    & .\build.ps1 -Configuration Release -Output $shippingExe
    $compiler = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
    $checkerExe = Join-Path $work 'ReleaseCheck.exe'
    $checkerSource = Join-Path $PSScriptRoot 'tools\ReleaseCheck.cs'
    & $compiler /nologo /target:winexe /optimize+ "/out:$checkerExe" "/reference:$shippingExe" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Web.Extensions.dll $checkerSource
    if ($LASTEXITCODE -ne 0) { throw '正式程序检查工具编译失败' }
    $checkProcess = Start-Process -FilePath $checkerExe -PassThru
    if (-not $checkProcess.WaitForExit(60000)) { $checkProcess.Kill(); throw '正式程序检查超时' }
    if ($checkProcess.ExitCode -ne 0) { throw (Get-Content -Raw -LiteralPath (Join-Path $work 'release-check.txt')) }
    $info = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($shippingExe)
    if ($info.ProductVersion -ne $version) { throw '正式程序版本不匹配' }
    Write-Output '正式程序检查通过。'

    New-Item -ItemType Directory -Path (Join-Path $packageRoot 'assets') -Force | Out-Null
    Copy-Item -LiteralPath $shippingExe -Destination (Join-Path $packageRoot '轻贴.exe')
    foreach ($document in @('README.md','使用说明.md','CHANGELOG.md')) {
        Copy-Item -LiteralPath (Join-Path $PSScriptRoot $document) -Destination (Join-Path $packageRoot $document)
    }
    Copy-Item -LiteralPath 'assets\qingtie.png' -Destination (Join-Path $packageRoot 'assets\qingtie.png')
    Copy-Item -LiteralPath (Join-Path $work '.test-data\preview.png') -Destination (Join-Path $packageRoot 'assets\preview.png')
    Copy-Item -LiteralPath (Join-Path $work '.test-data\preview.png') -Destination 'assets\preview.png' -Force
    Set-Content -LiteralPath (Join-Path $packageRoot 'VERSION') -Value $version -Encoding utf8
    $expected = @('assets/preview.png','assets/qingtie.png','CHANGELOG.md','README.md','VERSION','使用说明.md','轻贴.exe') | Sort-Object
    $actual = @(Get-ChildItem -LiteralPath $packageRoot -File -Recurse | ForEach-Object { $_.FullName.Substring($packageRoot.Length + 1).Replace('\','/') }) | Sort-Object
    if (@(Compare-Object $expected $actual).Count -ne 0) { throw '发布内容清单不匹配' }
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    [System.IO.Compression.ZipFile]::CreateFromDirectory($packageRoot, $zipPath, [System.IO.Compression.CompressionLevel]::Optimal, $true)
    $archive = [System.IO.Compression.ZipFile]::OpenRead($zipPath)
    try {
        $entries = @($archive.Entries | Where-Object { -not $_.FullName.EndsWith('/') } | ForEach-Object { $_.FullName.Substring($name.Length + 1) }) | Sort-Object
        if (@(Compare-Object $expected $entries).Count -ne 0) { throw '压缩包内容清单不匹配' }
    } finally { $archive.Dispose() }
    $zipHash = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()
    $exeHash = (Get-FileHash -LiteralPath (Join-Path $packageRoot '轻贴.exe') -Algorithm SHA256).Hash.ToLowerInvariant()
    @("$zipHash  $name-windows.zip", "$exeHash  $name/轻贴.exe") | Set-Content -LiteralPath $hashPath -Encoding utf8
    @(
        "轻贴 $version 发布检查",
        '日期：2026-10-29',
        '开发版：完整现有功能检查通过。',
        '正式版：独立存储、菜单、悬浮球状态恢复、关闭状态持久保存及图标检查通过。',
        '正式程序不包含 SmokeTest 或开发预览入口。',
        '压缩包仅包含 7 个白名单文件；不含个人便签、测试数据或历史程序。',
        '运行程序与压缩包 SHA-256 已生成。',
        '本次验证仅覆盖当前 Windows 64 位环境，未进行其他机器或 32 位系统实测。',
        '正式程序未进行数字签名。',
        "检查记录：$work"
    ) | Set-Content -LiteralPath (Join-Path $work 'validation-summary.txt') -Encoding utf8
    Copy-Item -LiteralPath 'RELEASE_NOTES.md' -Destination (Join-Path $releaseRoot "$name-发布说明.md")
    Write-Output "发布包：$zipPath"
    Write-Output "校验文件：$hashPath"
} finally { Pop-Location }
