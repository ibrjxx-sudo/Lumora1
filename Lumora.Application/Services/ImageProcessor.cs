using Lumora.Application.Contracts;
using Lumora.Shared.Imaging;
using Tayf.Core;

namespace Lumora.Application.Services;
public sealed class ImageProcessor:IImageProcessor
{
    readonly IImageCodec codec;readonly SemaphoreSlim decodeGate=new(1,1),renderGate=new(1,1);string? cachedPath;long cachedStamp;int cachedSide,reducedSide;ImageFrame? cachedFrame,reducedFrame;
    public ImageProcessor(IImageCodec codec){this.codec=codec;SharedImageIO.Codec=codec;}
    public Task<ImageFrame> ReadAsync(string path,int side=1100,CancellationToken token=default)=>LoadSourceAsync(path,side,token);
    public async Task<ImageFrame> PreviewAsync(string path,Recipe recipe,int side=1100,CancellationToken token=default)
    {
        var copy=RecipeSnapshot.Capture(recipe);copy.Validate();var source=await LoadSourceAsync(path,side,token);
        await renderGate.WaitAsync(token);
        try{return await Task.Run(()=>ImageEngine.Render(source,copy,token),token);}
        finally{renderGate.Release();}
    }
    async Task<ImageFrame> LoadSourceAsync(string path,int side,CancellationToken token)
    {
        await decodeGate.WaitAsync(token);
        try
        {
            long stamp=File.GetLastWriteTimeUtc(path).Ticks;
            if(cachedFrame is null||cachedPath!=path||cachedStamp!=stamp||cachedSide<side)
            {cachedFrame=await Task.Run(()=>codec.Load(path,side),token);cachedPath=path;cachedStamp=stamp;cachedSide=side;reducedFrame=null;}
            if(side<cachedSide)
            {if(reducedFrame is null||reducedSide!=side){var full=cachedFrame;reducedFrame=await Task.Run(()=>SharedImageIO.Resize(full,side),token);reducedSide=side;}}
            return side<cachedSide?reducedFrame!:cachedFrame;
        }
        finally{decodeGate.Release();}
    }
    public async Task ExportAsync(string path,Recipe recipe,string target,ExportSettings settings,CancellationToken token=default)
    {
        if(Path.GetFullPath(path).Equals(Path.GetFullPath(target),StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Choose a new output file.");var copy=recipe.Clone();copy.Validate();string temporary=target+"."+Guid.NewGuid().ToString("N")+".tmp";
        try{await Task.Run(()=>{token.ThrowIfCancellationRequested();var frame=ImageEngine.Render(codec.Load(path),copy,token);codec.Save(frame,temporary,settings);token.ThrowIfCancellationRequested();File.Move(temporary,target,true);},token);}finally{if(File.Exists(temporary))File.Delete(temporary);}
    }
    public async Task Export4KAsync(string path,Recipe recipe,string target,CancellationToken token=default,IProgress<double>? progress=null)
    {
        if(Path.GetFullPath(path).Equals(Path.GetFullPath(target),StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Choose a new output file.");var copy=recipe.Clone();string temporary=target+"."+Guid.NewGuid().ToString("N")+".tmp";
        try{await Task.Run(()=>{var frame=ImageEngine.Render(codec.Load(path),copy,token);var enhanced=SuperResolution.Upscale(frame,3840,token,progress);codec.Save(enhanced,temporary,new());token.ThrowIfCancellationRequested();File.Move(temporary,target,true);},token);}finally{if(File.Exists(temporary))File.Delete(temporary);}
    }
}
public sealed class PhotoIntelligence:IPhotoIntelligence
{
    SubjectSegmentation? segmentation;readonly SemaphoreSlim gate=new(1,1);
    public async Task<SubjectMatte> SegmentAsync(ImageFrame frame,CancellationToken token=default){await gate.WaitAsync(token);try{if(segmentation is null)segmentation=await Task.Run(()=>{ObjectRemoval.ReleaseModel();return new SubjectSegmentation(LocalModels.PathFor("subject"));},token);var matte=await segmentation.PredictAsync(frame,token);return await Task.Run(()=>SubjectRefinement.Edges(frame,matte,token),token);}finally{gate.Release();}}
    public Task<FaceAnchor?> DetectFaceAsync(ImageFrame frame,CancellationToken token=default)=>Task.Run(()=>{token.ThrowIfCancellationRequested();return FaceDetector.Detect(frame,LocalModels.PathFor("face"));},token);
    public async Task<ImageFrame> RemoveAsync(ImageFrame frame,IReadOnlyList<RemovalOperation> removals,CancellationToken token=default){var snapshot=Json.Read<List<RemovalOperation>>(Json.Write(removals.ToList()));await PrepareRemovalAsync(token);return await Task.Run(()=>ObjectRemoval.Apply(frame,snapshot,token),token);}
    public async Task PrepareRemovalAsync(CancellationToken token=default){await gate.WaitAsync(token);try{if(segmentation is not null)await Task.Run(()=>{segmentation.Dispose();segmentation=null;},token);}finally{gate.Release();}}
    public async Task ReleaseCachesAsync(CancellationToken token=default){await gate.WaitAsync(token);try{await Task.Run(()=>{segmentation?.Dispose();segmentation=null;ObjectRemoval.ReleaseModel();},token);}finally{gate.Release();}}
    public void Dispose(){segmentation?.Dispose();gate.Dispose();}
}
