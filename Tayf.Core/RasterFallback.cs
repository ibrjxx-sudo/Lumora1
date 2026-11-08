using System.Diagnostics;
using System.IO;

namespace Tayf.Core;
public static class RasterFallback
{
    public static ImageFrame Load(string path,int maxSide=0)
    {
        string exe=Path.Combine(AppContext.BaseDirectory,"Native","ffmpeg","ffmpeg.exe");if(!File.Exists(exe))throw new InvalidDataException("فك حزمة Lumora كاملة مع مجلد Native لدعم هذه الصيغة.");
        string directory=Path.Combine(Path.GetTempPath(),"LumoraRaster",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);string result=Path.Combine(directory,"frame.png");
        try
        {
            var info=new ProcessStartInfo(exe){UseShellExecute=false,CreateNoWindow=true,RedirectStandardError=true,RedirectStandardOutput=true};foreach(string a in new[]{"-hide_banner","-v","error","-nostdin","-i",Path.GetFullPath(path),"-frames:v","1"})info.ArgumentList.Add(a);int side=maxSide>0?maxSide:10000;foreach(string a in new[]{"-vf",$"scale=w='min({side},iw)':h='min({side},ih)':force_original_aspect_ratio=decrease",result})info.ArgumentList.Add(a);
            using var process=Process.Start(info)??throw new InvalidDataException("تعذر بدء محرك الصور.");var error=process.StandardError.ReadToEndAsync();var output=process.StandardOutput.ReadToEndAsync();if(!process.WaitForExit(120000)){process.Kill(true);throw new InvalidDataException("استغرق فك الصورة أكثر من دقيقتين.");}Task.WaitAll(error,output);if(process.ExitCode!=0||!File.Exists(result))throw new InvalidDataException("ترميز الصورة غير متاح أو الملف تالف. جرّب تحويله إلى TIFF/PNG من مصدره.");return ImageIO.Load(result,maxSide);
        }finally{foreach(string file in Directory.EnumerateFiles(directory))File.Delete(file);Directory.Delete(directory);}
    }
}
