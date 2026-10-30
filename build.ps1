param(
 [string]$Output = '轻贴.exe',
 [ValidateSet('Release','Debug')][string]$Configuration = 'Release'
)
$ErrorActionPreference = 'Stop'
Push-Location $PSScriptRoot
try {
 $compiler = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
 & $compiler /nologo /target:exe /out:IconBuilder.exe /reference:System.Drawing.dll IconBuilder.cs
 if ($LASTEXITCODE -ne 0) { throw '图标工具编译失败' }
 & .\IconBuilder.exe
 if ($LASTEXITCODE -ne 0) { throw '图标生成失败' }
 $options = @('/optimize+', '/debug-')
 if ($Configuration -eq 'Debug') { $options += '/define:DEBUG' }
 & $compiler /nologo /target:winexe @options "/out:$Output" /win32manifest:app.manifest /win32icon:assets/qingtie.ico /resource:assets/qingtie.ico,QingTie.Icon /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Web.Extensions.dll QingTie.cs VersionInfo.cs
 if ($LASTEXITCODE -ne 0) { throw '编译失败' }
 Remove-Item -LiteralPath .\IconBuilder.exe
} finally { Pop-Location }
