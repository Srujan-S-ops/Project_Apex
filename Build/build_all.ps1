<#
  PROJECT APEX 2.0 — full build.
  1. Generates original assets (Python)       -> UnityGame/Assets/Resources
  2. Builds + tests the Java engine (Maven)    -> JavaEngine/target/apex-engine.jar
  3. Builds the Unity Windows player           -> Build/Windows/ProjectApex.exe
  4. Bundles the engine + a trimmed Java 21 runtime -> Build/Windows/Backend
  Usage:  powershell -ExecutionPolicy Bypass -File Build\build_all.ps1 [-SkipTests] [-SkipAssets] [-SkipUnity]
#>
param([switch]$SkipTests, [switch]$SkipAssets, [switch]$SkipUnity)
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$unity = "C:\Program Files\Unity\Hub\Editor\6000.3.28f1\Editor\Unity.exe"
$mvn = Join-Path $root ".tools\apache-maven-3.9.11\bin\mvn.cmd"
if (-not (Test-Path $mvn)) { $mvn = "mvn" }

if (-not $SkipAssets) {
    Write-Host "== Generating assets" -ForegroundColor Cyan
    python (Join-Path $root "Assets_Source\Tools\generate_assets.py")
    if ($LASTEXITCODE -ne 0) { throw "asset generation failed" }
}

Write-Host "== Building Java engine" -ForegroundColor Cyan
Push-Location (Join-Path $root "JavaEngine")
try {
    if ($SkipTests) { & $mvn -B -q package -DskipTests } else { & $mvn -B package }
    if ($LASTEXITCODE -ne 0) { throw "maven build failed" }
} finally { Pop-Location }

if (-not $SkipUnity) {
    if (-not (Test-Path (Join-Path $root "UnityGame\Assets\TextMesh Pro"))) {
        # AssetDatabase.ImportPackage is asynchronous in batch mode, so import TMP essentials via the command line
        $tmp = (Get-ChildItem (Join-Path $root "UnityGame\Library\PackageCache\com.unity.ugui@*\Package Resources\TMP Essential Resources.unitypackage") | Select-Object -First 1).FullName
        if ($tmp) { & $unity -batchmode -quit -projectPath (Join-Path $root "UnityGame") -importPackage $tmp -logFile (Join-Path $root "Build\unity_tmp.log") | Out-Null }
    }
    Write-Host "== Building Unity player (this takes several minutes)" -ForegroundColor Cyan
    & $unity -batchmode -quit -projectPath (Join-Path $root "UnityGame") -buildTarget Win64 `
        -executeMethod Apex.EditorTools.ApexBuildPipeline.BuildWindows -logFile (Join-Path $root "Build\unity_build.log") | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "unity build failed (see Build\unity_build.log)" }
}

Write-Host "== Bundling backend" -ForegroundColor Cyan
$backend = Join-Path $root "Build\Windows\Backend"
New-Item -ItemType Directory -Force $backend | Out-Null
Copy-Item (Join-Path $root "JavaEngine\target\apex-engine.jar") $backend -Force
$jre = Join-Path $backend "jre"
if (-not (Test-Path (Join-Path $jre "bin\java.exe"))) {
    $javaHome = $env:JAVA_HOME
    if (-not $javaHome) { $javaHome = Split-Path -Parent (Split-Path -Parent (Get-Command java).Source) }
    $jlink = Join-Path $javaHome "bin\jlink.exe"
    $modules = "java.base,java.compiler,java.datatransfer,java.desktop,java.instrument,java.logging,java.management,java.naming,java.net.http,java.prefs,java.rmi,java.scripting,java.security.jgss,java.security.sasl,java.sql,java.transaction.xa,java.xml,jdk.charsets,jdk.crypto.ec,jdk.management,jdk.net,jdk.unsupported,jdk.zipfs,jdk.localedata"
    & $jlink --add-modules $modules --strip-debug --no-man-pages --no-header-files --compress=zip-6 --output $jre
    if ($LASTEXITCODE -ne 0) { throw "jlink failed" }
}
Write-Host "== Done: $(Join-Path $root 'Build\Windows\ProjectApex.exe')" -ForegroundColor Green
