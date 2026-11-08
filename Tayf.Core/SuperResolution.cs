using System.IO;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace Tayf.Core;
/// <summary>CPU Real-ESRGAN x4. Overlapping reflected tiles keep inference memory bounded.</summary>
public static class SuperResolution
{
    public static ImageFrame Upscale(ImageFrame source,int longEdge=3840,CancellationToken token=default,IProgress<double>? progress=null)
    {
        source.Validate();token.ThrowIfCancellationRequested();if(longEdge<128||longEdge>4096)throw new ArgumentOutOfRangeException(nameof(longEdge));
        if(Math.Max(source.Width,source.Height)>=longEdge)return source with{Pixels=(byte[])source.Pixels.Clone(),LinearRgb=null};
        int nativeEdge=Math.Clamp(Math.Max(source.Width,source.Height)*4,128,longEdge);
        if(nativeEdge<longEdge){var intermediate=Upscale(source,nativeEdge,token);return Upscale(intermediate,longEdge,token,progress);}
        string model=LocalModels.PathFor("upscale");if(!File.Exists(model))throw new FileNotFoundException("ثبّت نموذج التكبير المحلي من مركز النماذج.",model);
        OrtEnv.Instance().DisableTelemetryEvents();using var options=new SessionOptions{GraphOptimizationLevel=GraphOptimizationLevel.ORT_ENABLE_ALL,IntraOpNumThreads=Math.Max(1,Math.Min(4,Environment.ProcessorCount/2)),InterOpNumThreads=1};
        using var session=new InferenceSession(model,options);string name=session.InputMetadata.Keys.First();var dims=session.InputMetadata[name].Dimensions;bool nhwc=dims[3]==3;int tile=nhwc?dims[1]:dims[2];if(tile<64||tile>512)throw new InvalidDataException("أبعاد نموذج التكبير غير متوافقة.");
        int targetW=Math.Max(1,(int)Math.Round(source.Width*longEdge/(double)Math.Max(source.Width,source.Height))),targetH=Math.Max(1,(int)Math.Round(source.Height*longEdge/(double)Math.Max(source.Width,source.Height)));
        int w=Math.Max(1,(targetW+3)/4),h=Math.Max(1,(targetH+3)/4);var input=ImageIO.Stretch(source,w,h);int ow=w*4,oh=h*4;var pixels=ImageIO.Stretch(source,ow,oh).Pixels;
        int halo=Math.Min(32,tile/4),core=tile-halo*2,columns=(w+core-1)/core,rows=(h+core-1)/core,done=0;
        int Reflect(int v,int length){if(length==1)return 0;int period=2*(length-1);v=((v%period)+period)%period;return v<length?v:period-v;}
        using var run=new RunOptions();using var cancellation=token.Register(()=>run.Terminate=true);
        try
        {
            for(int top=0;top<h;top+=core)for(int left=0;left<w;left+=core)
            {
                token.ThrowIfCancellationRequested();var tensor=new DenseTensor<float>(nhwc?[1,tile,tile,3]:[1,3,tile,tile]);
                for(int y=0;y<tile;y++)for(int x=0;x<tile;x++){int i=(Reflect(top+y-halo,h)*w+Reflect(left+x-halo,w))*4;for(int c=0;c<3;c++)tensor.Buffer.Span[nhwc?(y*tile+x)*3+c:c*tile*tile+y*tile+x]=input.Pixels[i+c]/255f;}
                using var result=session.Run([NamedOnnxValue.CreateFromTensor(name,tensor)],session.OutputNames,run);var output=result.First().AsTensor<float>();var shape=output.Dimensions.ToArray();bool outNhwc=shape[3]==3;int side=outNhwc?shape[1]:shape[2];if(side!=tile*4)throw new InvalidDataException("مقياس نموذج التكبير غير متوافق.");
                var values=output.ToArray();int cw=Math.Min(core,w-left)*4,ch=Math.Min(core,h-top)*4;
                for(int y=0;y<ch;y++)for(int x=0;x<cw;x++){int sx=x+halo*4,sy=y+halo*4,d=((top*4+y)*ow+left*4+x)*4;for(int c=0;c<3;c++)pixels[d+c]=(byte)Math.Clamp((int)Math.Round(values[outNhwc?(sy*side+sx)*3+c:c*side*side+sy*side+sx]*255),0,255);}
                progress?.Report(++done*100d/(columns*rows));
            }
        }
        catch(OnnxRuntimeException)when(token.IsCancellationRequested){throw new OperationCanceledException(token);}
        token.ThrowIfCancellationRequested();var frame=new ImageFrame(ow,oh,pixels);return ow==targetW&&oh==targetH?frame:ImageIO.Stretch(frame,targetW,targetH);
    }
}
