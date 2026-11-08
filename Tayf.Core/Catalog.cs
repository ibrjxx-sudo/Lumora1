using System.IO;
using System.Security.Cryptography;

namespace Tayf.Core;

public sealed class Catalog
{
    readonly object gate=new();
    readonly string file;
    public string DirectoryPath {get;}
    public string OriginalsPath=>Path.Combine(DirectoryPath,"Originals");
    public CatalogData Data {get;private set;}
    public bool RecoveredBackup {get;}
    public Catalog(string directory)
    {
        DirectoryPath=Path.GetFullPath(directory);Directory.CreateDirectory(OriginalsPath);Directory.CreateDirectory(Path.Combine(DirectoryPath,"Thumbnails"));file=Path.Combine(DirectoryPath,"catalog.json");
        if(File.Exists(file))
        {
            try{Data=Read(file);}
            catch(Exception ex)when(ex is System.Text.Json.JsonException or InvalidDataException)
            {
                if(!File.Exists(file+".bak"))throw new InvalidDataException("تعذر قراءة مكتبة الصور. الملف محفوظ؛ لم تُنشأ مكتبة بديلة فوقه.",ex);
                Data=Read(file+".bak");RecoveredBackup=true;
                File.Copy(file,file+".corrupt-"+DateTime.UtcNow.ToString("yyyyMMddHHmmssfff"),false);
                File.Copy(file+".bak",file,true);
            }
        }else Data=new();
    }
    static CatalogData Read(string path)
    {
        var data=Json.Read<CatalogData>(File.ReadAllText(path));
        if(data.Version!=1||data.Media is null||data.Albums is null||data.Presets is null)throw new InvalidDataException("صيغة مكتبة غير مدعومة.");
        foreach(var media in data.Media)
        {
            if(media is null||media.Recipe is null||media.History is null||media.Versions is null||string.IsNullOrWhiteSpace(media.Id))throw new InvalidDataException("بيانات سجل الصورة في المكتبة غير صالحة.");
            media.Recipe.Validate();
            foreach(var revision in media.History.Concat(media.Versions)){if(revision?.Recipe is null)throw new InvalidDataException("بيانات تاريخ التعديل غير صالحة.");revision.Recipe.Validate();}
            media.HistoryIndex=Math.Clamp(media.HistoryIndex,0,Math.Max(0,media.History.Count-1));
        }
        foreach(var preset in data.Presets){if(preset?.Recipe is null)throw new InvalidDataException("بيانات الفلتر في المكتبة غير صالحة.");preset.Recipe.Validate();}
        return data;
    }
    void SaveLocked()
    {
        string temp=file+"."+Guid.NewGuid().ToString("N")+".tmp";
        try
        {
            using(var stream=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None))using(var writer=new StreamWriter(stream)){writer.Write(Json.Write(Data));writer.Flush();stream.Flush(true);}
            if(File.Exists(file))File.Replace(temp,file,file+".bak",true);else File.Move(temp,file);
        }
        finally{if(File.Exists(temp))File.Delete(temp);}
    }
    public void Update(Action<CatalogData> mutation)
    {lock(gate){string previous=Json.Write(Data);try{mutation(Data);SaveLocked();}catch{Data=Json.Read<CatalogData>(previous);throw;}}}
    public MediaItem? Find(string id){lock(gate)return Data.Media.FirstOrDefault(m=>m.Id==id);}
    public MediaItem Import(string source)
    {
        string full=Path.GetFullPath(source);var image=ImageIO.Load(full,1100);string id;
        using(var stream=File.OpenRead(full))id=Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
        lock(gate)
        {
            var old=Data.Media.FirstOrDefault(m=>m.Id==id);if(old is not null){Update(d=>d.Media.First(m=>m.Id==id).Deleted=false);return old;}
            string original=Path.Combine(OriginalsPath,id+Path.GetExtension(full).ToLowerInvariant()),thumb=Path.Combine(DirectoryPath,"Thumbnails",id+".png");
            if(!File.Exists(original))
            {
                File.Copy(full,original,false);
                using var copied=File.OpenRead(original);
                if(!Convert.ToHexString(SHA256.HashData(copied)).Equals(id,StringComparison.OrdinalIgnoreCase))
                    throw new IOException("تغيّر الملف أثناء الاستيراد. أعد المحاولة بعد اكتمال حفظه.");
            }
            // Probe dimensions without decoding camera originals into a full RGBA allocation.
            var dimensions=ImageIO.Probe(original);var thumbnail=ImageIO.Resize(image,260);
            if(!File.Exists(thumb))File.WriteAllBytes(thumb,ImageIO.EncodePng(thumbnail));
            var item=new MediaItem{Id=id,Name=Path.GetFileName(full),Source=full,Original=original,Width=dimensions.Width,Height=dimensions.Height,Thumbnail=thumb,History=[new(){Name="الأصل",Recipe=new()}]};
            Update(d=>d.Media.Add(item));return item;
        }
    }
    public void Record(string id,Recipe recipe,string name)=>RecordMany([id],recipe,name);
    public void RecordMany(IReadOnlyCollection<string> ids,Recipe recipe,string name,bool preserveSpatial=false)
    {
        recipe.Validate();var unique=ids.Distinct().ToArray();if(unique.Length==0)return;Update(d=>{foreach(var id in unique){var media=d.Media.First(m=>m.Id==id);var next=preserveSpatial?PresetMixer.Apply(media.Recipe,media.Recipe,recipe,1):recipe.Clone();if(Json.Write(media.Recipe)==Json.Write(next))continue;
            if(media.HistoryIndex<media.History.Count-1)media.History.RemoveRange(media.HistoryIndex+1,media.History.Count-media.HistoryIndex-1);
            media.Recipe=next;media.History.Add(new(){Name=name,Recipe=next.Clone()});if(media.History.Count>400)media.History.RemoveAt(0);media.HistoryIndex=media.History.Count-1;}});
    }
    public Recipe Step(string id,int direction)
    {
        Update(d=>{var item=d.Media.First(m=>m.Id==id);if(item.History.Count==0)return;item.HistoryIndex=Math.Clamp(item.HistoryIndex+direction,0,item.History.Count-1);item.Recipe=item.History[item.HistoryIndex].Recipe.Clone();});return Find(id)!.Recipe.Clone();
    }
    public void SaveVersion(string id,string name,Recipe recipe)=>Update(d=>d.Media.First(m=>m.Id==id).Versions.Add(new(){Name=name,Recipe=recipe.Clone()}));
    public void Select(string id)=>Update(d=>d.Current=id);
    public void Export(string id,Recipe recipe,string destination,ExportSettings settings,CancellationToken token=default)
    {
        var media=Find(id)??throw new InvalidOperationException("الصورة غير موجودة.");string target=Path.GetFullPath(destination),relative=Path.GetRelativePath(OriginalsPath,target);
        if(!relative.StartsWith("..",StringComparison.Ordinal)||Data.Media.Any(m=>Path.GetFullPath(m.Source).Equals(target,StringComparison.OrdinalIgnoreCase)))throw new InvalidOperationException("اختر ملفًا جديدًا؛ لا يُسمح بالكتابة فوق الأصل.");
        if(!new[]{"png","jpeg","jpg","webp","tiff","tif","bmp","gif"}.Contains(settings.Format.ToLowerInvariant()))throw new InvalidDataException("صيغة تصدير غير مدعومة.");
        if(settings.LongEdge is <0 or >12000)throw new InvalidDataException("مقاس التصدير غير صالح.");
        token.ThrowIfCancellationRequested();var frame=ImageIO.Load(media.Original);var result=ImageEngine.Render(frame,recipe,token);token.ThrowIfCancellationRequested();
        string temp=target+"."+Guid.NewGuid().ToString("N")+".tmp";try{ImageIO.Save(result,temp,settings);token.ThrowIfCancellationRequested();File.Move(temp,target,true);}finally{if(File.Exists(temp))File.Delete(temp);}
    }
    public static void SavePreset(string path,Recipe recipe){recipe.Validate();File.WriteAllText(path,Json.Write(new PresetFile{Recipe=recipe}));}
    public static Recipe ReadPreset(string path){var info=new FileInfo(path);if(info.Length>5_000_000)throw new InvalidDataException("ملف الوصفة كبير جدًا.");var file=Json.Read<PresetFile>(File.ReadAllText(path));if(file.Format!="tayf-csharp-preset"||file.Version!=1)throw new InvalidDataException("صيغة وصفة غير متوافقة.");file.Recipe.Validate();return file.Recipe;}
    public sealed class PresetFile{public string Format{get;set;}="tayf-csharp-preset";public int Version{get;set;}=1;public Recipe Recipe{get;set;}=new();}
}
