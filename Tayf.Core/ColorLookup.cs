namespace Tayf.Core;
public static class ColorLookup
{
    public const int Side=16,Tile=20,Width=80;
    public static ImageFrame Create(Recipe source)
    {
        int n=Side,w=n*n,h=n;byte[] pixels=new byte[w*h*4];
        for(int b=0;b<n;b++)for(int g=0;g<n;g++)for(int r=0;r<n;r++){int i=(g*w+b*n+r)*4;pixels[i]=(byte)Math.Round(r*255d/(n-1));pixels[i+1]=(byte)Math.Round(g*255d/(n-1));pixels[i+2]=(byte)Math.Round(b*255d/(n-1));pixels[i+3]=255;}
        var recipe=source.Clone();recipe.Crop=[0,0,1,1];recipe.Subject=null;recipe.Face=null;recipe.Removals.Clear();recipe.Background=null;recipe.BackgroundImage=null;recipe.Masks.Clear();recipe.Retouch.Clear();
        foreach(string key in new[]{"vignette","lensVignette","grain","sharpen","texture","clarity","blur","portraitBlur","subjectCutout","keyEnabled","luminanceNoise","colorNoise","rotation","quarterTurns","flipX","flipY","vertical","horizontal","distortion"})recipe.Values.Remove(key);
        var mapped=ImageEngine.Render(new(w,h,pixels),recipe);var atlas=new byte[Width*Width*4];
        // Square atlas with duplicated edge texels prevents adjacent blue slices
        // from bleeding when WPF rasterizes an ImageBrush sampler at the element size.
        for(int b=0;b<n;b++)for(int y=0;y<Tile;y++)for(int x=0;x<Tile;x++)
        {
            int r=Math.Clamp(x-2,0,n-1),g=Math.Clamp(y-2,0,n-1),sourceOffset=(g*w+b*n+r)*4,target=((b/4*Tile+y)*Width+b%4*Tile+x)*4;
            Buffer.BlockCopy(mapped.Pixels,sourceOffset,atlas,target,4);
        }
        return new(Width,Width,atlas);
    }
}

