using Lumora.Application.Contracts;
using Tayf.Core;

namespace Lumora.Application.Repositories;
/// <summary>Compatible schema-1 repository. UI never performs catalog I/O or encoding synchronously.</summary>
public sealed class JsonCatalogRepository:ICatalogRepository,IDisposable
{
    readonly IAppPaths paths;readonly SemaphoreSlim gate=new(1,1);Catalog? catalog;Mutex? instance;FileStream? lease;
    public JsonCatalogRepository(IAppPaths paths)=>this.paths=paths;
    async Task<T> Read<T>(Func<Catalog,T> action,CancellationToken token){await gate.WaitAsync(token);try{return await Task.Run(()=>{token.ThrowIfCancellationRequested();if(catalog is null){AcquireLease();catalog=new Catalog(paths.DataDirectory);}return action(catalog);},token);}finally{gate.Release();}}
    void AcquireLease()
    {
        if(lease is not null)return;Directory.CreateDirectory(paths.DataDirectory);
        if(OperatingSystem.IsWindows())
        {
            string suffix=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(Path.GetFullPath(paths.DataDirectory))))[..20];instance=new Mutex(false,"Local\\TayfCSharp-"+suffix,out bool fresh);
            if(!fresh){instance.Dispose();instance=null;throw new IOException("Close the other Lumora window before opening this library.");}
        }
        try{lease=new FileStream(Path.Combine(paths.DataDirectory,".lumora-session.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);}
        catch{instance?.Dispose();instance=null;throw;}
    }
    Task Write(Action<Catalog> action,CancellationToken token)=>Read(c=>{action(c);return true;},token);
    public Task<LibraryPage> QueryAsync(LibraryQuery query,int offset,int count,CancellationToken token=default)=>Read(c=>
    {
        if(offset<0||count is <1 or >200)throw new ArgumentOutOfRangeException(nameof(count));IEnumerable<MediaItem> list=c.Data.Media.Where(m=>m.Deleted==query.Trash&&m.Rating>=Math.Clamp(query.MinimumRating,0,5));if(query.FavoritesOnly)list=list.Where(m=>m.Favorite);if(query.Album is not null)list=list.Where(m=>m.Album==query.Album);if(!string.IsNullOrWhiteSpace(query.Search))list=list.Where(m=>m.Name.Contains(query.Search,StringComparison.OrdinalIgnoreCase));list=query.Sort switch{"name"=>list.OrderBy(m=>m.Name),"rating"=>list.OrderByDescending(m=>m.Rating),_=>list.OrderByDescending(m=>m.Added)};var all=list.ToArray();return new LibraryPage(all.Skip(offset).Take(count).Select(m=>new LibraryEntry(m.Id,m.Name,m.Thumbnail,m.Width,m.Height,m.Rating,m.Favorite,m.Deleted,m.Album)).ToArray(),all.Length);
    },token);
    public Task<MediaItem?> FindAsync(string id,CancellationToken token=default)=>Read(c=>c.Find(id)is {} item?Json.Read<MediaItem>(Json.Write(item)):null,token);
    public async Task<MediaItem> ImportAsync(Stream source,string displayName,CancellationToken token=default)
    {
        displayName=Path.GetFileName(displayName);if(string.IsNullOrWhiteSpace(displayName)||displayName.Length>240)throw new ArgumentException("Invalid file name.");string folder=Path.Combine(paths.CacheDirectory,"imports");Directory.CreateDirectory(folder);string staged=Path.Combine(folder,Guid.NewGuid().ToString("N")+Path.GetExtension(displayName));
        try{await using(var target=new FileStream(staged,FileMode.CreateNew,FileAccess.Write,FileShare.None,1024*1024,true)){byte[] buffer=new byte[1024*1024];long total=0;int read;while((read=await source.ReadAsync(buffer,token))>0){if((total+=read)>2_000_000_000)throw new InvalidDataException("Import exceeds 2 GB.");await target.WriteAsync(buffer.AsMemory(0,read),token);}}
            return await Read(c=>{var item=c.Import(staged);c.Update(data=>{var stored=data.Media.First(m=>m.Id==item.Id);stored.Name=displayName;stored.Source=stored.Original;});return Json.Read<MediaItem>(Json.Write(c.Find(item.Id)!));},token);
        }finally{if(File.Exists(staged))File.Delete(staged);}
    }
    public Task SaveEditsAsync(string id,Recipe recipe,string name,CancellationToken token=default){var copy=recipe.Clone();copy.Validate();return Write(c=>c.Record(id,copy,name),token);}
    public Task SaveEditsBatchAsync(IReadOnlyCollection<string> ids,Recipe recipe,string name,CancellationToken token=default){var snapshot=ids.Distinct().ToArray();var copy=recipe.Clone();copy.Validate();return Write(c=>c.RecordMany(snapshot,copy,name,true),token);}
    public Task<Recipe> StepAsync(string id,int direction,CancellationToken token=default)=>Read(c=>c.Step(id,direction),token);
    public Task SetFavoriteAsync(string id,bool value,CancellationToken token=default)=>Write(c=>c.Update(d=>d.Media.First(m=>m.Id==id).Favorite=value),token);
    public Task SetRatingAsync(string id,int value,CancellationToken token=default){if(value is <0 or >5)throw new ArgumentOutOfRangeException(nameof(value));return Write(c=>c.Update(d=>d.Media.First(m=>m.Id==id).Rating=value),token);}
    public Task SetDeletedAsync(string id,bool value,CancellationToken token=default)=>Write(c=>c.Update(d=>d.Media.First(m=>m.Id==id).Deleted=value),token);
    public Task SetAlbumAsync(string id,string album,CancellationToken token=default)=>Write(c=>c.Update(d=>{if(album.Length>0&&!d.Albums.Contains(album))throw new ArgumentException("Album not found.");d.Media.First(m=>m.Id==id).Album=album;}),token);
    public Task<IReadOnlyList<string>> AlbumsAsync(CancellationToken token=default)=>Read<IReadOnlyList<string>>(c=>c.Data.Albums.ToArray(),token);
    public Task AddAlbumAsync(string name,CancellationToken token=default){name=name.Trim();if(name.Length is <1 or >100)throw new ArgumentException("Invalid album name.");return Write(c=>c.Update(d=>{if(!d.Albums.Contains(name))d.Albums.Add(name);}),token);}
    public Task<IReadOnlyList<Preset>> PresetsAsync(CancellationToken token=default)=>Read<IReadOnlyList<Preset>>(c=>Json.Read<List<Preset>>(Json.Write(c.Data.Presets)),token);
    public Task SavePresetAsync(Preset preset,CancellationToken token=default){preset.Recipe.Validate();var copy=Json.Read<Preset>(Json.Write(preset));return Write(c=>c.Update(d=>{d.Presets.RemoveAll(p=>p.Id==copy.Id);d.Presets.Add(copy);}),token);}
    public Task DeletePresetAsync(string id,CancellationToken token=default)=>Write(c=>c.Update(d=>d.Presets.RemoveAll(p=>p.Id==id)),token);
    public Task SaveVersionAsync(string id,string name,Recipe recipe,CancellationToken token=default){var copy=recipe.Clone();copy.Validate();if(string.IsNullOrWhiteSpace(name)||name.Length>100)throw new ArgumentException("Invalid version name.");return Write(c=>c.SaveVersion(id,name,copy),token);}
    public Task ExportLibraryAsync(string destination,CancellationToken token=default)=>Read(c=>{LibraryArchive.Export(c,destination,token);return true;},token);
    public Task ImportLibraryAsync(string archive,CancellationToken token=default)=>Read(c=>{LibraryArchive.Import(c,archive,paths.CacheDirectory,token);return true;},token);
    public void Dispose(){lease?.Dispose();instance?.Dispose();gate.Dispose();}
}
