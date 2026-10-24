# 打安装包：dotnet publish + vpk pack（Velopack），产物在 dist\。
# 产物：VigilWattApp-win-Setup.exe（仅当前用户安装、无 UAC、自动建开始菜单+桌面快捷方式）
#       releases.win.json + RELEASES + *.nupkg（Velopack 更新清单与全量/增量包）
# 用法：powershell -ExecutionPolicy Bypass -File scripts\build-installer.ps1 [-Version 1.0.1] [-FrameworkDependent]
#   -FrameworkDependent：精简版，不打包 .NET 与 Windows App SDK 运行时，Setup 约 22 MB；
#   目标机必须已有 .NET 8 Desktop Runtime 与 Windows App Runtime 1.7，产物在 dist-lite\。
param(
    [string]$Version = "",
    [switch]$FrameworkDependent
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot   # scripts\ 的上一级 = 仓库根

# 1) 应用运行时锁 DLL，先杀掉（见 docs/dev/build.md 第 3 条）
cmd /c "taskkill /IM VigilWatt.App.exe /F >nul 2>&1"

# 2) 发布：默认 self-contained（含 .NET 与 Windows App SDK，目标机零依赖）；
#    -FrameworkDependent 则两者都不打包，体积约减半，但目标机必须先装运行时。
$publishDir = Join-Path $root $(if ($FrameworkDependent) { "publish-lite" } else { "publish" })
$csproj = Join-Path $root "src\VigilWatt.App\VigilWatt.App.csproj"
# 发布目录必须清空：从完整版切到精简版时，上一次留下的原生 DLL 会被一起打进包，
# 体积看起来没降，却已经不再需要那些运行时文件。
if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }
$publishArgs = @($csproj, "-c", "Release", "-p:Platform=x64", "-o", $publishDir)
if ($FrameworkDependent) {
    $publishArgs += @("-p:SelfContained=false", "-p:WindowsAppSDKSelfContained=false")
}
dotnet publish @publishArgs
if ($LASTEXITCODE -ne 0) { throw "dotnet publish 失败" }
if ($FrameworkDependent) {
    Write-Host "==> 精简版：目标机需 .NET 8 Desktop Runtime + Windows App Runtime 1.7"
}

# 3) 版本号：默认从发布 exe 的 FileVersion 读（单一来源是 csproj 的 <Version>）。
#    FileVersion 是 4 段（1.0.0.0），vpk 只收 3 段 SemVer2，截掉第 4 段。
$exe = Join-Path $publishDir "VigilWatt.App.exe"
if ($Version -eq "") { $Version = (Get-Item $exe).VersionInfo.FileVersion }
$parts = $Version.Split('.')
if ($parts.Length -gt 3) { $Version = ($parts[0..2] -join '.') }
Write-Host "==> 打包版本: $Version"

# 4) 更新说明：截 docs\project\release-notes.md 里当前版本那一段（格式契约见该文件头）。
#    缺这一段只警告不中断——应用内会显示「本版本没有更新说明」，比打不出包好。
. (Join-Path $PSScriptRoot "get-release-notes.ps1")
$notesFile = Join-Path ([System.IO.Path]::GetTempPath()) "vigilwatt-release-notes-$Version.md"
$notes = Get-ReleaseNotes -Root $root -Version $Version
if ($notes) {
    Write-ReleaseNotesFile -Text $notes -Path $notesFile | Out-Null
    Write-Host "==> 更新说明: release-notes.md 的 $Version 段"
}
else {
    Write-Warning "release-notes.md 里没有 $Version 段，本次打包不带更新说明"
}

# 5) Velopack 打包。packId 必须用 VigilWattApp 而不是 VigilWatt：
#    Velopack 默认装到 %LocalAppData%\<packId>；若也叫 VigilWatt，安装根目录会
#    和旧数据目录混淆，卸载时容易连 data\（settings/history/logs）一起清掉。
$dist = Join-Path $root $(if ($FrameworkDependent) { "dist-lite" } else { "dist" })
# 精简版单独目录、每次清空：避免旧同版本包让 vpk 报 duplicate，也避免误混入完整版资产。
if ($FrameworkDependent -and (Test-Path $dist)) { Remove-Item $dist -Recurse -Force }
$vpkArgs = @(
    "pack"
    "--packId", "VigilWattApp"
    "--packTitle", "VigilWatt 无线设备电量监控"
    "--packVersion", $Version
    "--packDir", $publishDir
    "--mainExe", "VigilWatt.App.exe"
    "--icon", (Join-Path $root "src\VigilWatt.App\Assets\VigilWatt.ico")
    "--runtime", "win-x64"
    "--outputDir", $dist
)
if ($notes) { $vpkArgs += @("--releaseNotes", $notesFile) }
vpk @vpkArgs
if ($LASTEXITCODE -ne 0) { throw "vpk pack 失败" }

$feed = Join-Path $dist "releases.win.json"
if (-not (Test-Path -LiteralPath $feed)) { throw "vpk pack 未生成 releases.win.json 更新清单" }

$setup = Get-ChildItem $dist -Filter "*-win-Setup.exe" | Select-Object -First 1
Write-Host "==> 完成。安装包: $($setup.FullName)"
Write-Host "    更新产物需整体上传到 GitHub Release（推 v* 标签时 .github/workflows/release.yml 会自动上传）"
