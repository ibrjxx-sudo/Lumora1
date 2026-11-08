namespace Tayf.Core;
public static class BeautyEffects
{
    static byte Byte(double v)=>(byte)Math.Clamp((int)Math.Round(v),0,255);
    static double Smooth(double a,double b,double x){double t=Math.Clamp((x-a)/(b-a),0,1);return t*t*(3-2*t);}
    public static ImageFrame Apply(ImageFrame input,Recipe p,CancellationToken token=default)
    {
        var f=p.Face;if(f is null)return input;f.Validate();string[] keys=["skinSmooth","blemish","faceSlim","jaw","chin","eyeSize","eyeDistance","eyeLift","noseWidth","noseLength","mouthWidth","redEye","eyeReflection"];if(!keys.Any(k=>p.Get(k)!=0))return input;
        int w=input.Width,h=input.Height;var data=(byte[])input.Pixels.Clone();double fx=f.Bounds[0],fy=f.Bounds[1],fw=f.Bounds[2],fh=f.Bounds[3],cx=fx+fw/2,cy=fy+fh/2;
        double Weight(double x,double y,double px,double py,double rx,double ry){double d=Math.Sqrt(Math.Pow((x-px)/Math.Max(.001,rx),2)+Math.Pow((y-py)/Math.Max(.001,ry),2));return Math.Pow(Math.Max(0,1-d*d),2);}
        double[] Sample(byte[] bytes,double x,double y){x=Math.Clamp(x,0,w-1);y=Math.Clamp(y,0,h-1);int x0=(int)x,y0=(int)y,x1=Math.Min(w-1,x0+1),y1=Math.Min(h-1,y0+1);double dx=x-x0,dy=y-y0;var v=new double[4];for(int c=0;c<4;c++)v[c]=(bytes[(y0*w+x0)*4+c]*(1-dx)+bytes[(y0*w+x1)*4+c]*dx)*(1-dy)+(bytes[(y1*w+x0)*4+c]*(1-dx)+bytes[(y1*w+x1)*4+c]*dx)*dy;return v;}
        bool warp=keys.Skip(2).Take(9).Any(k=>p.Get(k)!=0);
        if(warp)Parallel.For(0,h,new ParallelOptions{CancellationToken=token},y=>{for(int x=0;x<w;x++)
        {
            double nx=(x+.5)/w,ny=(y+.5)/h,sx=nx,sy=ny;
            double face=Weight(nx,ny,cx,cy,fw*.65,fh*.66),lower=Smooth(fy+fh*.35,fy+fh*.8,ny);sx+=(nx-cx)*(Math.Clamp(p.Get("faceSlim"),-30,30)/150*face+Math.Clamp(p.Get("jaw"),-20,20)/130*face*lower);sy-=Math.Clamp(p.Get("chin"),-15,15)/100*fh*.18*Weight(nx,ny,cx,fy+fh*.86,fw*.4,fh*.3);
            for(int eye=0;eye<2;eye++){var e=f.Points[eye];double a=Weight(nx,ny,e[0],e[1],fw*.20,fh*.13);sx+=(nx-e[0])*a*(-Math.Clamp(p.Get("eyeSize"),-25,25)/120);sy+=(ny-e[1])*a*(-Math.Clamp(p.Get("eyeSize"),-25,25)/120);sx-=(e[0]<cx?-1:1)*Math.Clamp(p.Get("eyeDistance"),-15,15)/100*fw*.16*a;sy+=Math.Clamp(p.Get("eyeLift"),-10,10)/100*fh*.15*a;}
            var nose=f.Points[2];double nw=Weight(nx,ny,nose[0],nose[1],fw*.22,fh*.27);sx+=(nx-nose[0])*Math.Clamp(p.Get("noseWidth"),-30,30)/100*nw;sy+=(ny-nose[1])*Math.Clamp(p.Get("noseLength"),-20,20)/130*nw;
            double mx=(f.Points[3][0]+f.Points[4][0])/2,my=(f.Points[3][1]+f.Points[4][1])/2;double mw=Weight(nx,ny,mx,my,fw*.28,fh*.14);sx+=(nx-mx)*(-Math.Clamp(p.Get("mouthWidth"),-20,20)/120)*mw;
            if(Math.Abs(sx-nx)+Math.Abs(sy-ny)<1e-10)continue;var s=Sample(input.Pixels,sx*w-.5,sy*h-.5);int at=(y*w+x)*4;for(int c=0;c<4;c++)data[at+c]=Byte(s[c]);
        }});
        byte[] source=(byte[])data.Clone();int radius=Math.Clamp((int)Math.Round(Math.Min(w,h)/500d),1,4);double smooth=Math.Clamp(p.Get("skinSmooth")/100,0,1),blemish=Math.Clamp(p.Get("blemish")/100,0,1);
        if(smooth>0||blemish>0)Parallel.For(0,h,new ParallelOptions{CancellationToken=token},y=>{for(int x=0;x<w;x++)
        {
            double nx=(x+.5)/w,ny=(y+.5)/h,face=Weight(nx,ny,cx,cy,fw*.52,fh*.54);if(face<=0)continue;double protect=0;for(int j=0;j<2;j++)protect=Math.Max(protect,Weight(nx,ny,f.Points[j][0],f.Points[j][1],fw*.23,fh*.15));protect=Math.Max(protect,Weight(nx,ny,(f.Points[3][0]+f.Points[4][0])/2,(f.Points[3][1]+f.Points[4][1])/2,fw*.3,fh*.13));if(protect>.4)continue;
            int i=(y*w+x)*4;double sum=0;double[] avg=new double[3],plain=new double[3];int count=0;for(int dy=-radius;dy<=radius;dy++)for(int dx=-radius;dx<=radius;dx++){int j=(Math.Clamp(y+dy,0,h-1)*w+Math.Clamp(x+dx,0,w-1))*4;double diff=0;for(int c=0;c<3;c++){diff+=Math.Pow(source[j+c]-source[i+c],2);plain[c]+=source[j+c];}double wt=Math.Exp(-diff/1100d-(dx*dx+dy*dy)/(double)(radius*radius*2));sum+=wt;count++;for(int c=0;c<3;c++)avg[c]+=source[j+c]*wt;}
            for(int c=0;c<3;c++){avg[c]/=Math.Max(1e-6,sum);plain[c]/=count;}double chroma=Math.Abs((source[i]-source[i+1])-(plain[0]-plain[1])),delta=Math.Abs((source[i]+source[i+1]+source[i+2])/3d-plain.Average()),spot=Smooth(9,25,delta)*Smooth(4,20,chroma+delta*.35);double a=smooth*.75*face*(1-protect),b=blemish*spot*face*(1-protect);for(int c=0;c<3;c++)data[i+c]=Byte(source[i+c]*(1-a-b*.65)+avg[c]*a+plain[c]*b*.65);
        }});
        if(p.Get("redEye")>0||p.Get("eyeReflection")>0)for(int eye=0;eye<2;eye++)
        {
            var e=f.Points[eye];double rx=fw*.055,ry=Math.Max(fh*.025,rx*w/h*.55);int left=Math.Max(0,(int)((e[0]-rx)*w)),right=Math.Min(w-1,(int)((e[0]+rx)*w)),top=Math.Max(0,(int)((e[1]-ry)*h)),bottom=Math.Min(h-1,(int)((e[1]+ry)*h));
            var dark=new List<double[]>();for(int y=top;y<=bottom;y++)for(int x=left;x<=right;x++){int i=(y*w+x)*4;double lum=(data[i]+data[i+1]+data[i+2])/3d;if(lum<135)dark.Add([data[i],data[i+1],data[i+2]]);}double[] iris=dark.Count>0?Enumerable.Range(0,3).Select(c=>dark.Select(v=>v[c]).Order().ElementAt(dark.Count/2)).ToArray():[45,35,30];
            for(int y=top;y<=bottom;y++)for(int x=left;x<=right;x++){token.ThrowIfCancellationRequested();int i=(y*w+x)*4;double wt=Weight((x+.5)/w,(y+.5)/h,e[0],e[1],rx,ry),r=data[i],g=data[i+1],b=data[i+2];double red=Smooth(1.2,2.1,r/Math.Max(15,(g+b)/2))*Smooth(55,140,r)*Math.Clamp(p.Get("redEye")/100,0,1)*wt;if(red>0){data[i]=Byte(r*(1-red)+(g+b)/2*red);}double glare=Smooth(175,245,Math.Min(r,Math.Min(g,b)))*Math.Clamp(p.Get("eyeReflection")/100,0,1)*wt*.85;for(int c=0;c<3;c++)data[i+c]=Byte(data[i+c]*(1-glare)+iris[c]*glare);}
        }
        return new(w,h,data);
    }
}
public static class SmartEnhance
{
    public static void Apply(ImageFrame input,Recipe p,double strength=1)
    {
        var auto=new Recipe();ImageEngine.AutoLight(input,auto);ImageEngine.AutoWhiteBalance(input,auto);strength=Math.Clamp(strength,0,1);foreach(var pair in auto.Values)p.Set(pair.Key,p.Get(pair.Key)*(1-strength)+pair.Value*strength);
        p.Set("exposure",Math.Clamp(p.Get("exposure"),-1.5,1.5));p.Set("highlights",Math.Min(-12,p.Get("highlights")));p.Set("shadows",Math.Max(14,p.Get("shadows")));p.Set("sharpen",42*strength);p.Set("masking",60);p.Set("radius",.8);p.Set("texture",10*strength);p.Set("colorNoise",18*strength);
        var sample=ImageIO.Resize(input,350);double noise=0;int n=0;for(int y=1;y<sample.Height-1;y+=2)for(int x=1;x<sample.Width-1;x+=2){int i=(y*sample.Width+x)*4;double center=sample.Pixels[i+1],a=(sample.Pixels[i-4+1]+sample.Pixels[i+4+1]+sample.Pixels[i-sample.Width*4+1]+sample.Pixels[i+sample.Width*4+1])/4d;if(Math.Abs(center-a)<25){noise+=Math.Abs(center-a);n++;}}p.Set("luminanceNoise",Math.Clamp(noise/Math.Max(1,n)*3,5,35)*strength);
    }
}
