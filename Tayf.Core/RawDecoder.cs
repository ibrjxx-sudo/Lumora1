using System.IO;
using System.Runtime.InteropServices;

namespace Tayf.Core;
public static class RawDecoder
{
    public static readonly System.Collections.Concurrent.ConcurrentDictionary<string,(int Width,int Height)> LastDimensions=new(StringComparer.OrdinalIgnoreCase);
    public static readonly string[] Extensions=[".raw",".dng",".cr2",".cr3",".crw",".nef",".nrw",".arw",".sr2",".srf",".raf",".orf",".rw2",".rwl",".pef",".ptx",".srw",".3fr",".fff",".iiq",".mos",".mrw",".kdc",".dcr",".erf",".mef",".x3f",".bay",".cap",".eip"];
    static RawDecoder(){NativeLibrary.SetDllImportResolver(typeof(RawDecoder).Assembly,(name,assembly,search)=>name=="lumora-libraw"?NativeLibrary.Load(Path.Combine(AppContext.BaseDirectory,"Native","raw","libraw.dll")):IntPtr.Zero);}
    public static bool IsRaw(string path)=>Extensions.Contains(Path.GetExtension(path).ToLowerInvariant());
    static void Check(int error){if(error!=0)throw new InvalidDataException("تعذر فك RAW: "+Marshal.PtrToStringAnsi(libraw_strerror(error)));}
    public static ImageFrame Load(string path,int maxSide=0)
    {
        IntPtr raw=IntPtr.Zero,image=IntPtr.Zero;
        try
        {
            raw=libraw_init(0);if(raw==IntPtr.Zero)throw new OutOfMemoryException("لا توجد ذاكرة كافية لفك RAW.");
            Check(libraw_open_wfile(raw,Path.GetFullPath(path)));long rw=libraw_get_raw_width(raw),rh=libraw_get_raw_height(raw);if(rw<1||rh<1||rw*rh>ImageIO.MaxRenderPixels)throw new InvalidDataException("RAW أكبر من ميزانية فك المستشعر (120 ميغابكسل).");
            libraw_set_output_bps(raw,16);libraw_set_output_color(raw,1);libraw_set_gamma(raw,0,1);libraw_set_gamma(raw,1,1);libraw_set_no_auto_bright(raw,1);libraw_set_highlight(raw,2);libraw_set_demosaic(raw,3);
            for(int c=0;c<4;c++){float v=libraw_get_cam_mul(raw,c);if(v<=0)v=libraw_get_pre_mul(raw,c);if(v>0&&float.IsFinite(v))libraw_set_user_mul(raw,c,v);}
            Check(libraw_unpack(raw));Check(libraw_dcraw_process(raw));image=libraw_dcraw_make_mem_image(raw,out int error);Check(error);if(image==IntPtr.Zero)throw new InvalidDataException("RAW لا يحتوي صورة قابلة للفك.");
            int type=Marshal.ReadInt32(image),height=(ushort)Marshal.ReadInt16(image,4),width=(ushort)Marshal.ReadInt16(image,6),colors=(ushort)Marshal.ReadInt16(image,8),bits=(ushort)Marshal.ReadInt16(image,10);uint length=unchecked((uint)Marshal.ReadInt32(image,12));
            if(type!=2||bits!=16||colors is <3 or >4||width<1||height<1||(long)width*height>ImageIO.MaxRenderPixels||length!=(long)width*height*colors*2)throw new InvalidDataException("نتيجة محرك RAW غير مدعومة.");
            LastDimensions[Path.GetFullPath(path)]=(width,height);double scale=maxSide>0?Math.Min(1,maxSide/(double)Math.Max(width,height)):1;int w=Math.Max(1,(int)Math.Round(width*scale)),h=Math.Max(1,(int)Math.Round(height*scale));var linear=new ushort[checked(w*h*3)];var rgba=new byte[checked(w*h*4)];
            // Sample directly from the 16-bit demosaiced buffer; never use an embedded JPEG.
            for(int y=0;y<h;y++)for(int x=0;x<w;x++)
            {
                double sx=Math.Clamp((x+.5)*width/w-.5,0,width-1),sy=Math.Clamp((y+.5)*height/h-.5,0,height-1);int x0=(int)sx,y0=(int)sy,x1=Math.Min(width-1,x0+1),y1=Math.Min(height-1,y0+1);double fx=sx-x0,fy=sy-y0;
                for(int c=0;c<3;c++){double At(int xx,int yy)=>(ushort)Marshal.ReadInt16(image,16+((yy*width+xx)*colors+c)*2);double value=(At(x0,y0)*(1-fx)+At(x1,y0)*fx)*(1-fy)+(At(x0,y1)*(1-fx)+At(x1,y1)*fx)*fy;linear[(y*w+x)*3+c]=(ushort)Math.Clamp((int)Math.Round(value),0,65535);double v=value/65535;rgba[(y*w+x)*4+c]=(byte)Math.Clamp((int)Math.Round((v<=.0031308?v*12.92:1.055*Math.Pow(v,1/2.4)-.055)*255),0,255);}rgba[(y*w+x)*4+3]=255;
            }
            return new(w,h,rgba){LinearRgb=linear};
        }
        catch(DllNotFoundException ex){throw new InvalidDataException("محرك RAW غير موجود. فك حزمة Lumora كاملة مع مجلد Native.",ex);}
        finally{if(image!=IntPtr.Zero)libraw_dcraw_clear_mem(image);if(raw!=IntPtr.Zero)libraw_close(raw);}
    }
    const string Dll="lumora-libraw";
    [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)]static extern IntPtr libraw_init(uint flags);
    [DllImport(Dll,CallingConvention=CallingConvention.Cdecl,CharSet=CharSet.Unicode)]static extern int libraw_open_wfile(IntPtr raw,string path);
    [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)]static extern int libraw_unpack(IntPtr raw);
    [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)]static extern int libraw_dcraw_process(IntPtr raw);
    [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)]static extern IntPtr libraw_dcraw_make_mem_image(IntPtr raw,out int error);
    [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)]static extern void libraw_dcraw_clear_mem(IntPtr image);
    [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)]static extern void libraw_close(IntPtr raw);
    [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)]static extern IntPtr libraw_strerror(int error);
    [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)]static extern void libraw_set_output_bps(IntPtr raw,int value);
    [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)]static extern void libraw_set_output_color(IntPtr raw,int value);
    [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)]static extern void libraw_set_gamma(IntPtr raw,int index,float value);
    [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)]static extern void libraw_set_no_auto_bright(IntPtr raw,int value);
    [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)]static extern void libraw_set_highlight(IntPtr raw,int value);
    [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)]static extern void libraw_set_demosaic(IntPtr raw,int value);
    [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)]static extern float libraw_get_cam_mul(IntPtr raw,int index);
    [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)]static extern float libraw_get_pre_mul(IntPtr raw,int index);
    [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)]static extern void libraw_set_user_mul(IntPtr raw,int index,float value);
    [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)]static extern int libraw_get_raw_width(IntPtr raw);
    [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)]static extern int libraw_get_raw_height(IntPtr raw);
}
