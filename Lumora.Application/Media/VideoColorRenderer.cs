using Lumora.Application.Media;
using Tayf.Core;
namespace Tayf.Desktop;
internal sealed record VideoRenderOptions(double Start=0,double? End=null,double Volume=1,int Turns=0,int MaxSide=0);
/// <summary>Native LUT fast path; bounded frame batches preserve the full existing image engine for spatial edits.</summary>
internal static class VideoColorRenderer
{
    static readonly string[] Spatial=["vignette","lensVignette","grain","sharpen","texture","clarity","blur","portraitBlur","subjectCutout","keyEnabled","luminanceNoise","colorNoise","rotation","vertical","horizontal","distortion"];
    public static async Task ExportAsync(string input,string output,Recipe recipe,IProgress<double>? progress=null,CancellationToken token=default,VideoRenderOptions? options=null)
    {
        var info=await MediaRuntime.ProbeAsync(input,token);options??=new();double end=options.End??info.Duration,duration=end-options.Start;if(duration<=0||end>info.Duration+.1||options.Start<0)throw new InvalidDataException("Invalid video range.");
        var copy=recipe.Clone();copy.Set("quarterTurns",copy.Get("quarterTurns")+options.Turns);copy.Validate();
        bool spatial=Spatial.Any(k=>copy.Get(k)!=0)||copy.Masks.Count>0||copy.Removals.Count>0||copy.Retouch.Count>0||copy.Face is not null;
        using var work=new MediaWorkArea();
        if(!spatial)
        {
            string lut=work.FilePath("colour.cube");await Task.Run(()=>XmpPreset.SaveCube(lut,new(){Recipe=PresetMixer.Photographic(copy)}),token);
            var crop=copy.Crop;string vf="lut3d=file="+MediaRuntime.FilterPath(lut)+":interp=tetrahedral";
            if(!crop.SequenceEqual(new[]{0d,0,1,1}))vf+=$",crop=iw*{MediaRuntime.Number(crop[2])}:ih*{MediaRuntime.Number(crop[3])}:iw*{MediaRuntime.Number(crop[0])}:ih*{MediaRuntime.Number(crop[1])}";
            if(copy.Get("flipX")!=0)vf+=",hflip";if(copy.Get("flipY")!=0)vf+=",vflip";int turns=((int)copy.Get("quarterTurns")%4+4)%4;vf+=turns switch{1=>",transpose=clock",2=>",hflip,vflip",3=>",transpose=cclock",_=>""};
            if(options.MaxSide>0)vf+=$",scale=w='min({options.MaxSide},iw)':h='min({options.MaxSide},ih)':force_original_aspect_ratio=decrease";vf+=",scale=trunc(iw/2)*2:trunc(ih/2)*2,setsar=1";
            await MediaRuntime.RunAsync(MediaRuntime.FFmpeg,["-y","-v","error","-filter_threads","2","-ss",MediaRuntime.Number(options.Start),"-i",input,"-t",MediaRuntime.Number(duration),"-map","0:v:0","-map","0:a:0?","-vf",vf,"-c:v","libx264","-preset","veryfast","-crf","16","-threads","3","-c:a","aac","-b:a","192k","-af","volume="+MediaRuntime.Number(options.Volume),output],token:token);progress?.Report(100);return;
        }
        // Keep only 24 decoded frames on disk and a single working frame in managed memory.
        double fps=Math.Clamp(info.Fps,1,120);int count=checked((int)Math.Ceiling(duration*fps));var batches=new List<string>();
        for(int first=0;first<count;first+=24)
        {
            token.ThrowIfCancellationRequested();int length=Math.Min(24,count-first);string pattern=work.FilePath("input-%04d.png");string scale=options.MaxSide>0?$",scale=w='min({options.MaxSide},iw)':h='min({options.MaxSide},ih)':force_original_aspect_ratio=decrease":"";
            await MediaRuntime.RunAsync(MediaRuntime.FFmpeg,["-y","-v","error","-ss",MediaRuntime.Number(options.Start+first/fps),"-i",input,"-frames:v",length.ToString(),"-vf","fps="+MediaRuntime.Number(fps)+scale,"-start_number","0",pattern],token:token);
            int actual=0;
            for(int index=0;index<length;index++)
            {
                string source=work.FilePath($"input-{index:0000}.png");if(!File.Exists(source))break;int current=index;
                await Task.Run(()=>{var frame=ImageEngine.Render(ImageIO.Load(source),copy,token);int w=Math.Max(2,frame.Width/2*2),h=Math.Max(2,frame.Height/2*2);if(w!=frame.Width||h!=frame.Height)frame=ImageIO.Stretch(frame,w,h);ImageIO.Save(frame,work.FilePath($"output-{current:0000}.png"),new());},token);File.Delete(source);actual++;progress?.Report((first+index+1)*90d/count);
            }
            if(actual==0)throw new InvalidDataException("The video decoder returned no frames.");string batch=work.FilePath($"batch-{batches.Count:00000}.mkv");
            await MediaRuntime.RunAsync(MediaRuntime.FFmpeg,["-y","-v","error","-framerate",MediaRuntime.Number(fps),"-i",work.FilePath("output-%04d.png"),"-frames:v",actual.ToString(),"-c:v","ffv1","-level","3",batch],token:token);batches.Add(batch);for(int i=0;i<actual;i++)File.Delete(work.FilePath($"output-{i:0000}.png"));
        }
        string concat=work.FilePath("frames.txt");await File.WriteAllLinesAsync(concat,batches.Select(p=>"file '"+p.Replace('\\','/').Replace("'","'\\''")+"'"),token);
        await MediaRuntime.RunAsync(MediaRuntime.FFmpeg,["-y","-v","error","-f","concat","-safe","0","-i",concat,"-ss",MediaRuntime.Number(options.Start),"-i",input,"-map","0:v:0","-map","1:a:0?","-t",MediaRuntime.Number(duration),"-c:v","libx264","-preset","veryfast","-crf","16","-threads","3","-c:a","aac","-b:a","192k","-af","volume="+MediaRuntime.Number(options.Volume),output],token:token);progress?.Report(100);
    }
}
