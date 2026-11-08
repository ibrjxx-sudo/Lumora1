using System.IO;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace Tayf.Core;
public sealed class FaceAnchor
{
    public double[] Bounds {get;set;}=[.25,.15,.5,.65];
    public double[][] Points {get;set;}=[[.38,.38],[.62,.38],[.5,.52],[.41,.64],[.59,.64]];
    public void Validate(){if(Bounds is null||Points is null||Bounds.Length!=4||Points.Length!=5||Bounds.Any(v=>!double.IsFinite(v))||Bounds[2]<=0||Bounds[3]<=0||Points.Any(p=>p is null||p.Length!=2||p.Any(v=>!double.IsFinite(v)||v<0||v>1)))throw new InvalidDataException("مواضع الوجه غير صالحة.");}
}
public static class FaceDetector
{
    static readonly object Gate=new();static InferenceSession? session;
    public static FaceAnchor? Detect(ImageFrame image,string model,CancellationToken token=default)
    {
        lock(Gate)
        {
            if(!File.Exists(model))throw new FileNotFoundException("نموذج الوجه غير موجود في Models.");
            session??=new InferenceSession(model,new SessionOptions{IntraOpNumThreads=Math.Min(4,Environment.ProcessorCount)});
            const int side=640;var resized=ImageIO.Resize(image,side);int w=resized.Width,h=resized.Height;var tensor=new DenseTensor<float>(new[]{1,3,side,side});for(int y=0;y<h;y++)for(int x=0;x<w;x++){int i=(y*w+x)*4;for(int c=0;c<3;c++)tensor[0,c,y,x]=resized.Pixels[i+2-c];}
            token.ThrowIfCancellationRequested();using var result=session.Run([NamedOnnxValue.CreateFromTensor(session.InputMetadata.Keys.First(),tensor)]);token.ThrowIfCancellationRequested();var map=result.ToDictionary(v=>v.Name,v=>v.AsTensor<float>().ToArray());var candidates=new List<(float Score,FaceAnchor Face)>();
            foreach(int stride in new[]{8,16,32})
            {
                var cls=map["cls_"+stride];var obj=map["obj_"+stride];var box=map["bbox_"+stride];var kp=map["kps_"+stride];int cols=side/stride;
                for(int i=0;i<cls.Length;i++)
                {
                    float score=MathF.Sqrt(Math.Clamp(cls[i],0,1)*Math.Clamp(obj[i],0,1));if(score<.75)continue;double cx=(i%cols+box[i*4])*stride,cy=(i/cols+box[i*4+1])*stride,bw=Math.Exp(Math.Clamp(box[i*4+2],-8,8))*stride,bh=Math.Exp(Math.Clamp(box[i*4+3],-8,8))*stride;if(cx<0||cy<0||cx>w||cy>h)continue;
                    var f=new FaceAnchor{Bounds=[Math.Clamp((cx-bw/2)/w,0,1),Math.Clamp((cy-bh/2)/h,0,1),Math.Min(1,bw/w),Math.Min(1,bh/h)],Points=Enumerable.Range(0,5).Select(j=>new double[]{Math.Clamp((i%cols+kp[i*10+j*2])*stride/w,0,1),Math.Clamp((i/cols+kp[i*10+j*2+1])*stride/h,0,1)}).ToArray()};candidates.Add((score,f));
                }
            }
            return candidates.OrderByDescending(v=>v.Score*Math.Sqrt(v.Face.Bounds[2]*v.Face.Bounds[3])).Select(v=>v.Face).FirstOrDefault();
        }
    }
}
