using System.Security.Cryptography;
using System.Text.Json;
namespace Tayf.Core;
/// <summary>Offline verification of signed release files. No user files are read.</summary>
public static class ReleaseIntegrity
{
    public static async Task VerifyAsync(string directory,byte[] manifest,byte[] signature,string publicKey,string version,CancellationToken token=default)
    {
        if(manifest.Length is <2 or >262144||signature.Length>1024)throw new InvalidDataException("Invalid release manifest.");
        using(var rsa=RSA.Create()){rsa.ImportFromPem(publicKey);if(!rsa.VerifyData(manifest,signature,HashAlgorithmName.SHA256,RSASignaturePadding.Pss))throw new InvalidDataException("Release signature mismatch.");}
        using var json=JsonDocument.Parse(manifest);var root=json.RootElement;
        if(root.GetProperty("version").GetString()!=version||root.GetProperty("algorithm").GetString()!="RSA-PSS-SHA256")throw new InvalidDataException("Release version mismatch.");
        string basePath=Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);int count=0;
        foreach(var entry in root.GetProperty("files").EnumerateArray())
        {
            token.ThrowIfCancellationRequested();string name=entry.GetProperty("path").GetString()??"";
            if(++count>1000||string.IsNullOrWhiteSpace(name)||Path.IsPathRooted(name)||name.Contains('\\')||name.Split('/').Any(p=>p is ".." or "." or "")||!seen.Add(name))throw new InvalidDataException("Unsafe release file path.");
            string file=Path.GetFullPath(Path.Combine(basePath,name));if(!file.StartsWith(basePath,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Release file outside installation.");
            for(string? component=file;component is not null&&component.Length>basePath.Length;component=Path.GetDirectoryName(component))if((File.GetAttributes(component)&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException("Redirected release file.");
            await using var stream=new FileStream(file,FileMode.Open,FileAccess.Read,FileShare.Read,1024*1024,FileOptions.Asynchronous|FileOptions.SequentialScan);
            if(stream.Length!=entry.GetProperty("bytes").GetInt64())throw new InvalidDataException("Release file size mismatch: "+name);
            string hash=Convert.ToHexString(await SHA256.HashDataAsync(stream,token));if(!hash.Equals(entry.GetProperty("sha256").GetString(),StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Modified release file: "+name);
        }
        if(!seen.Contains("Lumora.exe"))throw new InvalidDataException("Missing executable verification.");
    }
}
