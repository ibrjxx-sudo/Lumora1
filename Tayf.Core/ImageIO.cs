using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SkiaSharp;

namespace Tayf.Core;

public static class ImageIO
{
    public const long MaxPixels=1_000_000_000;
    public const long MaxRenderPixels=120_000_000;
    public static readonly string[] Extensions=[".jpg",".jpeg",".jfif",".png",".webp",".tif",".tiff",".bmp",".gif",".ico",".avif",".heic",".heif",".jxl",".exr",".hdr",".psd",..RawDecoder.Extensions];
    public static string FileDialogFilter=>"الصور وملفات RAW|"+string.Join(";",Extensions.Select(e=>"*"+e))+"|كل الملفات|*.*";
    public static ImageFrame Load(string path,int maxSide=0)
    {
        var file=new FileInfo(path);
        if(!file.Exists)throw new FileNotFoundException("ملف الصورة غير موجود.",path);
        if(file.Length>2_000_000_000)throw new InvalidDataException("ملف الصورة أكبر من 2 غيغابايت.");
        if(!Extensions.Contains(file.Extension.ToLowerInvariant()))throw new InvalidDataException("صيغة غير مدعومة. اختر صورة أو ملف كاميرا RAW مدعومًا.");
        if(RawDecoder.IsRaw(path))return RawDecoder.Load(path,maxSide);
        if(new[]{".avif",".heic",".heif",".jxl",".exr",".hdr",".psd",".ico"}.Contains(file.Extension.ToLowerInvariant()))return RasterFallback.Load(path,maxSide);
        using var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);
        if(file.Extension.Equals(".webp",StringComparison.OrdinalIgnoreCase))return LoadSkia(stream,maxSide);
        try
        {
            var decoder=BitmapDecoder.Create(stream,BitmapCreateOptions.PreservePixelFormat,BitmapCacheOption.OnDemand);
            if(decoder.Frames.Count<1)throw new InvalidDataException("ملف الصورة لا يحتوي إطارات.");
            var source=decoder.Frames[0];CheckSize(source.PixelWidth,source.PixelHeight);
            maxSide=BoundDecode(source.PixelWidth,source.PixelHeight,maxSide);
            BitmapSource bitmap=source;
            if(source.ColorContexts is {Count:>0})
            {
                try{bitmap=new ColorConvertedBitmap(source,source.ColorContexts[0],new ColorContext(PixelFormats.Bgra32),PixelFormats.Bgra32);}
                catch(Exception ex)when(ex is NotSupportedException or ArgumentException){throw new InvalidDataException("تعذر تحويل ملف اللون المضمّن بأمان.",ex);}
            }
            int orientation=1;
            if(source.Metadata is BitmapMetadata metadata){try{if(metadata.GetQuery("/app1/ifd/{ushort=274}") is ushort v)orientation=v;}catch(NotSupportedException){} }
            if(maxSide>0 && Math.Max(bitmap.PixelWidth,bitmap.PixelHeight)>maxSide)
            {double scale=maxSide/(double)Math.Max(bitmap.PixelWidth,bitmap.PixelHeight);bitmap=new TransformedBitmap(bitmap,new ScaleTransform(scale,scale));}
            var converted=new FormatConvertedBitmap(bitmap,PixelFormats.Bgra32,null,0);int w=converted.PixelWidth,h=converted.PixelHeight;
            byte[] bgra=new byte[checked(w*h*4)];converted.CopyPixels(bgra,w*4,0);for(int i=0;i<bgra.Length;i+=4)(bgra[i],bgra[i+2])=(bgra[i+2],bgra[i]);
            return Orient(new(w,h,bgra),orientation);
        }
        catch(Exception ex)when(ex is FileFormatException or NotSupportedException or ArgumentException or System.Runtime.InteropServices.COMException)
        {throw new InvalidDataException("تعذر فك الصورة. قد يكون الملف تالفًا أو ترميزه غير مدعوم.",ex);}
    }
    static ImageFrame LoadSkia(Stream stream,int maxSide)
    {
        using var data=SKData.Create(stream);using var codec=SKCodec.Create(data);
        if(codec is null)throw new InvalidDataException("ملف WebP تالف أو غير مدعوم.");
        CheckSize(codec.Info.Width,codec.Info.Height);if(codec.FrameCount>1)throw new InvalidDataException("WebP المتحرك غير مدعوم؛ افتحه كفيديو أو اختر صورة ثابتة.");
        maxSide=BoundDecode(codec.Info.Width,codec.Info.Height,maxSide);
        var size=maxSide>0?codec.GetScaledDimensions(Math.Min(1,maxSide/(float)Math.Max(codec.Info.Width,codec.Info.Height))):new SKSizeI(codec.Info.Width,codec.Info.Height);
        using var color=SKColorSpace.CreateSrgb();var info=new SKImageInfo(size.Width,size.Height,SKColorType.Rgba8888,SKAlphaType.Unpremul,color);
        using var bitmap=SKBitmap.Decode(codec,info)??throw new InvalidDataException("تعذر فك صورة WebP.");
        var bytes=new byte[checked(info.Width*info.Height*4)];Marshal.Copy(bitmap.GetPixels(),bytes,0,bytes.Length);var frame=Orient(new(info.Width,info.Height,bytes),(int)codec.EncodedOrigin);
        return maxSide>0?Resize(frame,maxSide):frame;
    }
    static int BoundDecode(int w,int h,int side){if(side>0)return side;return (long)w*h>MaxRenderPixels?(int)Math.Floor(Math.Max(w,h)*Math.Sqrt(MaxRenderPixels/((double)w*h))):0;}
    static void CheckSize(int w,int h){if(w<1||h<1||(long)w*h>MaxPixels)throw new InvalidDataException("أبعاد الصورة غير صالحة أو غير قابلة للفك على هذا الجهاز.");}
    public static (int Width,int Height) Probe(string path)
    {
        if(RawDecoder.IsRaw(path)){if(RawDecoder.LastDimensions.TryGetValue(Path.GetFullPath(path),out var dimensions))return dimensions;var raw=RawDecoder.Load(path,64);return RawDecoder.LastDimensions.GetValueOrDefault(Path.GetFullPath(path),(raw.Width,raw.Height));}
        if(new[]{".avif",".heic",".heif",".jxl",".exr",".hdr",".psd",".ico"}.Contains(Path.GetExtension(path).ToLowerInvariant())){var frame=RasterFallback.Load(path);return(frame.Width,frame.Height);}
        using var stream=File.OpenRead(path);int w,h,orientation=1;
        if(Path.GetExtension(path).Equals(".webp",StringComparison.OrdinalIgnoreCase)){using var data=SKData.Create(stream);using var codec=SKCodec.Create(data)??throw new InvalidDataException("صورة غير صالحة.");w=codec.Info.Width;h=codec.Info.Height;orientation=(int)codec.EncodedOrigin;}
        else{var decoder=BitmapDecoder.Create(stream,BitmapCreateOptions.DelayCreation,BitmapCacheOption.OnDemand);var frame=decoder.Frames[0];w=frame.PixelWidth;h=frame.PixelHeight;try{if(frame.Metadata is BitmapMetadata metadata&&metadata.GetQuery("/app1/ifd/{ushort=274}") is ushort v)orientation=v;}catch(NotSupportedException){}}
        CheckSize(w,h);return orientation is >=5 and <=8?(h,w):(w,h);
    }
    public static ImageFrame Stretch(ImageFrame frame,int w,int h)
    {
        using var source=SkiaBitmap(frame);using var target=new SKBitmap(new SKImageInfo(w,h,SKColorType.Rgba8888,SKAlphaType.Unpremul));
        using(var canvas=new SKCanvas(target)){using var image=SKImage.FromBitmap(source);canvas.DrawImage(image,new SKRect(0,0,w,h),new SKSamplingOptions(SKFilterMode.Linear));}
        byte[] bytes=new byte[checked(w*h*4)];Marshal.Copy(target.GetPixels(),bytes,0,bytes.Length);return new(w,h,bytes);
    }
    static ImageFrame Orient(ImageFrame input,int orientation)
    {
        if(orientation is <2 or >8)return input;
        bool swap=orientation>=5;int w=swap?input.Height:input.Width,h=swap?input.Width:input.Height;var data=new byte[w*h*4];
        for(int y=0;y<h;y++)for(int x=0;x<w;x++)
        {
            (int sx,int sy)=orientation switch
            {2=>(input.Width-1-x,y),3=>(input.Width-1-x,input.Height-1-y),4=>(x,input.Height-1-y),5=>(y,x),6=>(y,input.Height-1-x),7=>(input.Width-1-y,input.Height-1-x),8=>(input.Width-1-y,x),_=>(x,y)};
            Buffer.BlockCopy(input.Pixels,(sy*input.Width+sx)*4,data,(y*w+x)*4,4);
        }return new(w,h,data);
    }
    public static BitmapSource ToBitmap(ImageFrame frame)
    {
        frame.Validate();var bgra=(byte[])frame.Pixels.Clone();for(int i=0;i<bgra.Length;i+=4)(bgra[i],bgra[i+2])=(bgra[i+2],bgra[i]);
        var source=BitmapSource.Create(frame.Width,frame.Height,96,96,PixelFormats.Bgra32,null,bgra,frame.Width*4);source.Freeze();return source;
    }
    public static ImageFrame Resize(ImageFrame frame,int maxSide)
    {
        if(maxSide<=0||Math.Max(frame.Width,frame.Height)<=maxSide)return frame;
        double scale=maxSide/(double)Math.Max(frame.Width,frame.Height);int w=Math.Max(1,(int)Math.Round(frame.Width*scale)),h=Math.Max(1,(int)Math.Round(frame.Height*scale));
        using var source=SkiaBitmap(frame);using var color=SKColorSpace.CreateSrgb();using var target=new SKBitmap(new SKImageInfo(w,h,SKColorType.Rgba8888,SKAlphaType.Unpremul,color));
        using(var canvas=new SKCanvas(target)){canvas.Clear(SKColors.Transparent);using var image=SKImage.FromBitmap(source);canvas.DrawImage(image,new SKRect(0,0,w,h),new SKSamplingOptions(SKFilterMode.Linear,SKMipmapMode.None));}
        byte[] pixels=new byte[w*h*4];Marshal.Copy(target.GetPixels(),pixels,0,pixels.Length);return new(w,h,pixels);
    }
    static SKBitmap SkiaBitmap(ImageFrame frame){using var color=SKColorSpace.CreateSrgb();var bitmap=new SKBitmap(new SKImageInfo(frame.Width,frame.Height,SKColorType.Rgba8888,SKAlphaType.Unpremul,color));Marshal.Copy(frame.Pixels,0,bitmap.GetPixels(),frame.Pixels.Length);return bitmap;}
    public static void Save(ImageFrame frame,string destination,ExportSettings settings)
    {
        frame.Validate();frame=Resize(frame,settings.LongEdge);if(!string.IsNullOrWhiteSpace(settings.Watermark))frame=Watermark(frame,settings.Watermark);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(destination))!);
        using var output=new FileStream(destination,FileMode.CreateNew,FileAccess.Write,FileShare.None);
        string format=settings.Format.ToLowerInvariant();
        if(format=="webp")
        {using var bitmap=SkiaBitmap(frame);using var image=SKImage.FromBitmap(bitmap);using var encoded=image.Encode(SKEncodedImageFormat.Webp,Math.Clamp(settings.Quality,1,100))??throw new InvalidDataException("تعذر ترميز WebP.");encoded.SaveTo(output);return;}
        BitmapEncoder encoder=format switch
        {"jpg" or "jpeg"=>new JpegBitmapEncoder{QualityLevel=Math.Clamp(settings.Quality,1,100)},"png"=>new PngBitmapEncoder(),"tif" or "tiff"=>new TiffBitmapEncoder{Compression=TiffCompressOption.Zip},"bmp"=>new BmpBitmapEncoder(),"gif"=>new GifBitmapEncoder(),_=>throw new InvalidDataException("صيغة تصدير غير مدعومة.")};
        if(format is "jpg" or "jpeg" or "bmp")frame=Flatten(frame);
        var source=ToBitmap(frame);encoder.Frames.Add(BitmapFrame.Create(source));encoder.Save(output);output.Flush(true);
    }
    static ImageFrame Flatten(ImageFrame frame){var bytes=(byte[])frame.Pixels.Clone();for(int i=0;i<bytes.Length;i+=4){double a=bytes[i+3]/255d;for(int c=0;c<3;c++)bytes[i+c]=(byte)Math.Round(bytes[i+c]*a+255*(1-a));bytes[i+3]=255;}return frame with{Pixels=bytes};}
    static ImageFrame Watermark(ImageFrame frame,string text)
    {
        ImageFrame? result=null;Exception? error=null;
        void Draw()
        {
            try
            {
                var visual=new DrawingVisual();using(var context=visual.RenderOpen())
                {context.DrawImage(ToBitmap(frame),new Rect(0,0,frame.Width,frame.Height));double size=Math.Max(4,Math.Min(frame.Width,frame.Height)*.035);var formatted=new FormattedText(text[..Math.Min(text.Length,150)],CultureInfo.GetCultureInfo("ar"),FlowDirection.RightToLeft,new Typeface("Segoe UI"),size,Brushes.White,1);double x=Math.Max(3,frame.Width-formatted.Width-12),y=Math.Max(1,frame.Height-formatted.Height-10);context.DrawRectangle(new SolidColorBrush(Color.FromArgb(90,0,0,0)),null,new Rect(x-3,y,Math.Min(frame.Width-x,formatted.Width+6),Math.Min(frame.Height-y,formatted.Height)));context.DrawText(formatted,new Point(x,y));}
                var bitmap=new RenderTargetBitmap(frame.Width,frame.Height,96,96,PixelFormats.Pbgra32);bitmap.Render(visual);var converted=new FormatConvertedBitmap(bitmap,PixelFormats.Bgra32,null,0);var bytes=new byte[frame.Pixels.Length];converted.CopyPixels(bytes,frame.Width*4,0);for(int i=0;i<bytes.Length;i+=4)(bytes[i],bytes[i+2])=(bytes[i+2],bytes[i]);result=frame with{Pixels=bytes};
            }catch(Exception ex){error=ex;}
        }
        if(Thread.CurrentThread.GetApartmentState()==ApartmentState.STA)Draw();else{var thread=new Thread(Draw);thread.SetApartmentState(ApartmentState.STA);thread.Start();thread.Join();}
        if(error is not null)throw new InvalidDataException("تعذر رسم العلامة المائية.",error);return result!;
    }
    public static byte[] EncodePng(ImageFrame frame){using var stream=new MemoryStream();var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(ToBitmap(frame)));encoder.Save(stream);return stream.ToArray();}
}
