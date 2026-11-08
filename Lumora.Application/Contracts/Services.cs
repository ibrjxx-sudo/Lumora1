using Tayf.Core;

namespace Lumora.Application.Contracts;
public interface IAppPaths
{
    string DataDirectory{get;}string CacheDirectory{get;}string ModelsDirectory{get;}string VideoDirectory{get;}string LogsDirectory{get;}
}
public sealed record LibraryQuery(string Search="",string? Album=null,bool FavoritesOnly=false,bool Trash=false,int MinimumRating=0,string Sort="newest");
public sealed record LibraryEntry(string Id,string Name,string Thumbnail,int Width,int Height,int Rating,bool Favorite,bool Deleted,string Album);
public sealed record LibraryPage(IReadOnlyList<LibraryEntry> Items,int Total);
public interface ICatalogRepository
{
    Task<LibraryPage> QueryAsync(LibraryQuery query,int offset,int count,CancellationToken token=default);
    Task<MediaItem?> FindAsync(string id,CancellationToken token=default);
    Task<MediaItem> ImportAsync(Stream source,string displayName,CancellationToken token=default);
    Task SaveEditsAsync(string id,Recipe recipe,string name,CancellationToken token=default);
    Task SaveEditsBatchAsync(IReadOnlyCollection<string> ids,Recipe recipe,string name,CancellationToken token=default);
    Task<Recipe> StepAsync(string id,int direction,CancellationToken token=default);
    Task SetFavoriteAsync(string id,bool value,CancellationToken token=default);
    Task SetRatingAsync(string id,int value,CancellationToken token=default);
    Task SetDeletedAsync(string id,bool value,CancellationToken token=default);
    Task SetAlbumAsync(string id,string album,CancellationToken token=default);
    Task<IReadOnlyList<string>> AlbumsAsync(CancellationToken token=default);
    Task AddAlbumAsync(string name,CancellationToken token=default);
    Task<IReadOnlyList<Preset>> PresetsAsync(CancellationToken token=default);
    Task SavePresetAsync(Preset preset,CancellationToken token=default);
    Task DeletePresetAsync(string id,CancellationToken token=default);
    Task SaveVersionAsync(string id,string name,Recipe recipe,CancellationToken token=default);
    Task ExportLibraryAsync(string destination,CancellationToken token=default);
    Task ImportLibraryAsync(string archive,CancellationToken token=default);
}
public interface IImageProcessor
{
    Task<ImageFrame> PreviewAsync(string path,Recipe recipe,int side=1100,CancellationToken token=default);
    Task ExportAsync(string path,Recipe recipe,string target,ExportSettings settings,CancellationToken token=default);
    Task<ImageFrame> ReadAsync(string path,int side=1100,CancellationToken token=default);
    Task Export4KAsync(string path,Recipe recipe,string target,CancellationToken token=default,IProgress<double>? progress=null);
}
public interface IPhotoIntelligence:IDisposable
{
    Task<SubjectMatte> SegmentAsync(ImageFrame frame,CancellationToken token=default);
    Task<FaceAnchor?> DetectFaceAsync(ImageFrame frame,CancellationToken token=default);
    Task<ImageFrame> RemoveAsync(ImageFrame frame,IReadOnlyList<RemovalOperation> removals,CancellationToken token=default);
    Task ReleaseCachesAsync(CancellationToken token=default);
    Task PrepareRemovalAsync(CancellationToken token=default);
}
public sealed record VideoInfo(string Path,int Width,int Height,double Duration,double Fps,bool HasAudio);
public interface IVideoService
{
    Task<VideoInfo> ProbeAsync(string path,CancellationToken token=default);
    Task<ImageFrame> FrameAsync(string path,double time,int side=1100,CancellationToken token=default);
    Task ExportAsync(MotionProject project,string target,IProgress<double>? progress=null,CancellationToken token=default);
    IReadOnlySet<string> Transitions{get;}
}
public interface ICaptionService
{
    Task<List<CaptionCue>> GenerateAsync(string video,double start,double end,string language,string terms,bool denoise,IProgress<double>? progress=null,CancellationToken token=default);
}
public interface IDeviceFiles
{
    Task<IReadOnlyList<PickedAsset>> PickImagesAsync(CancellationToken token=default);
    Task<IReadOnlyList<PickedAsset>> PickVideosAsync(CancellationToken token=default);
    Task<PickedAsset?> CapturePhotoAsync(CancellationToken token=default);
    Task<PickedAsset?> PickPresetAsync(CancellationToken token=default);
    Task<PickedAsset?> PickDocumentAsync(string title,CancellationToken token=default);
    Task<PickedAsset?> PickModelPackAsync(CancellationToken token=default);
    Task SaveAsync(string localFile,string suggestedName,CancellationToken token=default);
    Task ShareAsync(string localFile,string title,CancellationToken token=default);
}
public sealed record PickedAsset(string Name,Func<CancellationToken,Task<Stream>> Open);
public interface IAppLog{void Error(string operation,Exception error);}
public sealed record UserSettings(string Language="ar",string Theme="dark",int PreviewSide=1100);
public interface ISettingsStore{Task<UserSettings> ReadAsync(CancellationToken token=default);Task WriteAsync(UserSettings settings,CancellationToken token=default);}
