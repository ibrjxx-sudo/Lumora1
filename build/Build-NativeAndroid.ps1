param([string[]]$Abis=@('arm64-v8a','x86_64'),[string]$AndroidSdk=$env:ANDROID_HOME)
$ErrorActionPreference='Stop'
if(!$AndroidSdk){$AndroidSdk=$env:ANDROID_SDK_ROOT}
if(!$AndroidSdk){throw 'Set ANDROID_HOME to the installed Android SDK directory.'}
$root=Split-Path $PSScriptRoot -Parent;$source=Join-Path $root 'Native.Mobile'
$cmake=Join-Path $AndroidSdk 'cmake\3.31.1\bin\cmake.exe';$ninja=Join-Path $AndroidSdk 'cmake\3.31.1\bin\ninja.exe';$toolchain=Join-Path $AndroidSdk 'ndk\27.2.12479018\build\cmake\android.toolchain.cmake'
foreach($abi in $Abis)
{
    if($abi -notin @('arm64-v8a','x86_64')){throw 'Only supported 64-bit ABIs may be built.'}
    $build=Join-Path $source "build\android-$abi"
    & $cmake -S $source -B $build -G Ninja "-DCMAKE_MAKE_PROGRAM=$ninja" "-DCMAKE_TOOLCHAIN_FILE=$toolchain" "-DANDROID_ABI=$abi" '-DANDROID_PLATFORM=android-26' '-DCMAKE_BUILD_TYPE=Release' '-DANDROID_STL=c++_static'
    if($LASTEXITCODE -ne 0){throw "Native configure failed: $abi"}
    & $cmake --build $build --target lumora_native --parallel 4
    if($LASTEXITCODE -ne 0){throw "Native build failed: $abi"}
    $output=Join-Path $source "Android\$abi";New-Item -ItemType Directory -Force $output | Out-Null;Copy-Item -LiteralPath (Join-Path $build 'liblumora_native.so') -Destination $output -Force
}
