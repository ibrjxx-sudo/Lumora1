using System.IO;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
namespace Tayf.Core;
public static class AutoCaptionEngine
{
    public static async Task<List<CaptionCue>> GenerateAsync(string input,double start,double end,string language="ar",string glossary="",bool denoise=false,IProgress<double>? progress=null,CancellationToken token=default,bool useVad=false)
    {
        if(start<0||end<=start||end-start>1800||!double.IsFinite(start)||!double.IsFinite(end))throw new InvalidDataException("اختر نطاقًا بين 0 و30 دقيقة لتوليد الكابشن.");string model=LocalModels.PathFor("captions");if(!File.Exists(model))throw new FileNotFoundException("ثبّت نموذج الكابشن المحلي من مركز النماذج أولًا.");if(language is not "ar" and not "auto" and not "en")throw new ArgumentException("لغة غير مدعومة.");if(glossary.Length>1200)glossary=glossary[..1200];
        using var work=new MediaWorkArea();string wave=work.FilePath("speech.wav");var extract=new List<string>{"-hide_banner","-v","error","-nostdin","-ss",MediaTools.Number(start),"-i",Path.GetFullPath(input),"-t",MediaTools.Number(end-start),"-vn","-ac","1","-ar","16000"};if(denoise)extract.AddRange(["-af","highpass=f=70,afftdn=nf=-25"]);extract.AddRange(["-c:a","pcm_s16le",wave]);try{await MediaTools.RunAsync(MediaTools.FFmpeg,extract,token:token);}catch(InvalidOperationException e)when(e.Message.Contains("does not contain any stream",StringComparison.OrdinalIgnoreCase)||e.Message.Contains("Stream map",StringComparison.OrdinalIgnoreCase)){throw new InvalidDataException("لم أجد مسار صوت قابلًا للقراءة في هذا المقطع.",e);}progress?.Report(5);
        // Entirely silent input is handled before ASR to avoid invented captions.
        if(WaveRms(wave)<.0005)return [];
        string prefix=work.FilePath("captions");var args=new List<string>{"-m",model,"-f",wave,"-l",language,"-t",Math.Clamp(Environment.ProcessorCount-1,1,8).ToString(CultureInfo.InvariantCulture),"-ng","-bs","5","-bo","5","-ml","48","-sow","-osrt","-ojf","-of",prefix,"-np","-pp","-sns"};if(!string.IsNullOrWhiteSpace(glossary))args.AddRange(["--prompt",glossary]);
        string vad=LocalModels.PathFor("vad");if(useVad&&File.Exists(vad))args.AddRange(["--vad","-vm",vad,"--vad-min-speech-duration-ms","200","--vad-min-silence-duration-ms","400"]);
        await MediaTools.RunAsync(MediaTools.Whisper,args,log:line=>{var m=Regex.Match(line,@"progress\s*=\s*(\d+)%");if(m.Success)progress?.Report(5+int.Parse(m.Groups[1].Value,CultureInfo.InvariantCulture)*.94);},token:token);token.ThrowIfCancellationRequested();if(!File.Exists(prefix+".srt"))throw new InvalidDataException("لم يُنتج محرك التفريغ نصًا. راجع وجود صوت واضح.");var cues=Captions.ParseSrt(await File.ReadAllTextAsync(prefix+".srt",token));foreach(var c in cues){c.Start+=start;c.End=Math.Min(end,c.End+start);}cues=cues.Where(c=>c.End>c.Start).ToList();progress?.Report(100);return cues;
    }
    static double WaveRms(string path)
    {
        using var reader=new BinaryReader(File.OpenRead(path));reader.BaseStream.Position=12;while(reader.BaseStream.Position+8<=reader.BaseStream.Length){string id=Encoding.ASCII.GetString(reader.ReadBytes(4));uint length=reader.ReadUInt32();if(id=="data"){long end=Math.Min(reader.BaseStream.Length,reader.BaseStream.Position+length);double sum=0;long n=0;while(reader.BaseStream.Position+2<=end){double v=reader.ReadInt16()/32768d;sum+=v*v;n++;}return Math.Sqrt(sum/Math.Max(1,n));}reader.BaseStream.Position=Math.Min(reader.BaseStream.Length,reader.BaseStream.Position+length+(length%2));}throw new InvalidDataException("الصوت المستخرج غير صالح.");
    }
}
