using Lumora.Application.Contracts;
using Lumora.Shared.Imaging;
using Tayf.Core;
namespace Lumora.Application.Services;

/// <summary>Reuse a decoded video frame for colour, crop, rotation and filter previews.</summary>
public sealed class MotionFramePreview(IVideoService videos)
{
    readonly SemaphoreSlim gate=new(1,1);string? source;double position;long stamp;ImageFrame? frame,small;
    public async Task<ImageFrame> RenderAsync(string path,double time,Recipe recipe,int side,CancellationToken token=default)
    {
        var snapshot=RecipeSnapshot.Capture(recipe);await gate.WaitAsync(token);
        try
        {
            long currentStamp=File.GetLastWriteTimeUtc(path).Ticks;
            if(frame is null||source!=path||position!=time||stamp!=currentStamp)
            {frame=await videos.FrameAsync(path,time,900,token);source=path;position=time;stamp=currentStamp;small=null;}
            var input=frame;
            if(side<=640){small??=await Task.Run(()=>SharedImageIO.Resize(frame,640),token);input=small;}
            return await Task.Run(()=>ImageEngine.Render(input,snapshot,token),token);
        }
        finally{gate.Release();}
    }
    public static Recipe DisplayRecipe(MotionClip clip){var recipe=RecipeSnapshot.Capture(clip.Recipe);recipe.Set("quarterTurns",recipe.Get("quarterTurns")+clip.Turns);return recipe;}
}
