param([string]$Makensis,[switch]$Installer)
$ErrorActionPreference='Stop';$root=Split-Path $PSScriptRoot -Parent;$payload=Join-Path $root 'artifacts\Windows'
dotnet publish (Join-Path $root 'Lumora.Maui\Lumora.Maui.csproj') -f net10.0-windows10.0.19041.0 -c Release -r win-x64 --self-contained true -p:LumoraTargetFrameworks=net10.0-windows10.0.19041.0 -p:WindowsAppSDKSelfContained=true -o (Join-Path $payload 'Maui')
if($LASTEXITCODE -ne 0){throw 'MAUI Windows publish failed.'}
dotnet publish (Join-Path $root 'Tayf.Desktop\Tayf.Desktop.csproj') -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:TayfRuntimeVersion=10.0.14 -o (Join-Path $payload 'Classic')
if($LASTEXITCODE -ne 0){throw 'Classic Windows publish failed.'}
& (Join-Path $PSScriptRoot 'Stage-SharedModels.ps1') -PayloadRoot $payload
if($Installer)
{
    if(!$Makensis){$Makensis=(Get-Command makensis.exe -ErrorAction Stop).Source}
    & $Makensis "/DPAYLOAD=$payload" "/DOUTPUT=$(Join-Path $root 'artifacts')" (Join-Path $root 'Installer\Lumora.nsi')
    if($LASTEXITCODE -ne 0){throw 'Installer compilation failed.'}
}
