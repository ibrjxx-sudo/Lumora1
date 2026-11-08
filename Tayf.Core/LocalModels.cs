using System.IO;
using System.Net.Http;
using System.Security.Cryptography;

namespace Tayf.Core;
public sealed record LocalModel(string Id,string Name,string FileName,string Url,string Sha256,long Size);
public static class LocalModels
{
    public static readonly LocalModel[] All=[
        new("upscale","تكبير 4K · Real-ESRGAN","upscale4x.onnx","https://huggingface.co/qualcomm/Real-ESRGAN-General-x4v3/resolve/fe119b62b7cb27c3fa5fd3d992122f8e5a3a52c4/Real-ESRGAN-General-x4v3.onnx","bea98dff749104082449888501454b85a05ace135c89249599caec7ef2dace23",4872481),
        new("subject","عزل الخلفية · BiRefNet Lite","subject.onnx","https://huggingface.co/studioludens/birefnet-lite-512/resolve/962e67969ec6acec9c19fab76acee4f5e22e990c/onnx/model.onnx","1cb0fb360dadd15af77c639085d77a9df67db0c64315560c3de005f676345ac2",191877254),
        new("vad","كشف الكلام والصمت · Silero","ggml-silero-v6.2.0.bin","https://huggingface.co/ggml-org/whisper-vad/resolve/main/ggml-silero-v6.2.0.bin","2aa269b785eeb53a82983a20501ddf7c1d9c48e33ab63a41391ac6c9f7fb6987",885098),
        new("captions","الكابشن · Whisper Large v3 Turbo","ggml-large-v3-turbo-q5_0.bin","https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-large-v3-turbo-q5_0.bin","394221709cd5ad1f40c46e6031ca61bce88931e6e088c188294c6d5a55ffa7e2",574041195),
        new("erase","إزالة العناصر · LaMa","lama.onnx","https://huggingface.co/Carve/LaMa-ONNX/resolve/main/lama_fp32.onnx","1faef5301d78db7dda502fe59966957ec4b79dd64e16f03ed96913c7a4eb68d6",208044816),
        new("face","تحديد الوجه · YuNet","face.onnx","https://media.githubusercontent.com/media/opencv/opencv_zoo/main/models/face_detection_yunet/face_detection_yunet_2023mar.onnx","8f2383e4dd3cfbb4553ea8718107fc0423210dc964f9f4280604804ed2552fa4",232589)];
    public static string? DirectoryOverride {get;set;}
    public static string UserDirectory=>DirectoryOverride??Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Lumora","Models");
    public static string PathFor(string id)
    {
        var model=All.First(v=>v.Id==id);string bundled=Path.Combine(AppContext.BaseDirectory,"Models",model.FileName);
        if(File.Exists(bundled))return bundled;
        // Both installed Windows clients share models only inside a marked installation.
        string app=Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory);string? parent=Path.GetDirectoryName(app);
        if(OperatingSystem.IsWindows()&&(Path.GetFileName(app) is "Maui" or "Classic")&&parent is not null&&File.Exists(Path.Combine(parent,"lumora-install.marker")))
        {string shared=Path.Combine(parent,"Models",model.FileName);if(File.Exists(shared))return shared;}
        return Path.Combine(UserDirectory,model.FileName);
    }
    public static async Task DownloadAsync(string id,IProgress<double>? progress=null,CancellationToken token=default)
    {
        var model=All.First(v=>v.Id==id);using var http=new HttpClient{Timeout=TimeSpan.FromMinutes(45)};using var response=await http.GetAsync(model.Url,HttpCompletionOption.ResponseHeadersRead,token);response.EnsureSuccessStatusCode();await using var input=await response.Content.ReadAsStreamAsync(token);await InstallAsync(id,input,progress,token);
    }
    public static Task InstallAsync(string id,Stream input,IProgress<double>? progress=null,CancellationToken token=default)=>InstallToAsync(id,UserDirectory,input,progress,token);
    public static async Task InstallToAsync(string id,string directory,Stream input,IProgress<double>? progress=null,CancellationToken token=default)
    {
        var model=All.First(v=>v.Id==id);Directory.CreateDirectory(directory);string target=Path.Combine(directory,model.FileName),temporary=target+"."+Guid.NewGuid().ToString("N")+".download";
        try
        {
            await using(var output=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None,1024*1024,true))
            {byte[] buffer=new byte[1024*1024];long total=0;int read;while((read=await input.ReadAsync(buffer,token))>0){total+=read;if(total>model.Size)throw new InvalidDataException("حجم النموذج لا يطابق الإصدار المعتمد.");await output.WriteAsync(buffer.AsMemory(0,read),token);progress?.Report(total*100d/model.Size);}}
            await using var stream=File.OpenRead(temporary);string hash=Convert.ToHexString(await SHA256.HashDataAsync(stream,token));if(stream.Length!=model.Size||!hash.Equals(model.Sha256,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("فشل التحقق من سلامة النموذج.");stream.Close();token.ThrowIfCancellationRequested();File.Move(temporary,target,true);
        }
        finally{if(File.Exists(temporary))File.Delete(temporary);}
    }
}
