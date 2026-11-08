namespace Tayf.Core;
public static class MatteRefiner
{
    public static SubjectMatte Paint(SubjectMatte original,double x,double y,double radius,bool keep,double feather=.45)
    {
        original.Validate();if(!double.IsFinite(x)||!double.IsFinite(y)||!double.IsFinite(radius)||radius<=0||radius>.5)throw new ArgumentOutOfRangeException(nameof(radius));
        var bytes=(byte[])original.Decode().Clone();double cx=Math.Clamp(x,0,1)*original.Width,cy=Math.Clamp(y,0,1)*original.Height,r=radius*Math.Max(original.Width,original.Height),inner=r*(1-Math.Clamp(feather,0,.95));
        for(int iy=Math.Max(0,(int)(cy-r));iy<Math.Min(original.Height,Math.Ceiling(cy+r));iy++)for(int ix=Math.Max(0,(int)(cx-r));ix<Math.Min(original.Width,Math.Ceiling(cx+r));ix++)
        {double d=Math.Sqrt(Math.Pow(ix+.5-cx,2)+Math.Pow(iy+.5-cy,2));if(d>r)continue;double a=d<=inner?1:(r-d)/Math.Max(.0001,r-inner);a=a*a*(3-2*a);int i=iy*original.Width+ix;bytes[i]=(byte)Math.Round(bytes[i]*(1-a)+(keep?255:0)*a);}
        return SubjectMatte.Create(original.Width,original.Height,bytes);
    }
}
