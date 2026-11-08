param([Parameter(Mandatory=$true)][string]$Directory,[Parameter(Mandatory=$true)][string]$PrivateKeyPath)
$ErrorActionPreference='Stop'
$repository=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$releaseRoot=[IO.Path]::GetFullPath($Directory)
$entries=@()
foreach($file in Get-ChildItem -LiteralPath $releaseRoot -Recurse -File){
 $relative=[IO.Path]::GetRelativePath($releaseRoot,$file.FullName).Replace('\','/')
 if($relative -eq 'Lumora.exe' -or ($relative.StartsWith('Native/') -and $file.Extension -in '.exe','.dll')){$entries+=[ordered]@{path=$relative;bytes=$file.Length;sha256=(Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant()}}
}
if(!($entries|Where-Object {$_.path -eq 'Lumora.exe'})){throw 'Lumora.exe is missing.'}
[xml]$project=Get-Content -LiteralPath (Join-Path $repository 'Tayf.Desktop/Tayf.Desktop.csproj') -Raw
$version=[string]$project.Project.PropertyGroup.Version
$manifest=[ordered]@{version=$version;algorithm='RSA-PSS-SHA256';files=@($entries|Sort-Object {$_.path})}|ConvertTo-Json -Depth 5 -Compress
$bytes=[Text.Encoding]::UTF8.GetBytes($manifest)
$rsa=[Security.Cryptography.RSA]::Create()
try{
 $rsa.ImportFromPem([IO.File]::ReadAllText([IO.Path]::GetFullPath($PrivateKeyPath)))
 if($rsa.ExportSubjectPublicKeyInfoPem().Trim() -ne ([IO.File]::ReadAllText((Join-Path $repository 'Assets/release-public.pem'))).Trim()){throw 'Private key does not match the public key. Initialize and rebuild first.'}
 [IO.File]::WriteAllBytes((Join-Path $releaseRoot 'release.integrity.json'),$bytes)
 [IO.File]::WriteAllBytes((Join-Path $releaseRoot 'release.integrity.sig'),$rsa.SignData($bytes,[Security.Cryptography.HashAlgorithmName]::SHA256,[Security.Cryptography.RSASignaturePadding]::Pss))
 Write-Output "Signed $($entries.Count) release files."
}finally{$rsa.Dispose()}
