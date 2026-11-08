namespace Tayf.Core;

public static class CanvasGeometry
{
    public static double[] MoveCrop(double[] start,double dx,double dy)=>[Math.Clamp(start[0]+dx,0,1-start[2]),Math.Clamp(start[1]+dy,0,1-start[3]),start[2],start[3]];
    public static double[] ResizeCrop(double[] start,string handle,double dx,double dy,double ratio=0)
    {
        double l=start[0],t=start[1],r=l+start[2],b=t+start[3];
        bool west=handle.Contains('w'),east=handle.Contains('e'),north=handle.Contains('n'),south=handle.Contains('s');
        if(west)l=Math.Clamp(l+dx,0,r-.01);if(east)r=Math.Clamp(r+dx,l+.01,1);if(north)t=Math.Clamp(t+dy,0,b-.01);if(south)b=Math.Clamp(b+dy,t+.01,1);
        if(ratio>0)
        {
            double anchorX=west?start[0]+start[2]:east?start[0]:start[0]+start[2]/2,anchorY=north?start[1]+start[3]:south?start[1]:start[1]+start[3]/2;
            double width=r-l,height=b-t;
            if((west||east)&&(!(north||south)||Math.Abs(dx)>=Math.Abs(dy)))height=width/ratio;else width=height*ratio;
            double maxW=west?anchorX:east?1-anchorX:2*Math.Min(anchorX,1-anchorX),maxH=north?anchorY:south?1-anchorY:2*Math.Min(anchorY,1-anchorY);
            double fit=Math.Min(1,Math.Min(maxW/width,maxH/height));width*=fit;height*=fit;
            l=west?anchorX-width:east?anchorX:anchorX-width/2;t=north?anchorY-height:south?anchorY:anchorY-height/2;r=l+width;b=t+height;
        }
        return [Math.Clamp(l,0,1),Math.Clamp(t,0,1),Math.Max(.001,r-l),Math.Max(.001,b-t)];
    }
    public static void MoveMask(MaskRecipe target,MaskRecipe start,double dx,double dy)
    {
        var xs=start.Type=="brush"&&start.Points.Count>0?start.Points.Select(p=>p[0]).ToArray():start.Type=="linear"?[start.X,start.X2]:new[]{start.X};
        var ys=start.Type=="brush"&&start.Points.Count>0?start.Points.Select(p=>p[1]).ToArray():start.Type=="linear"?[start.Y,start.Y2]:new[]{start.Y};
        dx=Math.Clamp(dx,-xs.Min(),1-xs.Max());dy=Math.Clamp(dy,-ys.Min(),1-ys.Max());
        target.X=Math.Clamp(start.X+dx,0,1);target.Y=Math.Clamp(start.Y+dy,0,1);target.X2=Math.Clamp(start.X2+dx,0,1);target.Y2=Math.Clamp(start.Y2+dy,0,1);target.Points=start.Points.Select(p=>new[]{p[0]+dx,p[1]+dy}).ToList();
    }
}
