using System.IO;
using System.Text.Json.Serialization;

namespace Tayf.Core;
public sealed class MotionClip
{
    public string Id {get;set;}=Guid.NewGuid().ToString("N");
    public string Source {get;set;}="";
    public double SourceDuration {get;set;}
    public double Start {get;set;}
    public double End {get;set;}
    public double Speed {get;set;}=1;
    public double Volume {get;set;}=1;
    public int Turns {get;set;}
    public double FadeIn {get;set;}
    public double FadeOut {get;set;}
    public bool Denoise {get;set;}
    public Recipe Recipe {get;set;}=new();
    public List<CaptionCue> Captions {get;set;}=[];
    public string Title {get;set;}="";
    public string Transition {get;set;}="fade";
    public double TransitionSeconds {get;set;}=.5;
    [JsonIgnore]public double Duration=>(End-Start)/Speed;
    public MotionClip Clone()=>Json.Read<MotionClip>(Json.Write(this));
    public void Validate(bool requireSource=true)
    {
        ValidateLocalPath(Source);
        if(string.IsNullOrWhiteSpace(Source)||(requireSource&&!File.Exists(Source)))throw new FileNotFoundException("ملف فيديو المشروع مفقود. أعد ربطه من موقعه الأصلي.",Source);
        if(new[]{SourceDuration,Start,End,Speed,Volume,FadeIn,FadeOut,TransitionSeconds}.Any(v=>!double.IsFinite(v))||Start<0||End<=Start||End>SourceDuration+.1||Speed is <.25 or >4||Volume is <0 or >2||FadeIn<0||FadeOut<0||TransitionSeconds is <0 or >5)throw new InvalidDataException("قص أو سرعة أو صوت غير صالح في المشروع.");
        if(!TransitionCatalog.All.Any(v=>v.Id==Transition))throw new InvalidDataException("انتقال غير مدعوم.");Recipe.Validate();Tayf.Core.Captions.Validate(Captions);if(Title.Length>300)throw new InvalidDataException("العنوان أطول من الحد.");
    }
    public static void ValidateLocalPath(string path){if(Uri.TryCreate(path,UriKind.Absolute,out var uri)&&!uri.IsFile)throw new InvalidDataException("Only filesystem media sources are supported.");}
}
public sealed class MotionProject
{
    public int Version {get;set;}=1;
    public List<MotionClip> Clips {get;set;}=[];
    public CaptionStyle CaptionStyle {get;set;}=new();
    public bool BurnCaptions {get;set;}=true;
    public string? Music {get;set;}
    public double MusicVolume {get;set;}=.18;
    public int MaxSide {get;set;}=1920;
    public double Fps {get;set;}=30;
    public void Validate(bool requireSources=true){if(CaptionStyle is null)throw new InvalidDataException("تنسيق كابشن مفقود.");CaptionStyle.Validate();if(Version!=1||Clips is null||Clips.Count is <1 or >100||MaxSide is <0 or >7680||!double.IsFinite(Fps)||Fps is <15 or >120||!double.IsFinite(MusicVolume)||MusicVolume is <0 or >1)throw new InvalidDataException("إعدادات مشروع المونتاج غير صالحة.");foreach(var c in Clips)c.Validate(requireSources);if(requireSources&&Music is not null&&!File.Exists(Music))throw new FileNotFoundException("ملف الموسيقى مفقود.",Music);}
    public static double Overlap(MotionClip a,MotionClip b)=>a.Transition=="cut"?0:Math.Min(a.TransitionSeconds,Math.Min(a.Duration,b.Duration)*.45);
    public double[] Positions(){var p=new double[Clips.Count];for(int i=1;i<p.Length;i++)p[i]=p[i-1]+Clips[i-1].Duration-Overlap(Clips[i-1],Clips[i]);return p;}
    public double Duration=>Clips.Count==0?0:Positions()[^1]+Clips[^1].Duration;
    public List<CaptionCue> ExportCaptions()
    {
        var list=new List<CaptionCue>();var positions=Positions();for(int i=0;i<Clips.Count;i++){var clip=Clips[i];double begin=positions[i]+(i>0?Overlap(Clips[i-1],clip)/2:0),end=positions[i]+clip.Duration-(i<Clips.Count-1?Overlap(clip,Clips[i+1])/2:0);foreach(var c in Tayf.Core.Captions.Trim(clip.Captions,clip.Start,clip.End,clip.Speed,positions[i])){c.Start=Math.Max(begin,c.Start);c.End=Math.Min(end,c.End);if(c.End>c.Start+.02)list.Add(c);}if(!string.IsNullOrWhiteSpace(clip.Title))list.Add(new(){Start=begin,End=Math.Min(end,begin+3),Text=clip.Title});}return list;
    }
}
public sealed record TransitionDefinition(string Id,string Name,string Category);
public static class TransitionCatalog
{
    public static readonly IReadOnlyList<TransitionDefinition> All=Create();
    public static string Filter(string id)
    {
        if(!All.Any(v=>v.Id==id)||id=="cut")throw new InvalidDataException("انتقال غير صالح.");
        // The bundled FFmpeg's native squeezev crashes on a boundary frame.
        // Use a bounded custom resampling expression for this transition instead.
        if(id=="squeezev")
        {
            string y="min(H-1,max(0,(Y-H*(1-P)/2)/max(P,0.001)))";
            return $"xfade=transition=custom:expr='if(between(Y,H*(1-P)/2,H*(1+P)/2),if(eq(PLANE,0),a0(X,{y}),if(eq(PLANE,1),a1(X,{y}),a2(X,{y}))),B)'";
        }
        return "xfade=transition="+id;
    }
    static List<TransitionDefinition> Create()
    {
        var list=new List<TransitionDefinition>{new("cut","قص مباشر","أساسي")};
        void Add(string category,params (string Id,string Name)[] rows){list.AddRange(rows.Select(r=>new TransitionDefinition(r.Id,r.Name,category)));}
        Add("مزج وضوء",("fade","ذوبان ناعم"),("dissolve","تحلل عضوي"),("fadeblack","تلاشي للأسود"),("fadewhite","ومضة بيضاء"),("fadegrays","عبور رمادي"),("fadefast","ذوبان سريع"),("fadeslow","ذوبان بطيء"),("hblur","مزج ضبابي"),("distance","انتشار لوني"));
        Add("مسح",("wipeleft","مسح لليسار"),("wiperight","مسح لليمين"),("wipeup","مسح للأعلى"),("wipedown","مسح للأسفل"),("wipetl","مسح قطري علوي يسار"),("wipetr","مسح قطري علوي يمين"),("wipebl","مسح قطري سفلي يسار"),("wipebr","مسح قطري سفلي يمين"),("diagtl","قطر علوي يسار"),("diagtr","قطر علوي يمين"),("diagbl","قطر سفلي يسار"),("diagbr","قطر سفلي يمين"));
        Add("حركة",("slideleft","انزلاق لليسار"),("slideright","انزلاق لليمين"),("slideup","انزلاق للأعلى"),("slidedown","انزلاق للأسفل"),("smoothleft","إزاحة ناعمة يسار"),("smoothright","إزاحة ناعمة يمين"),("smoothup","إزاحة ناعمة أعلى"),("smoothdown","إزاحة ناعمة أسفل"),("zoomin","اقتراب"),("squeezeh","ضغط أفقي"),("squeezev","ضغط رأسي"));
        Add("أشكال",("circlecrop","دائرة مقتطعة"),("rectcrop","إطار مستطيل"),("radial","دوران شعاعي"),("circleopen","فتح دائرة"),("circleclose","إغلاق دائرة"),("vertopen","فتح رأسي"),("vertclose","إغلاق رأسي"),("horzopen","فتح أفقي"),("horzclose","إغلاق أفقي"),("pixelize","بكسلات"));
        Add("شرائح ورياح",("hlslice","شرائح يسار"),("hrslice","شرائح يمين"),("vuslice","شرائح أعلى"),("vdslice","شرائح أسفل"),("hlwind","رياح يسار"),("hrwind","رياح يمين"),("vuwind","رياح أعلى"),("vdwind","رياح أسفل"));
        Add("تغطية وكشف",("coverleft","تغطية يسار"),("coverright","تغطية يمين"),("coverup","تغطية أعلى"),("coverdown","تغطية أسفل"),("revealleft","كشف يسار"),("revealright","كشف يمين"),("revealup","كشف أعلى"),("revealdown","كشف أسفل"));return list;
    }
}
