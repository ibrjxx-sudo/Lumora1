namespace Tayf.Core;
public static class PresetMixer
{
    static readonly HashSet<string> SpatialKeys=["rotation","quarterTurns","flipX","flipY","vertical","horizontal","keyEnabled","keyTolerance","keyFeather","blur","portraitBlur","subjectCutout","subjectEdge","subjectX","subjectY","subjectScale","skinSmooth","blemish","faceSlim","jaw","chin","eyeSize","eyeDistance","eyeLift","noseWidth","noseLength","mouthWidth","redEye","eyeReflection"];
    public static Recipe Photographic(Recipe source){var copy=source.Clone();copy.Crop=[0,0,1,1];copy.Masks.Clear();copy.Retouch.Clear();copy.Removals.Clear();copy.Face=null;copy.Background=null;copy.BackgroundImage=null;copy.Subject=null;copy.Values.Remove("blur");copy.Values.Remove("portraitBlur");copy.Values.Remove("subjectCutout");foreach(string key in SpatialKeys)copy.Values.Remove(key);return copy;}
    public static Recipe Apply(Recipe current,Recipe original,Recipe settings,double amount)
    {
        var target=Photographic(settings);var recipe=current;
        if(amount==0)
        {
            var restored=original.Clone();var currentSpatial=recipe.Clone();
            restored.Crop=currentSpatial.Crop;restored.Masks=currentSpatial.Masks;restored.Retouch=currentSpatial.Retouch;restored.Face=currentSpatial.Face;restored.Removals=currentSpatial.Removals;restored.KeyColor=currentSpatial.KeyColor;restored.Background=currentSpatial.Background;restored.BackgroundImage=currentSpatial.BackgroundImage;restored.Subject=currentSpatial.Subject;
            foreach(string key in SpatialKeys)if(currentSpatial.Values.TryGetValue(key,out double value))restored.Set(key,value);else restored.Values.Remove(key);
            return restored;
        }
        var next=recipe.Clone();var keys=original.Values.Keys.Concat(target.Values.Keys).Concat(Recipe.Defaults.Keys).Distinct();foreach(string key in keys)if(!SpatialKeys.Contains(key))next.Set(key,original.Get(key)+(target.Get(key)-original.Get(key))*amount);
        double Blend(double a,double b)=>a+(b-a)*amount;
        for(int i=0;i<8;i++)next.Hsl[i]=new(){H=Blend(original.Hsl[i].H,target.Hsl[i].H),S=Math.Clamp(Blend(original.Hsl[i].S,target.Hsl[i].S),-100,100),L=Math.Clamp(Blend(original.Hsl[i].L,target.Hsl[i].L),-100,100)};
        foreach(string zone in next.Grading.Keys.ToArray())next.Grading[zone]=new(){H=Blend(original.Grading[zone].H,target.Grading[zone].H),S=Math.Clamp(Blend(original.Grading[zone].S,target.Grading[zone].S),0,100),L=Math.Clamp(Blend(original.Grading[zone].L,target.Grading[zone].L),-100,100)};
        static double CurveAt(List<CurvePoint> values,double x){var sorted=values.OrderBy(v=>v.X).ToArray();if(x<=sorted[0].X)return sorted[0].Y;for(int i=1;i<sorted.Length;i++)if(x<=sorted[i].X)return sorted[i-1].Y+(sorted[i].Y-sorted[i-1].Y)*(x-sorted[i-1].X)/Math.Max(.000001,sorted[i].X-sorted[i-1].X);return sorted[^1].Y;}
        foreach(string channel in next.Curves.Keys.ToArray())next.Curves[channel]=amount==1?target.Curves[channel].Select(p=>new CurvePoint(p.X,p.Y)).ToList():Enumerable.Range(0,17).Select(i=>new CurvePoint(i/16d,Math.Clamp(Blend(CurveAt(original.Curves[channel],i/16d),CurveAt(target.Curves[channel],i/16d)),0,1))).ToList();
        return next;
    }

}
