param([Parameter(Mandatory=$true)][string]$PrivateKeyPath)
$ErrorActionPreference='Stop'
$repository=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$secret=[IO.Path]::GetFullPath($PrivateKeyPath)
if($secret.StartsWith($repository+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Choose a private key location outside the source repository.'}
New-Item -ItemType Directory -Path (Split-Path $secret -Parent) -Force | Out-Null
$rsa=[Security.Cryptography.RSA]::Create(3072)
try{
 if(Test-Path -LiteralPath $secret){$rsa.ImportFromPem([IO.File]::ReadAllText($secret))}else{[IO.File]::WriteAllText($secret,$rsa.ExportPkcs8PrivateKeyPem())}
 [IO.File]::WriteAllText((Join-Path $repository 'Assets/release-public.pem'),$rsa.ExportSubjectPublicKeyInfoPem())
 Write-Output 'Public key prepared. Rebuild Windows before signing the published release.'
}finally{$rsa.Dispose()}
