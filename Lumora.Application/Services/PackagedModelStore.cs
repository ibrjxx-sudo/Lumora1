using Tayf.Core;
namespace Lumora.Application.Services;

/// <summary>Installs pinned local assets on first use without HTTP or loading model weights into managed memory.</summary>
public sealed class PackagedModelStore(string directory,Func<string,CancellationToken,Task<Stream>> openAsset)
{
    readonly SemaphoreSlim gate=new(1,1);
    public async Task<string> EnsureAsync(string id,IProgress<double>? progress=null,CancellationToken token=default)
    {
        var definition=LocalModels.All.First(m=>m.Id==id);string target=Path.Combine(directory,definition.FileName);
        static bool Complete(string path,long size)=>File.Exists(path)&&new FileInfo(path).Length==size;
        if(Complete(target,definition.Size))return target;
        await gate.WaitAsync(token);
        try
        {
            if(Complete(target,definition.Size))return target;
            await using var stream=await openAsset(definition.FileName,token);
            await LocalModels.InstallToAsync(id,directory,stream,progress,token);
            return target;
        }
        finally{gate.Release();}
    }
}
