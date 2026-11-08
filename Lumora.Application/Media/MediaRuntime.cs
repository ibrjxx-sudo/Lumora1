using System.Globalization;
using System.Text.Json;
namespace Lumora.Application.Media;
public interface IMediaCommandRunner
{
    Task<string> RunAsync(bool probe,IReadOnlyList<string> arguments,string? directory,Action<string>? log,CancellationToken token);
}
public sealed record MediaProbe(int Width,int Height,double Duration,double Fps,bool HasAudio);
/// <summary>The existing motion exporter targets this boundary. Mobile runs native libraries, Windows runs bundled tools.</summary>
public static class MediaRuntime
{
    public static IMediaCommandRunner? Runner {get;set;}
    public static string CacheDirectory {get;set;}=Path.Combine(Path.GetTempPath(),"LumoraJobs");
    public const string FFmpeg="ffmpeg",FFprobe="ffprobe";
    public static string Number(double value)=>value.ToString("0.######",CultureInfo.InvariantCulture);
    public static Task<string> RunAsync(string executable,IEnumerable<string> args,string? directory=null,Action<string>? log=null,CancellationToken token=default)
        =>(Runner??throw new InvalidOperationException("Media backend has not been configured.")).RunAsync(executable==FFprobe,args.ToArray(),directory,log,token);
    public static async Task<MediaProbe> ProbeAsync(string path,CancellationToken token=default)
    {
        var text=await RunAsync(FFprobe,["-v","error","-show_streams","-show_format","-of","json",Path.GetFullPath(path)],token:token);using var doc=JsonDocument.Parse(text);var root=doc.RootElement;var streams=root.GetProperty("streams").EnumerateArray().ToArray();var video=streams.FirstOrDefault(v=>v.TryGetProperty("codec_type",out var type)&&type.GetString()=="video");if(video.ValueKind==JsonValueKind.Undefined)throw new InvalidDataException("No video stream was found.");
        static double Read(JsonElement value,string key,double fallback=0)=>value.TryGetProperty(key,out var field)&&double.TryParse(field.ToString(),NumberStyles.Float,CultureInfo.InvariantCulture,out double number)&&double.IsFinite(number)?number:fallback;
        var pair=(video.TryGetProperty("avg_frame_rate",out var fps)?fps.GetString():"30/1")?.Split('/')??["30","1"];double rate=pair.Length==2&&double.TryParse(pair[0],CultureInfo.InvariantCulture,out double n)&&double.TryParse(pair[1],CultureInfo.InvariantCulture,out double d)&&d>0?n/d:30;
        int w=video.GetProperty("width").GetInt32(),h=video.GetProperty("height").GetInt32();if(video.TryGetProperty("side_data_list",out var side))foreach(var data in side.EnumerateArray())if(data.TryGetProperty("rotation",out var rotation)&&Math.Abs(rotation.GetInt32())%180==90)(w,h)=(h,w);
        double duration=root.TryGetProperty("format",out var format)?Read(format,"duration"):Read(video,"duration");if(w<1||h<1||duration<=0)throw new InvalidDataException("Invalid video metadata.");return new(w,h,duration,rate>0?rate:30,streams.Any(v=>v.TryGetProperty("codec_type",out var type)&&type.GetString()=="audio"));
    }
    public static string FilterPath(string path)=>"'"+Path.GetFullPath(path).Replace('\\','/').Replace(":","\\:").Replace("'","'\\''")+"'";
}
public sealed class MediaWorkArea:IDisposable
{
    readonly string root;public string DirectoryPath{get;}
    public MediaWorkArea(){root=Path.GetFullPath(MediaRuntime.CacheDirectory);DirectoryPath=Path.Combine(root,Guid.NewGuid().ToString("N"));Directory.CreateDirectory(DirectoryPath);}
    public string FilePath(string name)=>Path.Combine(DirectoryPath,Path.GetFileName(name));
    public void Dispose(){string full=Path.GetFullPath(DirectoryPath);if(!full.StartsWith(root+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))return;try{Directory.Delete(full,true);}catch(IOException){}catch(UnauthorizedAccessException){}}
}
