using System.Runtime.InteropServices;
using SkiaSharp;

namespace Tayf.Core;
public static class StudioBackdrops
{
    public static ImageFrame Create(int style,int width=1600,int height=2400)
    {
        SKColor[][] palettes=[ [new(34,37,48),new(105,110,129)], [new(238,229,212),new(190,171,147)], [new(192,201,192),new(84,113,101)], [new(222,207,209),new(150,111,128)], [new(203,218,235),new(104,135,167)], [new(249,248,243),new(215,213,202)], [new(23,28,42),new(15,18,27)], [new(218,203,173),new(129,109,83)] ];
        int palette=((style%8)+8)%8;var colors=palettes[palette];using var bitmap=new SKBitmap(new SKImageInfo(width,height,SKColorType.Rgba8888,SKAlphaType.Unpremul));using(var canvas=new SKCanvas(bitmap))
        {
            using var gradient=SKShader.CreateRadialGradient(new(width*.4f,height*.32f),height*.83f,[colors[0],colors[1]],null,SKShaderTileMode.Clamp);using var paint=new SKPaint{Shader=gradient,IsAntialias=true};canvas.DrawRect(0,0,width,height,paint);
            if(style>=8&&style<16)
            {
                using var glow=SKShader.CreateLinearGradient(new(0,0),new(width,height),[colors[1],colors[0],colors[1]],[0,.55f,1],SKShaderTileMode.Clamp);paint.Shader=glow;canvas.DrawRect(0,0,width,height,paint);
            }
            if(style>=16)
            {
                paint.Shader=null;using var shadow=SKImageFilter.CreateBlur(width*.026f,height*.009f);paint.ImageFilter=shadow;paint.Color=new SKColor(0,0,0,45);canvas.DrawOval(new SKRect(width*.08f,height*.78f,width*.93f,height*.87f),paint);paint.ImageFilter=null;
                using var pedestal=SKShader.CreateLinearGradient(new(0,height*.7f),new(width,height*.85f),[colors[0],colors[1]],null,SKShaderTileMode.Clamp);paint.Shader=pedestal;canvas.DrawRoundRect(new SKRect(width*.14f,height*.735f,width*.86f,height*.815f),width*.025f,width*.025f,paint);paint.Shader=null;paint.Color=colors[0];canvas.DrawOval(new SKRect(width*.14f,height*.70f,width*.86f,height*.77f),paint);
            }
        }
        byte[] pixels=new byte[width*height*4];Marshal.Copy(bitmap.GetPixels(),pixels,0,pixels.Length);return new(width,height,pixels);
    }
}
