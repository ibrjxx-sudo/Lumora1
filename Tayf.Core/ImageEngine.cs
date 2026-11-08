namespace Tayf.Core;

/// <summary>Independent C# raster pipeline. No JavaScript, browser, or Adobe processing code.</summary>
public static class ImageEngine
{
    static readonly HashSet<string> ColorKeys=["exposure","contrast","highlights","shadows","whites","blacks","temperature","tint","saturation","vibrance","vignette","midpoint","feather","roundness","vignetteHighlights","lensVignette","grain","grainSize","grainRoughness","dehaze","keyEnabled"];
    public static bool HasColorEdits(Recipe p)=>p.Values.Any(v=>ColorKeys.Contains(v.Key)&&v.Value!=Recipe.Defaults.GetValueOrDefault(v.Key))||p.Hsl.Any(v=>v.H!=0||v.S!=0||v.L!=0)||p.Grading.Values.Any(v=>v.S!=0||v.L!=0)||p.Curves.Values.Any(v=>v.Count!=2||v[0]!=new CurvePoint(0,0)||v[1]!=new CurvePoint(1,1))||p.Masks.Count>0;
    static double Clamp(double x,double a=0,double b=1)=>Math.Clamp(x,a,b);
    static byte Byte(double x)=>(byte)Math.Clamp((int)Math.Round(x),0,255);
    static double Linear(double x)=>x<=.04045?x/12.92:Math.Pow((x+.055)/1.055,2.4);
    static double Encoded(double x)=>x<=.0031308?x*12.92:1.055*Math.Pow(Math.Max(0,x),1/2.4)-.055;
    static double Luma(double r,double g,double b)=>r*.2126+g*.7152+b*.0722;
    static double Smooth(double a,double b,double x){var t=Clamp((x-a)/(Math.Abs(b-a)<1e-9?1:b-a));return t*t*(3-2*t);}
    public static ImageFrame Render(ImageFrame input,Recipe recipe,CancellationToken cancellation=default)
    {
        input.Validate(); recipe.Validate(); cancellation.ThrowIfCancellationRequested();
        input=ObjectRemoval.Apply(input,recipe.Removals,cancellation);
        var pixels=(byte[])input.Pixels.Clone();
        if(input.LinearRgb is not null)pixels=ColorPass(pixels,input.Width,input.Height,recipe,cancellation,input.LinearRgb);
        pixels=Retouch(pixels,input.Width,input.Height,recipe.Retouch,cancellation);
        pixels=Optics(pixels,input.Width,input.Height,recipe,cancellation);
        if(input.LinearRgb is null)pixels=ColorPass(pixels,input.Width,input.Height,recipe,cancellation);
        pixels=Detail(pixels,input.Width,input.Height,recipe,cancellation);
        pixels=BeautyEffects.Apply(new(input.Width,input.Height,pixels),recipe,cancellation).Pixels;
        pixels=PortraitEffects.Apply(new(input.Width,input.Height,pixels),recipe,cancellation).Pixels;
        return Geometry(pixels,input.Width,input.Height,recipe,cancellation);
    }
    public static (double H,double S,double L) RgbToHsl(double r,double g,double b)
    {
        var max=Math.Max(r,Math.Max(g,b));var min=Math.Min(r,Math.Min(g,b));var d=max-min;var l=(max+min)/2;
        if(d<1e-8)return(0,0,l);
        double h=max==r?((g-b)/d)%6:max==g?(b-r)/d+2:(r-g)/d+4;
        return((h*60+360)%360,d/(1-Math.Abs(2*l-1)),l);
    }
    public static (double R,double G,double B) HslToRgb(double h,double s,double l)
    {
        h=((h%360)+360)%360;var c=(1-Math.Abs(2*l-1))*s;var x=c*(1-Math.Abs((h/60)%2-1));var m=l-c/2;
        var v=h<60?(c,x,0d):h<120?(x,c,0d):h<180?(0d,c,x):h<240?(0d,x,c):h<300?(x,0d,c):(c,0d,x);
        return(v.Item1+m,v.Item2+m,v.Item3+m);
    }
    static float[] CurveLut(List<CurvePoint> points)
    {
        var p=points.OrderBy(p=>p.X).ToArray();var lut=new float[4096];
        for(int i=0;i<lut.Length;i++){var x=i/4095d;double y=p[^1].Y;if(x<=p[0].X)y=p[0].Y;else for(int j=1;j<p.Length;j++)if(x<=p[j].X){y=p[j-1].Y+(p[j].Y-p[j-1].Y)*(x-p[j-1].X)/Math.Max(.000001,p[j].X-p[j-1].X);break;}lut[i]=(float)y;}return lut;
    }
    static double Lookup(float[] lut,double x)=>lut[(int)Math.Round(Clamp(x)*4095)];
    public static double MaskWeight(MaskRecipe m,double x,double y,double r,double g,double b)
    {
        double weight=0,f=Clamp(m.Feather,.01,1),size=Math.Max(.005,m.Size);
        switch(m.Type)
        {
            case "radial":var d=Math.Sqrt(Math.Pow((x-m.X)/Math.Max(.01,m.Rx),2)+Math.Pow((y-m.Y)/Math.Max(.01,m.Ry),2));weight=1-Smooth(1-f,1,d);break;
            case "linear":weight=Clamp(((x-m.X)*(m.X2-m.X)+(y-m.Y)*(m.Y2-m.Y))/Math.Max(.000001,Math.Pow(m.X2-m.X,2)+Math.Pow(m.Y2-m.Y,2)));break;
            case "brush":foreach(var point in m.Points){var distance=Math.Sqrt(Math.Pow(x-point[0],2)+Math.Pow(y-point[1],2));weight=Math.Max(weight,1-Smooth(size*(1-f),size,distance));}break;
            case "luminance":var l=Luma(r,g,b);weight=Smooth(m.Min-f*.2,m.Min,l)*(1-Smooth(m.Max,m.Max+f*.2,l));break;
            case "color":var diff=Math.Sqrt(Math.Pow(r-m.Color[0],2)+Math.Pow(g-m.Color[1],2)+Math.Pow(b-m.Color[2],2));weight=1-Smooth(m.Tolerance,m.Tolerance+f*.2,diff);break;
        }
        if(m.Invert)weight=1-weight;return Clamp(weight*m.Opacity);
    }
    static byte[] ColorPass(byte[] data,int w,int h,Recipe p,CancellationToken token,ushort[]? raw=null)
    {
        if(!HasColorEdits(p)&&raw is null)return data;
        var output=new byte[data.Length];var luts=p.Curves.ToDictionary(k=>k.Key,k=>CurveLut(k.Value));
        double exposure=Math.Pow(2,Clamp(p.Get("exposure"),-5,5)),contrast=1+p.Get("contrast")/200,high=p.Get("highlights")/100,shadow=p.Get("shadows")/100,whites=p.Get("whites")/100,blacks=p.Get("blacks")/100,temp=p.Get("temperature"),tint=p.Get("tint"),sat=p.Get("saturation")/100,vibrance=p.Get("vibrance")/100;
        double vignette=p.Get("vignette"),mid=p.Get("midpoint")/120,feather=p.Get("feather")/90+.01,shape=Clamp(2+p.Get("roundness")/50,.5,5),protect=p.Get("vignetteHighlights")/100,lensVignette=p.Get("lensVignette"),grain=p.Get("grain"),grainSize=Math.Max(1,p.Get("grainSize")),rough=p.Get("grainRoughness"),dehaze=p.Get("dehaze")/500;
        bool hasHsl=p.Hsl.Any(v=>v.H!=0||v.S!=0||v.L!=0),hasGrade=p.Grading.Values.Any(v=>v.S!=0||v.L!=0),key=p.Get("keyEnabled")>0;
        double[] centers=[0,30,60,120,180,240,275,315];
        Parallel.For(0,h,new ParallelOptions{CancellationToken=token},y=>
        {
            for(int x=0;x<w;x++)
            {
                int i=(y*w+x)*4;double r=data[i]/255d,g=data[i+1]/255d,b=data[i+2]/255d,alpha=data[i+3];
                var or=r;var og=g;var ob=b;int ri=(y*w+x)*3;double nx=(x+.5)/w,ny=(y+.5)/h,lr=raw is null?Linear(r):raw[ri]/65535d,lg=raw is null?Linear(g):raw[ri+1]/65535d,lb=raw is null?Linear(b):raw[ri+2]/65535d,l=Luma(lr,lg,lb),sw=1-Smooth(.02,.42,l),hw=Smooth(.2,.95,l);
                double tone=exposure*Math.Pow(2,shadow*sw*.65+high*hw*.65+whites*hw*hw*.45+blacks*sw*sw*.45);
                lr*=tone*(1+temp/400+tint/800);lg*=tone*(1-tint/500);lb*=tone*(1-temp/400+tint/800);
                r=Clamp(Encoded((lr-.18)*contrast+.18));g=Clamp(Encoded((lg-.18)*contrast+.18));b=Clamp(Encoded((lb-.18)*contrast+.18));
                double av=Luma(r,g,b),range=Math.Max(r,Math.Max(g,b))-Math.Min(r,Math.Min(g,b)),saturation=Math.Max(0,1+sat+vibrance*(1-range));r=Clamp(av+(r-av)*saturation);g=Clamp(av+(g-av)*saturation);b=Clamp(av+(b-av)*saturation);
                if(hasHsl){var(hh,ss,ll)=RgbToHsl(r,g,b);double dh=0,ds=0,dl=0;for(int j=0;j<8;j++){double distance=Math.Min(Math.Abs(hh-centers[j]),360-Math.Abs(hh-centers[j])),weight=Math.Max(0,1-distance/42);dh+=p.Hsl[j].H*weight*.5;ds+=p.Hsl[j].S*weight/100;dl+=p.Hsl[j].L*weight/200;}(r,g,b)=HslToRgb(hh+dh,Clamp(ss*(1+ds)),Clamp(ll+dl*ss));}
                if(hasGrade){var yv=Luma(r,g,b);var balance=p.Get("gradingBalance")/300;var blend=Clamp(p.Get("gradingBlending")/100,.01,1);foreach(var zone in p.Grading){double weight=zone.Key switch{"shadows"=>1-Smooth(.1+balance,.5+balance,yv),"midtones"=>1-Math.Abs(yv-.5)*2,"highlights"=>Smooth(.45+balance,.95+balance,yv),_=>1};var(cr,cg,cb)=HslToRgb(zone.Value.H,1,.5);double strength=zone.Value.S/100*weight*(.1+.25*blend),luminance=zone.Value.L/300*weight;r=Clamp(r+(cr-.5)*strength+luminance);g=Clamp(g+(cg-.5)*strength+luminance);b=Clamp(b+(cb-.5)*strength+luminance);}}
                r=Lookup(luts["r"],Lookup(luts["rgb"],r));g=Lookup(luts["g"],Lookup(luts["rgb"],g));b=Lookup(luts["b"],Lookup(luts["rgb"],b));
                if(dehaze!=0){r=Clamp((r-dehaze)/(1-2*dehaze));g=Clamp((g-dehaze)/(1-2*dehaze));b=Clamp((b-dehaze)/(1-2*dehaze));}
                double distanceV=Math.Pow(Math.Pow(Math.Abs((nx-.5)*2),shape)+Math.Pow(Math.Abs((ny-.5)*2),shape),1/shape);
                if(vignette!=0||lensVignette!=0){double weight=Smooth(mid,mid+feather,distanceV),protection=1-Smooth(.5,1,Luma(r,g,b))*protect,gain=1+vignette/120*weight*protection+lensVignette/160*distanceV*distanceV;r=Clamp(r*gain);g=Clamp(g*gain);b=Clamp(b*gain);}
                foreach(var mask in p.Masks){double weight=MaskWeight(mask,nx,ny,or,og,ob),ex=Math.Pow(2,Clamp(mask.Exposure,-3,3)),c=1+mask.Contrast/200,s=1+mask.Saturation/100,brightness=Luma(r,g,b),mr=Clamp((r*ex-.5)*c+.5),mg=Clamp((g*ex-.5)*c+.5),mb=Clamp((b*ex-.5)*c+.5);r=r*(1-weight)+Clamp(brightness+(mr-brightness)*s)*weight;g=g*(1-weight)+Clamp(brightness+(mg-brightness)*s)*weight;b=b*(1-weight)+Clamp(brightness+(mb-brightness)*s)*weight;}
                if(grain>0){uint gx=(uint)(x/grainSize),gy=(uint)(y/grainSize),seed=unchecked((gx+1)*374761393u^(gy+1)*668265263u);seed=unchecked((seed^(seed>>13))*1274126177u);double noise=((seed%65536)/65535d-.5)*grain/650*(.5+rough/100);r=Clamp(r+noise);g=Clamp(g+noise);b=Clamp(b+noise);}
                if(key){double d=Math.Sqrt(Math.Pow(or*255-p.KeyColor[0],2)+Math.Pow(og*255-p.KeyColor[1],2)+Math.Pow(ob*255-p.KeyColor[2],2)),f=p.Get("keyFeather"),factor=f>0?Clamp((d-p.Get("keyTolerance"))/f):(d<=p.Get("keyTolerance")?0:1);alpha*=factor;if(p.Background is not null){double a=alpha/255;r=r*a+p.Background[0]/255*(1-a);g=g*a+p.Background[1]/255*(1-a);b=b*a+p.Background[2]/255*(1-a);alpha=255;}}
                output[i]=Byte(r*255);output[i+1]=Byte(g*255);output[i+2]=Byte(b*255);output[i+3]=Byte(alpha);
            }
        });return output;
    }
    static byte[] Gaussian(byte[] input,int w,int h,double radius,CancellationToken token)
    {
        int r=Math.Min(30,Math.Max(1,(int)Math.Ceiling(radius*2)));double sigma=Math.Max(.5,radius);var kernel=Enumerable.Range(0,r*2+1).Select(i=>Math.Exp(-Math.Pow(i-r,2)/(2*sigma*sigma))).ToArray();double sum=kernel.Sum();for(int i=0;i<kernel.Length;i++)kernel[i]/=sum;
        var temp=new float[w*h*3];var output=(byte[])input.Clone();var options=new ParallelOptions{CancellationToken=token};
        Parallel.For(0,h,options,y=>{for(int x=0;x<w;x++)for(int c=0;c<3;c++){double v=0;for(int k=-r;k<=r;k++)v+=input[(y*w+Math.Clamp(x+k,0,w-1))*4+c]*kernel[k+r];temp[(y*w+x)*3+c]=(float)v;}});
        Parallel.For(0,h,options,y=>{for(int x=0;x<w;x++)for(int c=0;c<3;c++){double v=0;for(int k=-r;k<=r;k++)v+=temp[(Math.Clamp(y+k,0,h-1)*w+x)*3+c]*kernel[k+r];output[(y*w+x)*4+c]=Byte(v);}});return output;
    }
    static byte[] Detail(byte[] input,int w,int h,Recipe p,CancellationToken token)
    {
        var data=input;var options=new ParallelOptions{CancellationToken=token};double luma=p.Get("luminanceNoise"),chroma=p.Get("colorNoise");
        if(luma>0||chroma>0){var blurred=Gaussian(data,w,h,1.2,token);Parallel.For(0,h,options,y=>{for(int x=0;x<w;x++){int i=(y*w+x)*4;double a=Luma(data[i],data[i+1],data[i+2]),b=Luma(blurred[i],blurred[i+1],blurred[i+2]),preserve=1-Smooth(6,35,Math.Abs(a-b)),lw=luma/100*.8*preserve,cw=chroma/100*.85;for(int c=0;c<3;c++)data[i+c]=Byte(data[i+c]+(b-a)*lw+((blurred[i+c]-b)-(data[i+c]-a))*cw);}});}
        foreach(var(amount,radius,edge) in new[]{(p.Get("sharpen")/100,p.Get("radius"),p.Get("masking")),(p.Get("texture")/160,.7,20d),(p.Get("clarity")/180,5d,0d)})
        {
            if(amount==0)continue;var blurred=Gaussian(data,w,h,radius,token);Parallel.For(0,h,options,y=>{for(int x=0;x<w;x++){int i=(y*w+x)*4;double difference=Luma(data[i]-blurred[i],data[i+1]-blurred[i+1],data[i+2]-blurred[i+2]),weight=edge>0?Smooth(0,edge/5+1,Math.Abs(difference)):1;for(int c=0;c<3;c++)data[i+c]=Byte(data[i+c]+amount*(data[i+c]-blurred[i+c])*weight);}});
        }
        return p.Get("blur")>0?Gaussian(data,w,h,Math.Min(20,p.Get("blur")),token):data;
    }
    static void Sample(byte[] data,int w,int h,double x,double y,byte[] output,int offset)
    {
        if(x<0||y<0||x>w-1||y>h-1)return;int ax=(int)x,ay=(int)y,bx=Math.Min(w-1,ax+1),by=Math.Min(h-1,ay+1);double fx=x-ax,fy=y-ay;
        for(int c=0;c<4;c++)output[offset+c]=Byte((1-fy)*((1-fx)*data[(ay*w+ax)*4+c]+fx*data[(ay*w+bx)*4+c])+fy*((1-fx)*data[(by*w+ax)*4+c]+fx*data[(by*w+bx)*4+c]));
    }
    static byte[] Optics(byte[] data,int w,int h,Recipe p,CancellationToken token)
    {
        double distortion=p.Get("distortion"),vertical=p.Get("vertical"),horizontal=p.Get("horizontal");if(distortion==0&&vertical==0&&horizontal==0)return data;
        var result=new byte[data.Length];Parallel.For(0,h,new ParallelOptions{CancellationToken=token},y=>{for(int x=0;x<w;x++){double nx=(x+.5)/w*2-1,ny=(y+.5)/h*2-1,perspective=1+ny*vertical/400+nx*horizontal/400;nx/=perspective;ny/=perspective;double radial=1+distortion/300*(nx*nx+ny*ny);Sample(data,w,h,(nx*radial+1)*w/2-.5,(ny*radial+1)*h/2-.5,result,(y*w+x)*4);}});return result;
    }
    static byte[] Retouch(byte[] input,int w,int h,List<RetouchStroke> strokes,CancellationToken token)
    {
        if(strokes.Count==0)return input;var output=(byte[])input.Clone();
        foreach(var stroke in strokes){token.ThrowIfCancellationRequested();double radius=Math.Max(1,Math.Min(.3,stroke.Size)*Math.Min(w,h)),dx=stroke.X*w,dy=stroke.Y*h,sx=stroke.Sx*w,sy=stroke.Sy*h;double[] targetMean=[0,0,0],sourceMean=[0,0,0];
            if(stroke.Type=="heal")for(int n=0;n<12;n++){double angle=n*Math.PI/6,rx=Math.Cos(angle)*radius*.9,ry=Math.Sin(angle)*radius*.9;int ti=(Math.Clamp((int)Math.Round(dy+ry),0,h-1)*w+Math.Clamp((int)Math.Round(dx+rx),0,w-1))*4,si=(Math.Clamp((int)Math.Round(sy+ry),0,h-1)*w+Math.Clamp((int)Math.Round(sx+rx),0,w-1))*4;for(int c=0;c<3;c++){targetMean[c]+=input[ti+c]/12d;sourceMean[c]+=input[si+c]/12d;}}
            for(int y=Math.Max(0,(int)(dy-radius));y<Math.Min(h,dy+radius);y++)for(int x=Math.Max(0,(int)(dx-radius));x<Math.Min(w,dx+radius);x++){double distance=Math.Sqrt(Math.Pow(x-dx,2)+Math.Pow(y-dy,2))/radius;if(distance>1)continue;double weight=(1-Smooth(1-stroke.Feather,1,distance))*stroke.Opacity;int tx=(int)Math.Round(sx+x-dx),ty=(int)Math.Round(sy+y-dy);if(tx<0||ty<0||tx>=w||ty>=h)continue;int i=(y*w+x)*4,j=(ty*w+tx)*4;for(int c=0;c<3;c++)output[i+c]=Byte(output[i+c]*(1-weight)+Clamp(input[j+c]+targetMean[c]-sourceMean[c],0,255)*weight);}
        }return output;
    }
    static ImageFrame Geometry(byte[] data,int w,int h,Recipe p,CancellationToken token)
    {
        if(p.Crop.SequenceEqual(new[]{0d,0d,1d,1d})&&(p.Get("rotation")+p.Get("quarterTurns")*90)%360==0&&p.Get("flipX")==0&&p.Get("flipY")==0)return new(w,h,data);
        int sx=Math.Min(w-1,(int)Math.Round(p.Crop[0]*w)),sy=Math.Min(h-1,(int)Math.Round(p.Crop[1]*h)),sw=Math.Max(1,Math.Min(w-sx,(int)Math.Round(p.Crop[2]*w))),sh=Math.Max(1,Math.Min(h-sy,(int)Math.Round(p.Crop[3]*h)));
        double angle=(p.Get("rotation")+p.Get("quarterTurns")*90)*Math.PI/180,c=Math.Cos(angle),s=Math.Sin(angle);int ow=Math.Max(1,(int)Math.Round(Math.Abs(sw*c)+Math.Abs(sh*s))),oh=Math.Max(1,(int)Math.Round(Math.Abs(sw*s)+Math.Abs(sh*c)));var output=new byte[checked(ow*oh*4)];
        Parallel.For(0,oh,new ParallelOptions{CancellationToken=token},y=>{for(int x=0;x<ow;x++){double dx=x-(ow-1)/2d,dy=y-(oh-1)/2d,rx=c*dx+s*dy,ry=-s*dx+c*dy;if(p.Get("flipX")>0)rx=-rx;if(p.Get("flipY")>0)ry=-ry;if(Math.Abs(rx)>(sw-.01)/2||Math.Abs(ry)>(sh-.01)/2)continue;Sample(data,w,h,Clamp(rx+(sw-1)/2d+sx,sx,sx+sw-1),Clamp(ry+(sh-1)/2d+sy,sy,sy+sh-1),output,(y*ow+x)*4);}});return new(ow,oh,output);
    }
    public static int[][] Histogram(ImageFrame input){int[][] bins=[new int[256],new int[256],new int[256]];for(int i=0;i<input.Pixels.Length;i+=4){if(input.Pixels[i+3]==0)continue;for(int c=0;c<3;c++)bins[c][input.Pixels[i+c]]++;}return bins;}
    public static void AutoLight(ImageFrame input,Recipe recipe){var values=new List<double>();int step=Math.Max(4,input.Pixels.Length/40000/4*4);for(int i=0;i<input.Pixels.Length;i+=step)if(input.Pixels[i+3]>0)values.Add(Luma(Linear(input.Pixels[i]/255d),Linear(input.Pixels[i+1]/255d),Linear(input.Pixels[i+2]/255d)));values.Sort();double median=values.Count>0?values[values.Count/2]:.18;recipe.Set("exposure",Clamp(Math.Log2(.18/Math.Max(.005,median)),-2,2));recipe.Set("shadows",15);recipe.Set("highlights",-15);}
    public static void AutoWhiteBalance(ImageFrame input,Recipe recipe){double r=0,g=0,b=0;for(int i=0;i<input.Pixels.Length;i+=16){r+=input.Pixels[i];g+=input.Pixels[i+1];b+=input.Pixels[i+2];}recipe.Set("temperature",Clamp((b-r)/Math.Max(1,r+b)*200,-100,100));recipe.Set("tint",Clamp(((r+b)/2-g)/Math.Max(1,g)*180,-100,100));}
}
