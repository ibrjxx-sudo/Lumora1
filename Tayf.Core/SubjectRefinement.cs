namespace Tayf.Core;
public static class SubjectRefinement
{
    public static RemovalOperation SourceStroke(Recipe recipe,RemovalOperation stroke)
    {
        stroke.Validate();if(recipe.Subject is null||recipe.Get("subjectCutout")<=0)return stroke;
        double scale=Math.Clamp(recipe.Get("subjectScale"),.1,4);var bounds=PortraitEffects.SubjectBounds(recipe.Subject);
        double dx=(1-scale)*(bounds[0]+bounds[2]/2)+recipe.Get("subjectX"),dy=(1-scale)*(bounds[1]+bounds[3]/2)+recipe.Get("subjectY");
        var result=new RemovalOperation{Size=Math.Clamp(stroke.Size/scale,.001,2)};
        foreach(var p in stroke.Points)
        {double x=(p[0]-dx)/scale,y=(p[1]-dy)/scale;if(x<0||x>1||y<0||y>1)continue;result.Points.Add([x,y]);}
        return result;
    }
    public static SubjectMatte Edges(ImageFrame image,SubjectMatte matte,CancellationToken token=default)
    {
        var guide=ImageIO.Stretch(image,matte.Width,matte.Height);var source=matte.Decode();var result=(byte[])source.Clone();int w=matte.Width,h=matte.Height;
        Parallel.For(0,h,new ParallelOptions{CancellationToken=token},y=>
        {
            for(int x=0;x<w;x++)
            {
                int at=y*w+x;byte a=source[at];if(a<=3){result[at]=0;continue;}if(a>=252){result[at]=255;continue;}
                double sum=0,weights=0;int center=at*4;
                for(int yy=Math.Max(0,y-2);yy<=Math.Min(h-1,y+2);yy++)for(int xx=Math.Max(0,x-2);xx<=Math.Min(w-1,x+2);xx++)
                {
                    int p=(yy*w+xx)*4;double d=0;for(int c=0;c<3;c++){double delta=guide.Pixels[p+c]-guide.Pixels[center+c];d+=delta*delta;}
                    double weight=1/(1+d/600+((xx-x)*(xx-x)+(yy-y)*(yy-y))*.2);sum+=source[yy*w+xx]*weight;weights+=weight;
                }
                result[at]=(byte)Math.Clamp(Math.Round(a*.35+sum/weights*.65),0,255);
            }
        });return SubjectMatte.Create(w,h,result);
    }
    public static SubjectMatte Stroke(SubjectMatte matte,RemovalOperation stroke,bool foreground,double aspect=1,CancellationToken token=default)
    {
        if(!double.IsFinite(stroke.Size)||stroke.Size is <.001 or >2||stroke.Points.Count>15000||stroke.Points.Any(p=>p.Length!=2||p.Any(v=>!double.IsFinite(v)||v<0||v>1)))throw new ArgumentException("Invalid subject brush stroke.");
        if(!double.IsFinite(aspect)||aspect<=0)throw new ArgumentOutOfRangeException(nameof(aspect));
        var pixels=(byte[])matte.Decode().Clone();int w=matte.Width,h=matte.Height;double rx=Math.Max(1,stroke.Size*w),ry=Math.Max(1,stroke.Size*aspect*h);
        void Dab(double x,double y)
        {
            token.ThrowIfCancellationRequested();double cx=x*w,cy=y*h;
            for(int yy=Math.Max(0,(int)(cy-ry-1));yy<=Math.Min(h-1,(int)(cy+ry+1));yy++)for(int xx=Math.Max(0,(int)(cx-rx-1));xx<=Math.Min(w-1,(int)(cx+rx+1));xx++)
            {double distance=Math.Sqrt(Math.Pow((xx+.5-cx)/rx,2)+Math.Pow((yy+.5-cy)/ry,2));if(distance>1)continue;double strength=Math.Clamp((1-distance)*Math.Min(rx,ry),0,1);int i=yy*w+xx;pixels[i]=(byte)Math.Round(pixels[i]*(1-strength)+(foreground?255:0)*strength);}
        }
        double[]? previous=null;var breaks=stroke.Breaks.ToHashSet();
        for(int i=0;i<stroke.Points.Count;i++)
        {
            var p=stroke.Points[i];if(previous is not null&&!breaks.Contains(i)){double dx=p[0]-previous[0],dy=p[1]-previous[1];int steps=(int)Math.Ceiling(Math.Sqrt(dx*dx*w*w+dy*dy*h*h)/Math.Max(.5,Math.Min(rx,ry)/3));for(int step=1;step<steps;step++)Dab(previous[0]+dx*step/steps,previous[1]+dy*step/steps);}
            Dab(p[0],p[1]);previous=p;
        }
        return SubjectMatte.Create(w,h,pixels);
    }
}
