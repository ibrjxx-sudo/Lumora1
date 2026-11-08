using System.IO.Compression;
using System.Security.Cryptography;
using Tayf.Core;
namespace Lumora.Application.Services;

/// <summary>Imports original pinned models from a local pack or a full Lumora APK, without HTTP.</summary>
public sealed class OfflineModelPack(string directory,string cache)
{
    const long MaxArchiveBytes=1_200_000_000;
    readonly SemaphoreSlim gate=new(1,1);
    public async Task ImportAsync(Stream input,IProgress<double>? progress=null,CancellationToken token=default,IReadOnlyList<string>? ids=null)
    {
        var definitions=ids is null?LocalModels.All:LocalModels.All.Where(m=>ids.Contains(m.Id)).ToArray();
        if(definitions.Length==0||ids is not null&&definitions.Length!=ids.Count)throw new InvalidDataException("Unknown model selection.");
        await gate.WaitAsync(token);string? staged=null;
        try
        {
            Stream archiveStream=input;
            if(!input.CanSeek)
            {
                Directory.CreateDirectory(cache);staged=Path.Combine(cache,"offline-pack-"+Guid.NewGuid().ToString("N")+".zip");
                await using(var output=File.Create(staged)){byte[] buffer=new byte[1024*1024];long count=0;int read;while((read=await input.ReadAsync(buffer,token))>0){if((count+=read)>MaxArchiveBytes)throw new InvalidDataException("Model pack exceeds its size budget.");await output.WriteAsync(buffer.AsMemory(0,read),token);}}
                archiveStream=File.OpenRead(staged);
            }
            try
            {
                if(archiveStream.Length>MaxArchiveBytes)throw new InvalidDataException("Model pack exceeds its size budget.");
                using var archive=new ZipArchive(archiveStream,ZipArchiveMode.Read,true);
                if(archive.Entries.Count>16000||archive.Entries.Any(e=>e.FullName.StartsWith('/')||e.FullName.Contains('\\')||e.FullName.Split('/').Contains("..")))throw new InvalidDataException("Unsafe model archive.");
                var selected=new List<(LocalModel Model,ZipArchiveEntry Entry)>();
                foreach(var model in definitions)
                {
                    string[] names=["Models/"+model.FileName,"Models/"+model.FileName+".br","assets/Models/"+model.FileName,"assets/Models/"+model.FileName+".br"];
                    var entries=archive.Entries.Where(e=>names.Contains(e.FullName,StringComparer.Ordinal)).ToArray();
                    if(entries.Length!=1)throw new InvalidDataException("Missing or duplicate model: "+model.FileName);
                    selected.Add((model,entries[0]));
                }
                long total=definitions.Sum(m=>m.Size),done=0;
                foreach(var (model,entry) in selected)
                {
                    token.ThrowIfCancellationRequested();string target=Path.Combine(directory,model.FileName);bool valid=false;
                    if(File.Exists(target)&&new FileInfo(target).Length==model.Size){await using var existing=File.OpenRead(target);valid=Convert.ToHexString(await SHA256.HashDataAsync(existing,token)).Equals(model.Sha256,StringComparison.OrdinalIgnoreCase);}
                    if(!valid)
                    {
                        await using var encoded=entry.Open();await using Stream decoded=entry.Name.EndsWith(".br",StringComparison.Ordinal)?new BrotliStream(encoded,CompressionMode.Decompress,true):encoded;
                        long offset=done;var modelProgress=new InlineProgress(v=>progress?.Report((offset+v*model.Size/100)*100/total));
                        await LocalModels.InstallToAsync(model.Id,directory,decoded,modelProgress,token);
                    }
                    done+=model.Size;progress?.Report(done*100d/total);
                }
            }
            finally{if(!ReferenceEquals(archiveStream,input))await archiveStream.DisposeAsync();}
        }
        finally{if(staged is not null&&File.Exists(staged))File.Delete(staged);gate.Release();}
    }
    sealed class InlineProgress(Action<double> report):IProgress<double>{public void Report(double value)=>report(value);}
}
