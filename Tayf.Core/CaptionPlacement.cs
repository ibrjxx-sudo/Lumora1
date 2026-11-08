namespace Tayf.Core;

/// <summary>Normalized coordinates refer to the displayed video, excluding letterboxing.</summary>
public readonly record struct TextViewport(double X,double Y,double Width,double Height);
public static class CaptionPlacement
{
    public static TextViewport Fit(double width,double height,double mediaWidth,double mediaHeight)
    {
        if(!double.IsFinite(width)||!double.IsFinite(height)||width<=0||height<=0)return new(0,0,0,0);
        if(!double.IsFinite(mediaWidth)||!double.IsFinite(mediaHeight)||mediaWidth<=0||mediaHeight<=0)return new(0,0,width,height);
        double scale=Math.Min(width/mediaWidth,height/mediaHeight),w=mediaWidth*scale,h=mediaHeight*scale;
        return new((width-w)/2,(height-h)/2,w,h);
    }
    public static double AnchorY(CaptionStyle style)=>style.PositionY??1-style.Bottom/100;
    public static void Move(CaptionStyle style,double startX,double startY,double dx,double dy,TextViewport viewport)
    {
        if(viewport.Width<=0||viewport.Height<=0||!new[]{startX,startY,dx,dy}.All(double.IsFinite))return;
        style.PositionX=Math.Clamp(startX+dx/viewport.Width,0,1);style.PositionY=Math.Clamp(startY+dy/viewport.Height,0,1);
    }
    public static void Resize(CaptionStyle style,double originalSize,double scale)
    {if(double.IsFinite(originalSize)&&double.IsFinite(scale)&&scale>0)style.Size=Math.Clamp(originalSize*scale,12,240);}
}
