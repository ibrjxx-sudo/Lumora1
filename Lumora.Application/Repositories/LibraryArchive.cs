using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Tayf.Core;
namespace Lumora.Application.Repositories;
/// <summary>Portable schema-1 archives. Imports merge and rebase paths without replacing existing edits or originals.</summary>
internal static partial class LibraryArchive
{
    [GeneratedRegex("^(Originals|Thumbnails|Backgrounds)/[a-fA-F0-9]{64}(\\.[a-zA-Z0-9]{1,12})?$")]
    private static partial Regex AssetName();
    static IEnumerable<Recipe> Recipes(CatalogData data)=>data.Media.SelectMany(m=>new[]{m.Recipe}.Concat(m.History.Select(h=>h.Recipe)).Concat(m.Versions.Select(v=>v.Recipe))).Concat(data.Presets.Select(p=>p.Recipe));
    public static void Export(Catalog catalog,string destination,CancellationToken token)
    {
        var data=Json.Read<CatalogData>(Json.Write(catalog.Data));var assets=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
        string Asset(string path,string folder,string? id=null)
        {
            token.ThrowIfCancellationRequested();if(!File.Exists(path))throw new FileNotFoundException("A library asset is missing.",path);
            if(id is null){using var stream=File.OpenRead(path);id=Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();}
            string name=folder+"/"+id+Path.GetExtension(path).ToLowerInvariant();if(!AssetName().IsMatch(name))throw new InvalidDataException("Unsupported asset name.");assets.TryAdd(name,path);return name;
        }
        foreach(var media in data.Media){media.Original=Asset(media.Original,"Originals",media.Id);media.Source=media.Original;media.Thumbnail=Asset(media.Thumbnail,"Thumbnails",media.Id);}
        foreach(var recipe in Recipes(data))if(recipe.BackgroundImage is {} background)recipe.BackgroundImage=Asset(background,"Backgrounds");
        string target=Path.GetFullPath(destination);if(assets.Values.Any(p=>Path.GetFullPath(p).Equals(target,StringComparison.OrdinalIgnoreCase)))throw new IOException("Cannot overwrite a library asset.");Directory.CreateDirectory(Path.GetDirectoryName(target)!);string temporary=target+"."+Guid.NewGuid().ToString("N")+".tmp";
        try
        {
            using(var output=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None))using(var zip=new ZipArchive(output,ZipArchiveMode.Create))
            {
                using(var writer=new StreamWriter(zip.CreateEntry("catalog.json",CompressionLevel.Optimal).Open()))writer.Write(Json.Write(data));
                foreach(var asset in assets){token.ThrowIfCancellationRequested();using var input=File.OpenRead(asset.Value);using var entry=zip.CreateEntry(asset.Key,CompressionLevel.NoCompression).Open();Copy(input,entry,new FileInfo(asset.Value).Length,token);}
            }
            token.ThrowIfCancellationRequested();File.Move(temporary,target,true);
        }finally{if(File.Exists(temporary))File.Delete(temporary);}
    }
    public static void Import(Catalog catalog,string archive,string cache,CancellationToken token)
    {
        string stage=Path.Combine(cache,"library-transfer",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(stage);
        try
        {
            using var zip=ZipFile.OpenRead(archive);if(zip.Entries.Count is <1 or >100000)throw new InvalidDataException("Invalid archive entry count.");var entries=new Dictionary<string,ZipArchiveEntry>(StringComparer.OrdinalIgnoreCase);long total=0;
            foreach(var entry in zip.Entries)
            {
                token.ThrowIfCancellationRequested();string name=entry.FullName;if(name!="catalog.json"&&!AssetName().IsMatch(name)||!entries.TryAdd(name,entry))throw new InvalidDataException("Unsafe or duplicate archive entry.");
                if(entry.Length<0||entry.Length>2_000_000_000||(total+=entry.Length)>40_000_000_000)throw new InvalidDataException("Archive exceeds the import budget.");
            }
            if(!entries.TryGetValue("catalog.json",out var manifest)||manifest.Length>128_000_000)throw new InvalidDataException("Invalid library manifest.");
            foreach(var row in entries)
            {
                string target=Path.Combine(stage,row.Key.Replace('/',Path.DirectorySeparatorChar));Directory.CreateDirectory(Path.GetDirectoryName(target)!);using var input=row.Value.Open();using var output=new FileStream(target,FileMode.CreateNew);Copy(input,output,row.Value.Length,token);
            }
            var incoming=new Catalog(stage).Data;var ids=new HashSet<string>(StringComparer.Ordinal);foreach(var item in incoming.Media)
            {
                if(item.Id.Length!=64||!item.Id.All(Uri.IsHexDigit)||!ids.Add(item.Id))throw new InvalidDataException("Invalid or duplicate media identity.");
                string source=Resolve(item.Original,"Originals",entries,stage);using var stream=File.OpenRead(source);if(!Convert.ToHexString(SHA256.HashData(stream)).Equals(item.Id,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Original checksum mismatch.");
                _=Resolve(item.Thumbnail,"Thumbnails",entries,stage);
            }
            foreach(var recipe in Recipes(incoming))if(recipe.BackgroundImage is {} background)
            {string path=Resolve(background,"Backgrounds",entries,stage);using var input=File.OpenRead(path);if(!Convert.ToHexString(SHA256.HashData(input)).Equals(Path.GetFileNameWithoutExtension(background),StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Background checksum mismatch.");}
            // Validation finishes before anything is merged into the live catalogue.
            foreach(var row in entries.Where(v=>v.Key!="catalog.json"))
            {
                token.ThrowIfCancellationRequested();string destination=Path.Combine(catalog.DirectoryPath,row.Key.Replace('/',Path.DirectorySeparatorChar));Directory.CreateDirectory(Path.GetDirectoryName(destination)!);if(!File.Exists(destination))File.Copy(Path.Combine(stage,row.Key.Replace('/',Path.DirectorySeparatorChar)),destination,false);
            }
            foreach(var item in incoming.Media){item.Original=Path.Combine(catalog.DirectoryPath,item.Original.Replace('/',Path.DirectorySeparatorChar));item.Source=item.Original;item.Thumbnail=Path.Combine(catalog.DirectoryPath,item.Thumbnail.Replace('/',Path.DirectorySeparatorChar));}
            foreach(var recipe in Recipes(incoming))if(recipe.BackgroundImage is {} background)recipe.BackgroundImage=Path.Combine(catalog.DirectoryPath,background.Replace('/',Path.DirectorySeparatorChar));
            token.ThrowIfCancellationRequested();catalog.Update(data=>
            {
                foreach(var item in incoming.Media)
                {
                    var existing=data.Media.FirstOrDefault(v=>v.Id==item.Id);if(existing is null)data.Media.Add(item);else
                    {existing.Versions.Add(new(){Name="Imported · "+DateTime.UtcNow.ToString("u"),Recipe=item.Recipe.Clone()});existing.Versions.AddRange(item.Versions);existing.Versions.AddRange(item.History);}
                }
                foreach(string album in incoming.Albums)if(!data.Albums.Contains(album))data.Albums.Add(album);
                foreach(var preset in incoming.Presets){if(data.Presets.Any(p=>p.Id==preset.Id))preset.Id=Guid.NewGuid().ToString("N");data.Presets.Add(preset);}
            });
        }
        finally{string root=Path.GetFullPath(Path.Combine(cache,"library-transfer"))+Path.DirectorySeparatorChar;if(Path.GetFullPath(stage).StartsWith(root,StringComparison.OrdinalIgnoreCase))try{Directory.Delete(stage,true);}catch(IOException){}}
    }
    static string Resolve(string name,string folder,Dictionary<string,ZipArchiveEntry> entries,string root)
    {if(!name.StartsWith(folder+"/",StringComparison.Ordinal)||!AssetName().IsMatch(name)||!entries.ContainsKey(name))throw new InvalidDataException("A manifest asset is missing or unsafe.");return Path.Combine(root,name.Replace('/',Path.DirectorySeparatorChar));}
    static void Copy(Stream input,Stream output,long expected,CancellationToken token)
    {byte[] buffer=new byte[1024*1024];long total=0;int count;while((count=input.Read(buffer))>0){token.ThrowIfCancellationRequested();if((total+=count)>expected)throw new InvalidDataException("Expanded archive size mismatch.");output.Write(buffer,0,count);}if(total!=expected)throw new InvalidDataException("Truncated archive entry.");}
}
