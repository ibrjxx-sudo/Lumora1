using Lumora.Application.Contracts;
using Tayf.Core;
namespace Lumora.Application.Services;

/// <summary>One render at a time, one replaceable pending request, then full-quality refinement after input stops.</summary>
public sealed class InteractivePreview(Func<string,Recipe,int,CancellationToken,Task<ImageFrame>> render,Func<ImageFrame,long,Task> publish,Action<Exception> error):IDisposable
{
    public InteractivePreview(IImageProcessor processor,Func<ImageFrame,long,Task> publish,Action<Exception> error):this(processor.PreviewAsync,publish,error){}
    readonly object gate=new();Request? pending;CancellationTokenSource? active;TaskCompletionSource wake=Signal();bool running,refining,disposed;long epoch,sequence;
    sealed record Request(string Path,Recipe Recipe,int Side,long Epoch,long Sequence);
    static TaskCompletionSource Signal()=>new(TaskCreationOptions.RunContinuationsAsynchronously);
    public bool IsCurrent(long value){lock(gate)return !disposed&&epoch==value;}
    public bool HasPendingWork{get{lock(gate)return running||pending is not null;}}
    public void Enqueue(string path,Recipe recipe,int side,bool uncropped=false)
    {
        var snapshot=RecipeSnapshot.Capture(recipe);if(uncropped)snapshot.Crop=[0,0,1,1];bool start;
        lock(gate)
        {
            ObjectDisposedException.ThrowIf(disposed,this);pending=new(path,snapshot,side,epoch,++sequence);wake.TrySetResult();
            if(refining)active?.Cancel();
            start=!running;if(start)running=true;
        }
        if(start)_=PumpAsync();
    }
    public void Cancel(){lock(gate){epoch++;pending=null;active?.Cancel();wake.TrySetResult();}}
    async Task PumpAsync()
    {
        while(true)
        {
            Request request;CancellationTokenSource cancellation;
            lock(gate)
            {
                if(disposed||pending is null){running=false;return;}
                request=pending;pending=null;active=cancellation=new();refining=false;wake=Signal();
            }
            try
            {
                var fast=await render(request.Path,request.Recipe,Math.Min(640,request.Side),cancellation.Token);
                if(IsCurrent(request.Epoch))await publish(fast,request.Epoch);
                Task input;lock(gate){if(pending is not null)continue;input=wake.Task;}
                await Task.WhenAny(Task.Delay(160,cancellation.Token),input);cancellation.Token.ThrowIfCancellationRequested();
                lock(gate){if(pending is not null||epoch!=request.Epoch)continue;refining=true;}
                if(request.Side>640)
                {
                    var full=await render(request.Path,request.Recipe,request.Side,cancellation.Token);
                    bool latest;lock(gate)latest=epoch==request.Epoch&&sequence==request.Sequence&&!disposed;
                    if(latest)await publish(full,request.Epoch);
                }
            }
            catch(OperationCanceledException){}
            catch(Exception exception){if(IsCurrent(request.Epoch))error(exception);}
            finally{lock(gate){if(ReferenceEquals(active,cancellation)){active=null;refining=false;}}cancellation.Dispose();}
        }
    }
    public void Dispose(){lock(gate){disposed=true;epoch++;pending=null;active?.Cancel();wake.TrySetResult();}}
}
