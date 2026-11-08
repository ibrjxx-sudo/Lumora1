param([string]$PayloadRoot=(Join-Path (Split-Path $PSScriptRoot -Parent) 'artifacts\Windows'))
$ErrorActionPreference='Stop';$source=Split-Path $PSScriptRoot -Parent
$payload=[IO.Path]::GetFullPath($PayloadRoot);$shared=Join-Path $payload 'Models';New-Item -ItemType Directory -Force $shared | Out-Null
$definitions=[regex]::Matches([IO.File]::ReadAllText((Join-Path $source 'Tayf.Core\LocalModels.cs')),'new\("([^"]+)","[^"]+","([^"]+)","[^"]+","([a-f0-9]+)",(\d+)\)')
if($definitions.Count -ne 6){throw 'The pinned model inventory is missing.'}
foreach($model in $definitions)
{
    $name=$model.Groups[2].Value;$modelSource=Join-Path $source "Models\$name"
    if(!(Test-Path -LiteralPath $modelSource)){throw "Missing bundled model: $name"}
    if((Get-FileHash -LiteralPath $modelSource -Algorithm SHA256).Hash -ne $model.Groups[3].Value){throw "Bundled model hash mismatch: $name"}
    Copy-Item -LiteralPath $modelSource -Destination (Join-Path $shared $name) -Force
}
foreach($client in @('Maui','Classic'))
{
    $duplicate=[IO.Path]::GetFullPath((Join-Path $payload "$client\Models"))
    if(!$duplicate.StartsWith($payload+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Duplicate-model cleanup escaped the Windows payload.'}
    if(Test-Path -LiteralPath $duplicate){Remove-Item -LiteralPath $duplicate -Recurse -Force}
}
Write-Output 'Both Windows clients use one pinned, shared offline model folder.'
