using Tayf.Core;
namespace Lumora.Application.Services;

/// <summary>A detached edit snapshot without serializing masks and mattes to JSON on every pointer event.</summary>
public static class RecipeSnapshot
{
    public static Recipe Capture(Recipe source)=>new()
    {
        Version=source.Version,Values=new(source.Values),
        Curves=source.Curves.ToDictionary(p=>p.Key,p=>p.Value.ToList()),
        Hsl=source.Hsl.Select(CopyColor).ToList(),Grading=source.Grading.ToDictionary(p=>p.Key,p=>CopyColor(p.Value)),
        Crop=source.Crop.ToArray(),KeyColor=source.KeyColor.ToArray(),Background=source.Background?.ToArray(),BackgroundImage=source.BackgroundImage,
        Face=source.Face is {} face?new(){Bounds=face.Bounds.ToArray(),Points=face.Points.Select(p=>p.ToArray()).ToArray()}:null,
        Subject=source.Subject is {} matte?new(){Width=matte.Width,Height=matte.Height,Compressed=matte.Compressed.ToArray()}:null,
        Masks=source.Masks.Select(m=>new MaskRecipe{
            Id=m.Id,Name=m.Name,Type=m.Type,X=m.X,Y=m.Y,X2=m.X2,Y2=m.Y2,Rx=m.Rx,Ry=m.Ry,Size=m.Size,Feather=m.Feather,
            Opacity=m.Opacity,Invert=m.Invert,Exposure=m.Exposure,Contrast=m.Contrast,Saturation=m.Saturation,Min=m.Min,Max=m.Max,Tolerance=m.Tolerance,
            Color=m.Color.ToArray(),Points=m.Points.Select(p=>p.ToArray()).ToList()}).ToList(),
        Retouch=source.Retouch.Select(s=>new RetouchStroke{Type=s.Type,X=s.X,Y=s.Y,Sx=s.Sx,Sy=s.Sy,Size=s.Size,Feather=s.Feather,Opacity=s.Opacity}).ToList(),
        Removals=source.Removals.Select(r=>new RemovalOperation{Size=r.Size,Points=r.Points.Select(p=>p.ToArray()).ToList(),Breaks=r.Breaks.ToList()}).ToList()
    };
    static ColorAdjustment CopyColor(ColorAdjustment c)=>new(){H=c.H,S=c.S,L=c.L};
}
