using System.IO;
using System.Runtime.CompilerServices;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using SkiaSharp;
using System.Runtime.InteropServices;

namespace Tayf.Core;
public sealed class RemovalOperation
{
    public double Size {get;set;}=.025;
    public List<double[]> Points {get;set;}=[];
    public List<int> Breaks {get;set;}=[];
    public void Validate(){if(!double.IsFinite(Size)||Size is <=0 or >.3||Points is null||Points.Count is <1 or >15000||Points.Any(p=>p.Length!=2||p.Any(v=>!double.IsFinite(v)||v<0||v>1))||Breaks is null||Breaks.Count>15000||Breaks.Any(i=>i<0||i>=Points.Count))throw new InvalidDataException("تحديد الإزالة غير صالح.");}
}
public static class ObjectRemoval
{
    static readonly object Gate=new();static InferenceSession? session;
    static readonly ConditionalWeakTable<ImageFrame,Dictionary<string,ImageFrame>> Cache=new();
    public static void ReleaseModel(){lock(Gate){session?.Dispose();session=null;Cache.Clear();}}
    public static ImageFrame Apply(ImageFrame source,List<RemovalOperation> operations,CancellationToken token=default)
    {
        if(operations.Count==0)return source;string key=Json.Write(operations);lock(Gate)
        {
            var cache=Cache.GetOrCreateValue(source);if(cache.TryGetValue(key,out var cached))return cached;
            string model=LocalModels.PathFor("erase");if(!File.Exists(model))throw new FileNotFoundException("نزّل نموذج إزالة العناصر من مركز النماذج المحلية أولًا.");session??=new InferenceSession(model,new SessionOptions{IntraOpNumThreads=Math.Min(4,Environment.ProcessorCount)});
            var result=source;foreach(var op in operations){token.ThrowIfCancellationRequested();op.Validate();try{result=Fill(result,op,session,token);}catch(OnnxRuntimeException)when(token.IsCancellationRequested){throw new OperationCanceledException(token);}}if(cache.Count>=2)cache.Clear();cache[key]=result;return result;
        }
    }
    static ImageFrame Fill(ImageFrame source,RemovalOperation operation,InferenceSession model,CancellationToken token)
    {
        int w=source.Width,h=source.Height,radius=Math.Max(1,(int)Math.Ceiling(operation.Size*Math.Max(w,h))),left=w,top=h,right=0,bottom=0;
        foreach(var p in operation.Points){int x=(int)(p[0]*(w-1)),y=(int)(p[1]*(h-1));left=Math.Min(left,x-radius);top=Math.Min(top,y-radius);right=Math.Max(right,x+radius);bottom=Math.Max(bottom,y+radius);}
        left=Math.Clamp(left,0,w-1);top=Math.Clamp(top,0,h-1);right=Math.Clamp(right,0,w-1);bottom=Math.Clamp(bottom,0,h-1);if((long)(right-left+1)*(bottom-top+1)>w*(long)h*.7)throw new InvalidDataException("حدّد مساحة أصغر واترك سياقًا حول العنصر للإزالة.");
        int pad=Math.Max(32,Math.Max(right-left,bottom-top)/2),x0=Math.Max(0,left-pad),y0=Math.Max(0,top-pad),x1=Math.Min(w-1,right+pad),y1=Math.Min(h-1,bottom+pad),cw=x1-x0+1,ch=y1-y0+1;const int side=512;
        var mask=new byte[cw*ch];using(var bitmap=new SKBitmap(new SKImageInfo(cw,ch,SKColorType.Gray8,SKAlphaType.Opaque)))
        {
            using(var canvas=new SKCanvas(bitmap))using(var paint=new SKPaint{Color=SKColors.White,StrokeWidth=(radius+2)*2,StrokeCap=SKStrokeCap.Round,StrokeJoin=SKStrokeJoin.Round,Style=SKPaintStyle.Stroke,IsAntialias=true})using(var path=new SKPathBuilder())
            {
                canvas.Clear(SKColors.Black);var breaks=operation.Breaks.ToHashSet();for(int i=0;i<operation.Points.Count;i++){var p=operation.Points[i];float x=(float)(p[0]*(w-1)-x0),y=(float)(p[1]*(h-1)-y0);if(i==0||breaks.Contains(i)){path.MoveTo(x,y);canvas.DrawPoint(x,y,paint);}else path.LineTo(x,y);}using var drawn=path.Detach();canvas.DrawPath(drawn,paint);
            }
            for(int y=0;y<ch;y++)Marshal.Copy(bitmap.GetPixels()+y*bitmap.RowBytes,mask,y*cw,cw);
        }
        token.ThrowIfCancellationRequested();
        var image=new DenseTensor<float>(new[]{1,3,side,side});var m=new DenseTensor<float>(new[]{1,1,side,side});for(int y=0;y<side;y++)for(int x=0;x<side;x++){int sx=Math.Min(cw-1,x*cw/side),sy=Math.Min(ch-1,y*ch/side),i=((sy+y0)*w+sx+x0)*4;for(int c=0;c<3;c++)image[0,c,y,x]=source.Pixels[i+c]/255f;m[0,0,y,x]=mask[sy*cw+sx]>0?1:0;}
        using var options=new RunOptions();using var cancel=token.Register(()=>options.Terminate=true);using var outputs=model.Run([NamedOnnxValue.CreateFromTensor("image",image),NamedOnnxValue.CreateFromTensor("mask",m)],model.OutputNames,options);token.ThrowIfCancellationRequested();var tensor=outputs.First().AsTensor<float>();var data=(byte[])source.Pixels.Clone();ushort[]? linear=source.LinearRgb is null?null:(ushort[])source.LinearRgb.Clone();
        for(int y=0;y<ch;y++)for(int x=0;x<cw;x++)if(mask[y*cw+x]>0)
        {
            double sx=Math.Clamp((x+.5)*side/cw-.5,0,side-1),sy=Math.Clamp((y+.5)*side/ch-.5,0,side-1);int xx=(int)sx,yy=(int)sy,xx1=Math.Min(side-1,xx+1),yy1=Math.Min(side-1,yy+1);double dx=sx-xx,dy=sy-yy;int at=((y+y0)*w+x+x0)*4;
            // Feather only inside the mask. Every original pixel outside it remains identical.
            double weight=mask[y*cw+x]/255d;if(mask[y*cw+Math.Max(0,x-2)]==0||mask[y*cw+Math.Min(cw-1,x+2)]==0||mask[Math.Max(0,y-2)*cw+x]==0||mask[Math.Min(ch-1,y+2)*cw+x]==0)weight*=.35;
            for(int c=0;c<3;c++){double value=(tensor[0,c,yy,xx]*(1-dx)+tensor[0,c,yy,xx1]*dx)*(1-dy)+(tensor[0,c,yy1,xx]*(1-dx)+tensor[0,c,yy1,xx1]*dx)*dy;byte b=(byte)Math.Clamp((int)Math.Round(data[at+c]*(1-weight)+value*weight),0,255);data[at+c]=b;if(linear is not null){double s=b/255d;linear[((y+y0)*w+x+x0)*3+c]=(ushort)Math.Round((s<=.04045?s/12.92:Math.Pow((s+.055)/1.055,2.4))*65535);}}
        }
        return new(w,h,data){LinearRgb=linear};
    }
}
