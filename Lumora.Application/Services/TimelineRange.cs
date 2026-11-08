namespace Lumora.Application.Services;
public readonly record struct TimelineRange(double Start,double End,double Position)
{
    public static TimelineRange Drag(double duration,double start,double end,double position,string handle,double fraction)
    {
        if(!double.IsFinite(duration)||duration<=0)throw new ArgumentOutOfRangeException(nameof(duration));
        double span=Math.Min(.05,duration/4),time=Math.Clamp(fraction,0,1)*duration;
        start=Math.Clamp(start,0,duration-span);end=Math.Clamp(end,start+span,duration);
        if(handle=="start")start=Math.Clamp(time,0,end-span);
        else if(handle=="end")end=Math.Clamp(time,start+span,duration);
        else position=time;
        return new(start,end,Math.Clamp(position,start,end));
    }
}
