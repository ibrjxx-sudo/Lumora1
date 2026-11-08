param([ValidateSet('apk','aab','both')][string]$Format='both',[string]$KeyStore=$env:LUMORA_KEYSTORE,[string]$Alias=$env:LUMORA_KEY_ALIAS,[string]$PasswordFile=$env:LUMORA_KEY_PASSWORD_FILE,[switch]$BuildNative,[string]$Runtime='android-arm64')
$ErrorActionPreference='Stop';$root=Split-Path $PSScriptRoot -Parent
if(!$KeyStore -or !$Alias -or !$PasswordFile){throw 'Set LUMORA_KEYSTORE, LUMORA_KEY_ALIAS and LUMORA_KEY_PASSWORD_FILE. Keep signing files outside the source tree.'}
if(!(Test-Path -LiteralPath $KeyStore) -or !(Test-Path -LiteralPath $PasswordFile)){throw 'Signing input file is missing.'}
if($BuildNative){& (Join-Path $PSScriptRoot 'Build-NativeAndroid.ps1');if($LASTEXITCODE -ne 0){throw 'Native build failed.'}}
$formats=if($Format -eq 'both'){'apk;aab'}else{$Format}
$project=Join-Path $root 'Lumora.Maui\Lumora.Maui.csproj'
$keyPasswordFile=Join-Path ([IO.Path]::GetTempPath()) ('lumora-key-'+[Guid]::NewGuid().ToString('N')+'.txt')
Copy-Item -LiteralPath $PasswordFile -Destination $keyPasswordFile
$props=@('-p:LumoraTargetFrameworks=net10.0-android',"-p:AndroidPackageFormats=$formats",'-p:AndroidKeyStore=true',"-p:AndroidSigningKeyStore=$KeyStore","-p:AndroidSigningKeyAlias=$Alias","-p:AndroidSigningStorePass=file:$PasswordFile","-p:AndroidSigningKeyPass=file:$keyPasswordFile")
# MSBuild command-line list separator must be escaped for a property containing both formats.
if($Format -eq 'both'){$props[1]='-p:AndroidPackageFormats=apk%3Baab'}
try{
    dotnet publish $project -f net10.0-android -c Release -r $Runtime @props -o (Join-Path $root 'artifacts\Android')
    if($LASTEXITCODE -ne 0){throw 'Android publish failed.'}
}finally{Remove-Item -LiteralPath $keyPasswordFile -Force -ErrorAction SilentlyContinue}
