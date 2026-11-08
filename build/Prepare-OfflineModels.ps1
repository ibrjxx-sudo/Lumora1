param([Parameter(Mandatory=$true)][string]$ModelArchive)
$ErrorActionPreference='Stop';$root=Split-Path $PSScriptRoot -Parent
Add-Type -AssemblyName System.IO.Compression.FileSystem
$packed=[IO.Compression.ZipFile]::OpenRead((Resolve-Path -LiteralPath $ModelArchive).Path)
try{$hasBrotli=@($packed.Entries | Where-Object {$_.Name -eq 'subject.onnx.br'}).Count -eq 1}finally{$packed.Dispose()}
if($hasBrotli){
    dotnet run --project (Join-Path $PSScriptRoot 'ModelCompression/Codec.csproj') -c Release -- restoreapk ([IO.Path]::GetFullPath($ModelArchive)) (Join-Path $root 'Models') (Join-Path $root 'Models/model-index.json')
    if($LASTEXITCODE -ne 0){throw 'Compressed model restoration failed.'}
    return
}
$definitions=[regex]::Matches([IO.File]::ReadAllText((Join-Path $root 'Tayf.Core\LocalModels.cs')),'new\("([^"]+)","[^"]+","([^"]+)","[^"]+","([a-f0-9]+)",(\d+)\)')
if($definitions.Count -ne 6){throw 'The pinned model inventory is missing.'}
$archive=[IO.Compression.ZipFile]::OpenRead((Resolve-Path -LiteralPath $ModelArchive).Path)
try
{
    foreach($model in $definitions)
    {
        $name=$model.Groups[2].Value;$target=Join-Path $root "Models\$name"
        if(!(Test-Path -LiteralPath $target))
        {
            $entries=@($archive.Entries | Where-Object {$_.Name -eq $name})
            if($entries.Count -ne 1){throw "Expected exactly one model named $name. Small bundled models are included in the source zip."}
            $temporary=$target+'.preparing'
            try{$sourceStream=$entries[0].Open();$destinationStream=[IO.File]::Create($temporary);try{$sourceStream.CopyTo($destinationStream,1048576)}finally{$destinationStream.Dispose();$sourceStream.Dispose()}
                if((Get-FileHash -LiteralPath $temporary -Algorithm SHA256).Hash -ne $model.Groups[3].Value){throw "Model integrity check failed: $name"}
                Move-Item -LiteralPath $temporary -Destination $target
            }finally{if(Test-Path -LiteralPath $temporary){Remove-Item -LiteralPath $temporary -Force}}
        }
        if((Get-Item -LiteralPath $target).Length -ne [long]$model.Groups[4].Value -or (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash -ne $model.Groups[3].Value){throw "Model integrity check failed: $name"}
        Write-Output "Verified offline model: $name"
    }
}
finally{$archive.Dispose()}
