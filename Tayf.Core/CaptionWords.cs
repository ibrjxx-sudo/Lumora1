using System.Text;
using System.Text.RegularExpressions;
namespace Tayf.Core;
public static class CaptionWords
{
    public static List<CaptionCue> Normalize(IEnumerable<CaptionCue> cues,double start,double end)
    {
        var result=new List<CaptionCue>();
        foreach(var cue in cues.OrderBy(c=>c.Start))
        {
            if(!double.IsFinite(cue.Start)||!double.IsFinite(cue.End)||cue.End<=start||cue.Start>=end)continue;
            string text=Regex.Replace(cue.Text.Normalize(NormalizationForm.FormC),@"\s+"," ").Trim();if(text.Length==0)continue;
            double a=Math.Clamp(cue.Start,start,end),b=Math.Clamp(cue.End,a,end);if(b<=a)continue;
            var previous=result.LastOrDefault();
            if(previous is not null&&previous.Text==text&&a<previous.End&&(previous.End-a)>Math.Min(previous.End-previous.Start,b-a)*.5)
            {previous.End=Math.Max(previous.End,b);previous.Confidence=Math.Max(previous.Confidence,cue.Confidence);continue;}
            result.Add(new(){Start=a,End=b,Text=text,Confidence=double.IsFinite(cue.Confidence)?Math.Clamp(cue.Confidence,0,1):0});
        }
        Captions.Validate(result);return result;
    }
}
