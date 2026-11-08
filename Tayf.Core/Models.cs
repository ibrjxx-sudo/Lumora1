using System.Text.Json;
using System.Text.Json.Serialization;
using System.IO;

namespace Tayf.Core;

public sealed record ImageFrame(int Width, int Height, byte[] Pixels)
{
    public ushort[]? LinearRgb {get;init;}
    public void Validate()
    {
        if (Width < 1 || Height < 1 || (long)Width * Height > 120_000_000 || Pixels.Length != (long)Width * Height * 4)
            throw new InvalidDataException("بيانات الصورة غير صالحة أو تتجاوز حد المعالجة.");
        if(LinearRgb is not null&&LinearRgb.LongLength!=(long)Width*Height*3)throw new InvalidDataException("بيانات RAW غير صالحة.");
    }
}
public sealed record CurvePoint(double X, double Y);
public sealed class ColorAdjustment { public double H { get; set; } public double S { get; set; } public double L { get; set; } }
public sealed class MaskRecipe
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "قناع";
    public string Type { get; set; } = "radial";
    public double X { get; set; } = .5; public double Y { get; set; } = .5;
    public double X2 { get; set; } = .8; public double Y2 { get; set; } = .8;
    public double Rx { get; set; } = .3; public double Ry { get; set; } = .3;
    public double Size { get; set; } = .08; public double Feather { get; set; } = .5;
    public double Opacity { get; set; } = 1; public bool Invert { get; set; }
    public double Exposure { get; set; } public double Contrast { get; set; } public double Saturation { get; set; }
    public double Min { get; set; } = .4; public double Max { get; set; } = 1;
    public double Tolerance { get; set; } = .15;
    public double[] Color { get; set; } = [.5, .5, .5];
    public List<double[]> Points { get; set; } = [];
}
public sealed class RetouchStroke
{
    public string Type { get; set; } = "clone";
    public double X { get; set; } public double Y { get; set; }
    public double Sx { get; set; } public double Sy { get; set; }
    public double Size { get; set; } = .03; public double Feather { get; set; } = .5;
    public double Opacity { get; set; } = 1;
}
public sealed class Recipe
{
    public FaceAnchor? Face {get;set;}
    public List<RemovalOperation> Removals {get;set;}=[];
    public int Version { get; set; } = 1;
    public Dictionary<string, double> Values { get; set; } = [];
    public Dictionary<string, List<CurvePoint>> Curves { get; set; } = new()
    { ["rgb"] = [new(0,0),new(1,1)], ["r"] = [new(0,0),new(1,1)], ["g"] = [new(0,0),new(1,1)], ["b"] = [new(0,0),new(1,1)] };
    public List<ColorAdjustment> Hsl { get; set; } = Enumerable.Range(0,8).Select(_=>new ColorAdjustment()).ToList();
    public Dictionary<string, ColorAdjustment> Grading { get; set; } = new()
    { ["shadows"] = new(), ["midtones"] = new(), ["highlights"] = new(), ["global"] = new() };
    public double[] Crop { get; set; } = [0,0,1,1];
    public double[] KeyColor { get; set; } = [255,255,255];
    public double[]? Background { get; set; }
    public List<MaskRecipe> Masks { get; set; } = [];
    public List<RetouchStroke> Retouch { get; set; } = [];
    public SubjectMatte? Subject {get;set;}
    public string? BackgroundImage {get;set;}
    public static readonly IReadOnlyDictionary<string,double> Defaults = new Dictionary<string,double>
    { ["subjectScale"]=1,["midpoint"]=50,["feather"]=65,["grainSize"]=1,["grainRoughness"]=50,["radius"]=1,["keyTolerance"]=35,["keyFeather"]=8,["gradingBlending"]=50 };
    public double Get(string key) => Values.TryGetValue(key,out double v) && double.IsFinite(v) ? v : Defaults.GetValueOrDefault(key);
    public void Set(string key,double value) { if (!double.IsFinite(value)) throw new ArgumentException("قيمة التعديل غير صالحة."); Values[key]=value; }
    public Recipe Clone() => Json.Read<Recipe>(Json.Write(this));
    public void Validate()
    {
        if(Version!=1 || Values is null || Values.Count>150 || Values.Any(k=>!double.IsFinite(k.Value)))throw new InvalidDataException("إصدار وصفة أو قيم غير مدعومة.");
        Subject?.Validate();
        Face?.Validate();
        if(Removals is null||Removals.Count>30)throw new InvalidDataException("عدد عمليات الإزالة أكبر من الحد.");foreach(var r in Removals)r.Validate();
        if(Crop is null || Crop.Length!=4 || Crop.Any(v=>!double.IsFinite(v)) || Crop[0]<0 || Crop[1]<0 || Crop[2]<=0 || Crop[3]<=0 || Crop[0]+Crop[2]>1.00001 || Crop[1]+Crop[3]>1.00001)throw new InvalidDataException("حدود القص غير صالحة.");
        if(Hsl is null || Hsl.Count!=8 || Grading is null || new[]{"shadows","midtones","highlights","global"}.Any(k=>!Grading.ContainsKey(k)))throw new InvalidDataException("بيانات الألوان غير صالحة.");
        foreach(var a in Hsl.Concat(Grading.Values)) if(a is null || !double.IsFinite(a.H)||!double.IsFinite(a.S)||!double.IsFinite(a.L))throw new InvalidDataException("قيمة لونية غير صالحة.");
        if(Curves is null || new[]{"rgb","r","g","b"}.Any(k=>!Curves.ContainsKey(k)))throw new InvalidDataException("قنوات المنحنى غير صالحة.");
        foreach(var points in Curves.Values)if(points is null||points.Count<2||points.Count>64||points.Any(p=>p is null||!double.IsFinite(p.X)||!double.IsFinite(p.Y)||p.X<0||p.X>1||p.Y<0||p.Y>1))throw new InvalidDataException("نقاط المنحنى غير صالحة.");
        if(KeyColor is null || KeyColor.Length!=3 || KeyColor.Any(v=>!double.IsFinite(v)) || Background is not null && (Background.Length!=3||Background.Any(v=>!double.IsFinite(v))))throw new InvalidDataException("لون الخلفية غير صالح.");
        if(Masks is null || Masks.Count>30 || Retouch is null || Retouch.Count>5000)throw new InvalidDataException("عدد الأقنعة أو لمسات الترميم أكبر من الحد.");
        foreach(var m in Masks)
        {
            if(m is null)throw new InvalidDataException("قناع فارغ غير صالح.");
            double[] values=[m.X,m.Y,m.X2,m.Y2,m.Rx,m.Ry,m.Size,m.Feather,m.Opacity,m.Exposure,m.Contrast,m.Saturation,m.Min,m.Max,m.Tolerance];
            if(values.Any(v=>!double.IsFinite(v))||m.Opacity is <0 or >1||m.Exposure is <-3 or >3||m.Feather is <=0 or >1||m.Size is <=0 or >.3||m.Points is null||m.Points.Count>15000||m.Points.Any(p=>p.Length!=2||p.Any(v=>!double.IsFinite(v)||v<0||v>1))||m.Color is null||m.Color.Length!=3||m.Color.Any(v=>!double.IsFinite(v)))throw new InvalidDataException("بيانات القناع غير صالحة.");
        }
        foreach(var s in Retouch){if(s is null)throw new InvalidDataException("لمسة ترميم فارغة غير صالحة.");double[] values=[s.X,s.Y,s.Sx,s.Sy,s.Size,s.Feather,s.Opacity];if(values.Any(v=>!double.IsFinite(v))||s.X is <0 or >1||s.Y is <0 or >1||s.Size is <=0 or >.3||s.Opacity is <0 or >1||s.Feather is <=0 or >1)throw new InvalidDataException("بيانات الترميم غير صالحة.");}
    }
}
public static class Json
{
    public static readonly JsonSerializerOptions Options=new(){WriteIndented=true,PropertyNameCaseInsensitive=true,NumberHandling=JsonNumberHandling.Strict};
    public static string Write<T>(T value)=>Lumora.Shared.Serialization.EngineSerializer.Write(value);
    public static T Read<T>(string text)=>Lumora.Shared.Serialization.EngineSerializer.Read<T>(text);
}
public sealed class MediaItem
{
    public string Id { get; set; }=""; public string Name { get; set; }="";
    public string Original { get; set; }=""; public string Source { get; set; }="";
    public int Width { get; set; } public int Height { get; set; }
    public string Thumbnail { get; set; }="";
    public Recipe Recipe { get; set; }=new();
    public List<Revision> History { get; set; }=[];
    public int HistoryIndex { get; set; }
    public List<Revision> Versions { get; set; }=[];
    public int Rating { get; set; } public bool Favorite { get; set; } public int Flag { get; set; }
    public string Album { get; set; }=""; public string Label { get; set; }="";
    public bool Deleted { get; set; } public DateTime Added { get; set; }=DateTime.UtcNow;
}
public sealed class Revision
{
    public string Name { get; set; }="الأصل"; public DateTime Created { get; set; }=DateTime.UtcNow;
    public Recipe Recipe { get; set; }=new();
}
public sealed class Preset { public string Description {get;set;}=""; public string Id { get; set; }=Guid.NewGuid().ToString("N"); public string Name { get; set; }="فلتر"; public string Category {get;set;}="فلاتري"; public Recipe Recipe { get; set; }=new(); }
public sealed class CatalogData
{
    public int Version { get; set; }=1;
    public List<MediaItem> Media { get; set; }=[];
    public List<string> Albums { get; set; }=[];
    public List<Preset> Presets { get; set; }=[];
    public string? Current { get; set; }
    public string Language { get; set; }="ar";
}
public sealed record ExportSettings(string Format="png",int Quality=92,int LongEdge=0,string Watermark="");
