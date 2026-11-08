using Tayf.Core;
namespace Lumora.Application.Services;
public sealed class MotionHistory
{
    readonly List<string> entries=[];int cursor=-1;long bytes;
    public bool CanUndo=>cursor>0;public bool CanRedo=>cursor>=0&&cursor<entries.Count-1;
    public MotionHistory()=>Reset(new());
    public void Reset(MotionProject project){entries.Clear();cursor=-1;bytes=0;Record(Json.Write(project));}
    public void Record(string snapshot)
    {
        if(cursor>=0&&entries[cursor]==snapshot)return;
        while(entries.Count>cursor+1){bytes-=entries[^1].Length*2L;entries.RemoveAt(entries.Count-1);}
        entries.Add(snapshot);bytes+=snapshot.Length*2L;cursor=entries.Count-1;
        while(entries.Count>1&&(entries.Count>20||bytes>4_000_000)){bytes-=entries[0].Length*2L;entries.RemoveAt(0);cursor--;}
    }
    public MotionProject? Step(int direction){int next=cursor+Math.Sign(direction);if(next<0||next>=entries.Count||next==cursor)return null;cursor=next;return Json.Read<MotionProject>(entries[cursor]);}
}
