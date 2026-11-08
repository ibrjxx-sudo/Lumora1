using System.Diagnostics;
using System.Text;
namespace Lumora.Application.Media;
public sealed class ProcessMediaRunner(string toolsDirectory):IMediaCommandRunner
{
    public async Task<string> RunAsync(bool probe,IReadOnlyList<string> arguments,string? directory,Action<string>? log,CancellationToken token)
    {
        string executable=Path.Combine(toolsDirectory,probe?"ffprobe.exe":"ffmpeg.exe");if(!File.Exists(executable))throw new FileNotFoundException("The local media tools are missing.",executable);
        var info=new ProcessStartInfo(executable){UseShellExecute=false,CreateNoWindow=true,RedirectStandardError=true,RedirectStandardOutput=true,WorkingDirectory=directory??toolsDirectory,StandardErrorEncoding=Encoding.UTF8,StandardOutputEncoding=Encoding.UTF8};foreach(var a in arguments)info.ArgumentList.Add(a);
        using var process=new Process{StartInfo=info};process.Start();using var cancel=token.Register(()=>{try{if(!process.HasExited)process.Kill(true);}catch(InvalidOperationException){}});var output=new StringBuilder();var errors=new StringBuilder();
        async Task Read(StreamReader reader,StringBuilder result){while(await reader.ReadLineAsync() is {} line){if(result.Length<2_000_000)result.AppendLine(line);log?.Invoke(line);}}
        await Task.WhenAll(Read(process.StandardOutput,output),Read(process.StandardError,errors),process.WaitForExitAsync());token.ThrowIfCancellationRequested();if(process.ExitCode!=0)throw new IOException("Media processing failed: "+errors.ToString()[Math.Max(0,errors.Length-4000)..]);return output.ToString();
    }
}
