using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace Tayf.Core;
public sealed class CaptionCue
{
    public double Start {get;set;}
    public double End {get;set;}
    public string Text {get;set;}="";
    public double Confidence {get;set;}=1;
}
public sealed class CaptionStyle
{
    public string Theme {get;set;}="clean";
    public double Size {get;set;}=48;
    public double Bottom {get;set;}=8;
    public double PositionX {get;set;}=.5;
    public double? PositionY {get;set;}
    public void Validate(){if(!double.IsFinite(Size)||!double.IsFinite(Bottom)||Size is <12 or >240||Bottom is <0 or >35||!double.IsFinite(PositionX)||PositionX is <0 or >1||(PositionY is double y&&(!double.IsFinite(y)||y is <0 or >1))||Theme is not "clean" and not "box" and not "yellow" and not "mint")throw new InvalidDataException("تنسيق الكابشن غير صالح.");}
}
public static class Captions
{
    public static void Validate(IEnumerable<CaptionCue> cues)
    {
        int count=0;foreach(var c in cues){if(++count>20000||!double.IsFinite(c.Start)||!double.IsFinite(c.End)||c.Start<0||c.End<=c.Start||c.Text is null||c.Text.Length>2000)throw new InvalidDataException("سطر كابشن غير صالح: راجع النص والبداية والنهاية.");}
    }
    public static List<CaptionCue> ParseSrt(string text)
    {
        if(text.Length>5_000_000)throw new InvalidDataException("ملف الكابشن كبير جدًا.");var result=new List<CaptionCue>();foreach(string block in Regex.Split(text.Trim().Replace("\r\n","\n"),"\n\\s*\n"))
        {
            var lines=block.Split('\n');int time=Array.FindIndex(lines,l=>l.Contains("-->"));if(time<0)continue;var m=Regex.Match(lines[time],@"(\d+):(\d\d):(\d\d)[,.](\d{1,3})\s*-->\s*(\d+):(\d\d):(\d\d)[,.](\d{1,3})");if(!m.Success)throw new InvalidDataException("توقيت SRT غير صالح.");double T(int i)=>int.Parse(m.Groups[i].Value,CultureInfo.InvariantCulture)*3600+int.Parse(m.Groups[i+1].Value,CultureInfo.InvariantCulture)*60+int.Parse(m.Groups[i+2].Value,CultureInfo.InvariantCulture)+int.Parse(m.Groups[i+3].Value.PadRight(3,'0'),CultureInfo.InvariantCulture)/1000d;string value=string.Join("\n",lines.Skip(time+1)).Trim();if(value.Length>0)result.Add(new(){Start=T(1),End=T(5),Text=value});
        }
        Validate(result);return result.OrderBy(c=>c.Start).ToList();
    }
    static string SrtTime(double value){var t=TimeSpan.FromMilliseconds(Math.Round(value*1000));return $"{(int)t.TotalHours:00}:{t.Minutes:00}:{t.Seconds:00},{t.Milliseconds:000}";}
    public static void SaveSrt(string path,IEnumerable<CaptionCue> cues){var list=cues.OrderBy(c=>c.Start).ToList();Validate(list);using var writer=new StreamWriter(path,false,new UTF8Encoding(true));int i=1;foreach(var c in list)writer.WriteLine($"{i++}\n{SrtTime(c.Start)} --> {SrtTime(c.End)}\n{c.Text.Trim()}\n");}
    public static List<CaptionCue> Trim(IEnumerable<CaptionCue> cues,double start,double end,double speed=1,double offset=0)
    {
        if(!double.IsFinite(speed)||speed is <.25 or >4||start<0||end<=start)throw new InvalidDataException("نطاق كابشن غير صالح.");return cues.Where(c=>c.End>start&&c.Start<end&&!string.IsNullOrWhiteSpace(c.Text)).Select(c=>new CaptionCue{Start=offset+(Math.Max(start,c.Start)-start)/speed,End=offset+(Math.Min(end,c.End)-start)/speed,Text=c.Text,Confidence=c.Confidence}).Where(c=>c.End-c.Start>.02).ToList();
    }
    static string AssTime(double value){var t=TimeSpan.FromMilliseconds(Math.Round(value*100)*10);return $"{(int)t.TotalHours}:{t.Minutes:00}:{t.Seconds:00}.{t.Milliseconds/10:00}";}
    public static void SaveAss(string path,IEnumerable<CaptionCue> cues,CaptionStyle style,int width,int height)
    {
        style.Validate();
        var list=cues.OrderBy(c=>c.Start).ToList();Validate(list);string theme=style.Theme;string color=theme=="yellow"?"&H0000DEFF":theme=="mint"?"&H00CBEAB5":"&H00FFFFFF";double font=Math.Clamp(style.Size,12,240)*height/1080;int border=theme=="box"?3:1,margin=(int)(height*Math.Clamp(style.Bottom,0,35)/100);string clean(string s)=>s.Replace("\\","＼").Replace("{","(").Replace("}",")").Replace("\r","").Replace("\n","\\N");
        using var writer=new StreamWriter(path,false,new UTF8Encoding(false));writer.WriteLine($"[Script Info]\nScriptType: v4.00+\nPlayResX: {width}\nPlayResY: {height}\nWrapStyle: 0\nScaledBorderAndShadow: yes\n\n[V4+ Styles]\nFormat: Name, Fontname, Fontsize, PrimaryColour, SecondaryColour, OutlineColour, BackColour, Bold, Italic, Underline, StrikeOut, ScaleX, ScaleY, Spacing, Angle, BorderStyle, Outline, Shadow, Alignment, MarginL, MarginR, MarginV, Encoding\nStyle: Default,Noto Sans Arabic,{InvariantNumber.Format(font)},{color},&H0000DEFF,&H00101018,&H80000000,-1,0,0,0,100,100,0,0,{border},{InvariantNumber.Format(Math.Max(1,height/420d))},1,2,{width/15},{width/15},{margin},1\n\n[Events]\nFormat: Layer, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text");foreach(var c in list)writer.WriteLine($"Dialogue: 0,{AssTime(c.Start)},{AssTime(c.End)},Default,,0,0,0,,{(style.PositionY is not null||style.PositionX!=.5?$"{{\\an2\\pos({InvariantNumber.Format(style.PositionX*width)},{InvariantNumber.Format(CaptionPlacement.AnchorY(style)*height)})}}":"")}{clean(c.Text)}");
    }
}
