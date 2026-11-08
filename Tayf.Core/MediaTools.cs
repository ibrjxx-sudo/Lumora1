using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;

namespace Tayf.Core;
public static class MediaTools
{
    public static string FFmpeg=>Path.Combine(AppContext.BaseDirectory,"Native","ffmpeg","ffmpeg.exe");
    public static string FFprobe=>Path.Combine(AppContext.BaseDirectory,"Native","ffmpeg","ffprobe.exe");
    public static string Whisper=>Path.Combine(AppContext.BaseDirectory,"Native","whisper","whisper-cli.exe");
    public static string Number(double value)=>value.ToString("0.######",CultureInfo.InvariantCulture);
    public static async Task<string> RunAsync(string exe,IEnumerable<string> args,string? directory=null,Action<string>? log=null,CancellationToken token=default)
    {
        if(!File.Exists(exe))throw new FileNotFoundException("أعد فك حزمة Lumora كاملة مع مجلد Native.",exe);token.ThrowIfCancellationRequested();var info=new ProcessStartInfo(exe){UseShellExecute=false,CreateNoWindow=true,RedirectStandardError=true,RedirectStandardOutput=true,StandardOutputEncoding=Encoding.UTF8,StandardErrorEncoding=Encoding.UTF8,WorkingDirectory=directory??Path.GetDirectoryName(exe)!};foreach(string a in args)info.ArgumentList.Add(a);
        using var process=new Process{StartInfo=info};if(!process.Start())throw new InvalidOperationException("تعذر بدء المحرك المحلي.");using var registration=token.Register(()=>{try{if(!process.HasExited)process.Kill(true);}catch(InvalidOperationException){}});var errors=new StringBuilder();var output=new StringBuilder();
        async Task Read(StreamReader reader,StringBuilder text){string? line;while((line=await reader.ReadLineAsync())is not null){if(text.Length<2_000_000)text.AppendLine(line);log?.Invoke(line);}}
        await Task.WhenAll(Read(process.StandardError,errors),Read(process.StandardOutput,output),process.WaitForExitAsync(CancellationToken.None));token.ThrowIfCancellationRequested();if(process.ExitCode!=0)throw new InvalidOperationException("تعذر تشغيل المحرك المحلي: "+(errors.Length>5000?errors.ToString()[^5000..]:errors.ToString()));return output.ToString();
    }
    public static async Task<MediaProbe> ProbeAsync(string path,CancellationToken token=default)
    {
        var text=await RunAsync(FFprobe,["-v","error","-show_streams","-show_format","-of","json",Path.GetFullPath(path)],token:token);using var d=JsonDocument.Parse(text);var streams=d.RootElement.GetProperty("streams").EnumerateArray().ToArray();var v=streams.FirstOrDefault(s=>s.GetProperty("codec_type").GetString()=="video");if(v.ValueKind==JsonValueKind.Undefined)throw new InvalidDataException("الملف لا يحتوي مسار فيديو.");
        double N(JsonElement e,string k,double fallback=0)=>e.TryGetProperty(k,out var a)&&double.TryParse(a.ToString(),NumberStyles.Float,CultureInfo.InvariantCulture,out var n)&&double.IsFinite(n)?n:fallback;string fps=v.TryGetProperty("avg_frame_rate",out var f)?f.GetString()??"30/1":"30/1";var pair=fps.Split('/');double rate=pair.Length==2&&double.TryParse(pair[0],CultureInfo.InvariantCulture,out double num)&&double.TryParse(pair[1],CultureInfo.InvariantCulture,out double den)&&den>0?num/den:30;double duration=d.RootElement.TryGetProperty("format",out var format)?N(format,"duration"):N(v,"duration");int rotation=0;if(v.TryGetProperty("side_data_list",out var side))foreach(var a in side.EnumerateArray())if(a.TryGetProperty("rotation",out var rot))rotation=rot.GetInt32();int w=v.GetProperty("width").GetInt32(),h=v.GetProperty("height").GetInt32();if(Math.Abs(rotation)%180==90)(w,h)=(h,w);
        return new(w,h,duration,rate>0?rate:30,streams.Any(s=>s.GetProperty("codec_type").GetString()=="audio"));
    }
}
public sealed record MediaProbe(int Width,int Height,double Duration,double Fps,bool HasAudio);
public sealed class MediaWorkArea:IDisposable
{
    public string DirectoryPath {get;}
    public MediaWorkArea(){DirectoryPath=Path.Combine(Path.GetTempPath(),"LumoraJobs",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(DirectoryPath);}
    public string FilePath(string name)=>Path.Combine(DirectoryPath,name);
    public void Dispose(){string root=Path.GetFullPath(Path.Combine(Path.GetTempPath(),"LumoraJobs"))+Path.DirectorySeparatorChar;string full=Path.GetFullPath(DirectoryPath);if(full.StartsWith(root,StringComparison.OrdinalIgnoreCase)&&Directory.Exists(full))try{Directory.Delete(full,true);}catch(IOException){}catch(UnauthorizedAccessException){}}
}
