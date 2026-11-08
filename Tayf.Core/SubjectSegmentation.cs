using System.IO;
using System.IO.Compression;
using System.Text.Json.Serialization;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace Tayf.Core;

public sealed class SubjectMatte
{
    public int Width{get;set;}public int Height{get;set;}
    public byte[] Compressed{get;set;}=[];
    [JsonIgnore]byte[]? decoded;
    public void Validate(){if(Width<1||Height<1||(long)Width*Height>4_194_304||Compressed is null||Compressed.Length>5_000_000)throw new InvalidDataException("قناع العزل غير صالح.");}
    public byte[] Decode(){Validate();if(decoded is not null)return decoded;using var source=new MemoryStream(Compressed);using var inflate=new ZLibStream(source,CompressionMode.Decompress);var bytes=new byte[Width*Height];inflate.ReadExactly(bytes);return decoded=bytes;}
    public static SubjectMatte Create(int width,int height,byte[] bytes){using var stream=new MemoryStream();using(var deflate=new ZLibStream(stream,CompressionLevel.Fastest,true))deflate.Write(bytes);return new(){Width=width,Height=height,Compressed=stream.ToArray()};}
    public double Sample(double x,double y)
    {
        var values=Decode();double xx=Math.Clamp(x*Width-.5,0,Width-1),yy=Math.Clamp(y*Height-.5,0,Height-1);int ax=(int)xx,ay=(int)yy,bx=Math.Min(ax+1,Width-1),by=Math.Min(ay+1,Height-1);double fx=xx-ax,fy=yy-ay;
        return ((1-fy)*(values[ay*Width+ax]*(1-fx)+values[ay*Width+bx]*fx)+fy*(values[by*Width+ax]*(1-fx)+values[by*Width+bx]*fx))/255;
    }
}

public sealed class SubjectSegmentation:IDisposable
{
    readonly InferenceSession session;
    readonly SemaphoreSlim gate=new(1,1);
    public SubjectSegmentation(string model)
    {
        OrtEnv.Instance().DisableTelemetryEvents();
        using var options=new SessionOptions{GraphOptimizationLevel=GraphOptimizationLevel.ORT_ENABLE_ALL,IntraOpNumThreads=Math.Max(1,Math.Min(4,Environment.ProcessorCount/2)),InterOpNumThreads=1};
        session=new InferenceSession(model,options);
    }
    public async Task<SubjectMatte> PredictAsync(ImageFrame image,CancellationToken cancellation=default)
    {
        await gate.WaitAsync(cancellation);
        try{return await Task.Run(()=>
        {
            cancellation.ThrowIfCancellationRequested();const int side=512;var resized=ImageIO.Stretch(image,side,side);var input=new DenseTensor<float>(new[]{1,3,side,side});double[] mean=[.485,.456,.406],std=[.229,.224,.225];
            for(int y=0;y<side;y++)for(int x=0;x<side;x++)for(int c=0;c<3;c++)input[0,c,y,x]=(float)((resized.Pixels[(y*side+x)*4+c]/255d-mean[c])/std[c]);
            using var run=new RunOptions();using var registration=cancellation.Register(()=>run.Terminate=true);
            using var results=session.Run([NamedOnnxValue.CreateFromTensor(session.InputMetadata.Keys.First(),input)],session.OutputMetadata.Keys.ToArray(),run);
            var tensor=results.Last().AsTensor<float>();var dimensions=tensor.Dimensions.ToArray();int width=dimensions[^1],height=dimensions[^2];var values=tensor.ToArray();var alpha=new byte[width*height];
            for(int i=0;i<alpha.Length;i++)alpha[i]=(byte)Math.Clamp((int)Math.Round(255/(1+Math.Exp(-values[i]))),0,255);
            cancellation.ThrowIfCancellationRequested();return SubjectMatte.Create(width,height,alpha);
        },cancellation);}finally{gate.Release();}
    }
    public void Dispose(){session.Dispose();gate.Dispose();}
}
