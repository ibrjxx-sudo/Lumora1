using SkiaSharp;
using System.IO;
using System.Runtime.InteropServices;

namespace Tayf.Core;

public static class PortraitEffects
{
    public static ImageFrame Apply(ImageFrame frame,Recipe recipe,CancellationToken token)
    {
        double blur=recipe.Get("portraitBlur");bool cutout=recipe.Get("subjectCutout")>0;
        if((blur<=0&&!cutout)||recipe.Subject is null)return frame;
        if(cutout)return Compose(frame,recipe,token);
        var matte=recipe.Subject;_ = matte.Decode();int w=frame.Width,h=frame.Height;
        ImageFrame? background=null;
        if(cutout&&recipe.BackgroundImage is string path)
        {
            if(!File.Exists(path))throw new FileNotFoundException("صورة الخلفية غير موجودة. اخترها مجددًا.",path);
            background=ImageIO.Load(path,Math.Max(w,h));
        }
        var output=(byte[])frame.Pixels.Clone();byte[]? soft=null;
        if(blur>0&&!cutout)
        {
            // Normalised background blur: foreground RGB contributes zero to the blur.
            var isolated=(byte[])frame.Pixels.Clone();
            for(int y=0;y<h;y++)for(int x=0;x<w;x++){int i=(y*w+x)*4;double alpha=1-Weight(matte,x,y,w,h,recipe);isolated[i+3]=(byte)Math.Round(alpha*255);}
            soft=Blur(new(w,h,isolated),Math.Max(.3,blur/100*Math.Min(w,h)*.04)).Pixels;
        }
        Parallel.For(0,h,new ParallelOptions{CancellationToken=token},y=>
        {
            for(int x=0;x<w;x++)
            {
                int i=(y*w+x)*4;double mask=Weight(matte,x,y,w,h,recipe),alpha=frame.Pixels[i+3]/255d;
                if(cutout)
                {
                    double foreground=mask*alpha;
                    if(background is not null||recipe.Background is not null)
                    {
                        for(int c=0;c<3;c++){double bg=background is not null?Cover(background,x,y,w,h,c):recipe.Background![c];output[i+c]=(byte)Math.Clamp(Math.Round(frame.Pixels[i+c]*foreground+bg*(1-foreground)),0,255);}output[i+3]=255;
                    }
                    else output[i+3]=(byte)Math.Round(255*foreground);
                }
                else if(soft is not null&&soft[i+3]>0)
                    for(int c=0;c<3;c++)output[i+c]=(byte)Math.Clamp(Math.Round(frame.Pixels[i+c]*mask+soft[i+c]*(1-mask)),0,255);
            }
        });return new(w,h,output);
    }
    public static double[] SubjectBounds(SubjectMatte matte)
    {
        var bytes=matte.Decode();int left=matte.Width,top=matte.Height,right=-1,bottom=-1;
        for(int y=0;y<matte.Height;y++)for(int x=0;x<matte.Width;x++)if(bytes[y*matte.Width+x]>110){left=Math.Min(left,x);right=Math.Max(right,x);top=Math.Min(top,y);bottom=Math.Max(bottom,y);}
        return right<left?[.15,.1,.7,.8]:[(double)left/matte.Width,(double)top/matte.Height,(right-left+1d)/matte.Width,(bottom-top+1d)/matte.Height];
    }
    static ImageFrame Compose(ImageFrame frame,Recipe recipe,CancellationToken token)
    {
        int w=frame.Width,h=frame.Height;var pixels=(byte[])frame.Pixels.Clone();var matte=recipe.Subject!;
        Parallel.For(0,h,new ParallelOptions{CancellationToken=token},y=>{for(int x=0;x<w;x++){int i=(y*w+x)*4;pixels[i+3]=(byte)Math.Round(pixels[i+3]*Weight(matte,x,y,w,h,recipe));}});
        using var foreground=new SKBitmap(new SKImageInfo(w,h,SKColorType.Rgba8888,SKAlphaType.Unpremul));Marshal.Copy(pixels,0,foreground.GetPixels(),pixels.Length);
        using var target=new SKBitmap(new SKImageInfo(w,h,SKColorType.Rgba8888,SKAlphaType.Unpremul));
        using(var canvas=new SKCanvas(target))
        {
            var color=recipe.Background;canvas.Clear(color is not null?new SKColor((byte)color[0],(byte)color[1],(byte)color[2]):SKColors.Transparent);
            if(recipe.BackgroundImage is string path)
            {
                var background=ImageIO.Load(path,Math.Max(w,h));using var bitmap=new SKBitmap(new SKImageInfo(background.Width,background.Height,SKColorType.Rgba8888,SKAlphaType.Unpremul));Marshal.Copy(background.Pixels,0,bitmap.GetPixels(),background.Pixels.Length);using var image=SKImage.FromBitmap(bitmap);
                float cover=Math.Max(w/(float)background.Width,h/(float)background.Height),bw=background.Width*cover,bh=background.Height*cover;
                canvas.DrawImage(image,new SKRect((w-bw)/2,(h-bh)/2,(w+bw)/2,(h+bh)/2),new SKSamplingOptions(SKFilterMode.Linear));
            }
            var bounds=SubjectBounds(matte);float scale=(float)Math.Clamp(recipe.Get("subjectScale"),.1,4),cx=(float)(bounds[0]+bounds[2]/2)*w,cy=(float)(bounds[1]+bounds[3]/2)*h;
            float dx=(1-scale)*cx+(float)recipe.Get("subjectX")*w,dy=(1-scale)*cy+(float)recipe.Get("subjectY")*h;
            using var subject=SKImage.FromBitmap(foreground);canvas.DrawImage(subject,new SKRect(dx,dy,dx+w*scale,dy+h*scale),new SKSamplingOptions(SKFilterMode.Linear));
        }
        token.ThrowIfCancellationRequested();Marshal.Copy(target.GetPixels(),pixels,0,pixels.Length);return new(w,h,pixels);
    }
    static double Weight(SubjectMatte matte,int x,int y,int w,int h,Recipe recipe)
    {
        double value=matte.Sample((x+.5)/w,(y+.5)/h),offset=recipe.Get("subjectEdge")/400;
        double low=Math.Clamp(.08+offset,0,.8),high=Math.Clamp(.85+offset,.2,1),t=Math.Clamp((value-low)/(high-low),0,1);return t*t*(3-2*t);
    }
    static double Cover(ImageFrame bg,int x,int y,int w,int h,int channel)
    {
        double scale=Math.Max(w/(double)bg.Width,h/(double)bg.Height);int xx=Math.Clamp((int)((x+(bg.Width*scale-w)/2)/scale),0,bg.Width-1),yy=Math.Clamp((int)((y+(bg.Height*scale-h)/2)/scale),0,bg.Height-1);return bg.Pixels[(yy*bg.Width+xx)*4+channel];
    }
    public static ImageFrame Blur(ImageFrame frame,double sigma)
    {
        using var source=new SKBitmap(new SKImageInfo(frame.Width,frame.Height,SKColorType.Rgba8888,SKAlphaType.Unpremul));Marshal.Copy(frame.Pixels,0,source.GetPixels(),frame.Pixels.Length);
        using var target=new SKBitmap(new SKImageInfo(frame.Width,frame.Height,SKColorType.Rgba8888,SKAlphaType.Unpremul));
        using(var canvas=new SKCanvas(target)){canvas.Clear(SKColors.Transparent);using var filter=SKImageFilter.CreateBlur((float)sigma,(float)sigma,SKShaderTileMode.Clamp);using var paint=new SKPaint{ImageFilter=filter};using var image=SKImage.FromBitmap(source);canvas.DrawImage(image,0,0,new SKSamplingOptions(SKFilterMode.Linear),paint);}
        var bytes=new byte[frame.Pixels.Length];Marshal.Copy(target.GetPixels(),bytes,0,bytes.Length);return frame with{Pixels=bytes};
    }
}
