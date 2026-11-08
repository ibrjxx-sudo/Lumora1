using System.Text.Json;
using System.Text.Json.Serialization;
using Lumora.Application.Contracts;

namespace Lumora.Application.Services;
[JsonSourceGenerationOptions(WriteIndented=true)]
[JsonSerializable(typeof(UserSettings))]
internal partial class ApplicationJsonContext:JsonSerializerContext;
public sealed class SettingsStore:ISettingsStore,IDisposable
{
    readonly string file;readonly SemaphoreSlim gate=new(1,1);
    public SettingsStore(IAppPaths paths)=>file=Path.Combine(paths.DataDirectory,"universal-settings.json");
    public async Task<UserSettings> ReadAsync(CancellationToken token=default)
    {
        await gate.WaitAsync(token);try{if(!File.Exists(file)){string legacy=Path.Combine(Path.GetDirectoryName(file)!,"language.txt");return File.Exists(legacy)&&(await File.ReadAllTextAsync(legacy,token)).Trim()=="en"?new("en"):new();}try{await using var input=File.OpenRead(file);var settings=await JsonSerializer.DeserializeAsync(input,ApplicationJsonContext.Default.UserSettings,token)??new();Validate(settings);return settings;}catch(Exception error)when(error is JsonException or InvalidDataException){File.Copy(file,file+".corrupt-"+DateTime.UtcNow.ToString("yyyyMMddHHmmssfff"),false);return new();}}finally{gate.Release();}
    }
    public async Task WriteAsync(UserSettings settings,CancellationToken token=default)
    {
        Validate(settings);await gate.WaitAsync(token);string temporary=file+"."+Guid.NewGuid().ToString("N")+".tmp";try{Directory.CreateDirectory(Path.GetDirectoryName(file)!);await using(var output=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None,4096,true)){await JsonSerializer.SerializeAsync(output,settings,ApplicationJsonContext.Default.UserSettings,token);await output.FlushAsync(token);output.Flush(true);}token.ThrowIfCancellationRequested();if(File.Exists(file))File.Replace(temporary,file,file+".bak",true);else File.Move(temporary,file);}finally{if(File.Exists(temporary))File.Delete(temporary);gate.Release();}
    }
    static void Validate(UserSettings settings){if(settings.Language is not "ar" and not "en"||settings.Theme is not "dark" and not "light"||settings.PreviewSide is <400 or >4096)throw new InvalidDataException("Invalid application settings.");}
    public void Dispose()=>gate.Dispose();
}
public sealed class FileAppLog:IAppLog
{
    readonly IAppPaths paths;readonly object gate=new();
    public FileAppLog(IAppPaths paths)=>this.paths=paths;
    public void Error(string operation,Exception error){try{lock(gate){Directory.CreateDirectory(paths.LogsDirectory);string file=Path.Combine(paths.LogsDirectory,"lumora-"+DateTime.UtcNow.ToString("yyyyMMdd")+".log");if(File.Exists(file)&&new FileInfo(file).Length>2_000_000)File.Move(file,file+"."+Guid.NewGuid().ToString("N"));File.AppendAllText(file,DateTime.UtcNow.ToString("O")+" "+operation+" "+error.GetType().Name+": "+error.Message+Environment.NewLine);}}catch(IOException){}catch(UnauthorizedAccessException){}}
}
