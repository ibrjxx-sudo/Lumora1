using Lumora.Application.Contracts;
using Tayf.Core;
namespace Lumora.Application.Media;
public sealed class MotionVideoService:IVideoService
{
    public IReadOnlySet<string> Transitions {get;}=TransitionCatalog.All.Select(v=>v.Id).ToHashSet();
    public async Task<VideoInfo> ProbeAsync(string path,CancellationToken token=default){var p=await MediaRuntime.ProbeAsync(path,token);return new(path,p.Width,p.Height,p.Duration,p.Fps,p.HasAudio);}
    public async Task<ImageFrame> FrameAsync(string path,double time,int side=1100,CancellationToken token=default)
    {
        MotionClip.ValidateLocalPath(path);if(!File.Exists(path))throw new FileNotFoundException("Video source is missing.",path);
        if(!double.IsFinite(time)||time<0||side is <64 or >4096)throw new ArgumentException("Invalid video frame request.");using var work=new MediaWorkArea();string target=work.FilePath("frame.png");
        await MediaRuntime.RunAsync(MediaRuntime.FFmpeg,["-y","-v","error","-ss",MediaRuntime.Number(time),"-i",path,"-frames:v","1","-vf",$"scale=w='min({side},iw)':h='min({side},ih)':force_original_aspect_ratio=decrease",target],token:token);return await Task.Run(()=>ImageIO.Load(target,side),token);
    }
    public Task ExportAsync(MotionProject project,string target,IProgress<double>? progress=null,CancellationToken token=default)
    {var snapshot=Json.Read<MotionProject>(Json.Write(project));snapshot.Validate();return Tayf.Desktop.MotionExporter.ExportAsync(snapshot,target,progress,token);}
}
