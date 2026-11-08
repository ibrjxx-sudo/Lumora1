using System.IO.Compression;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
string mode=args[0],models=Path.GetFullPath(args[1]);
if(mode=="restoreapk")
{
    string destination=Path.GetFullPath(args[2]);Directory.CreateDirectory(destination);
    using var inventory=JsonDocument.Parse(File.ReadAllText(args[3]));using var archive=ZipFile.OpenRead(models);
    foreach(var model in inventory.RootElement.EnumerateArray())
    {
        string name=model.GetProperty("file").GetString()!,expected=model.GetProperty("sha256").GetString()!,target=Path.Combine(destination,name);
        if(Path.GetFileName(name)!=name)throw new Exception("Invalid model filename");
        bool Valid(){if(!File.Exists(target)||new FileInfo(target).Length!=model.GetProperty("bytes").GetInt64())return false;using var file=File.OpenRead(target);return Convert.ToHexString(SHA256.HashData(file)).Equals(expected,StringComparison.OrdinalIgnoreCase);}
        if(Valid()){Console.WriteLine("Verified offline model: "+name);continue;}
        var entries=archive.Entries.Where(e=>e.Name==name+".br"||e.Name==name).ToArray();if(entries.Length!=1)throw new Exception("Expected one model: "+name);
        string temporary=target+"."+Guid.NewGuid().ToString("N")+".preparing";
        try
        {
            using(var encoded=entries[0].Open())using(Stream input=entries[0].Name.EndsWith(".br")?new BrotliStream(encoded,CompressionMode.Decompress,true):encoded)
            using(var outputFile=File.Create(temporary))input.CopyTo(outputFile,1024*1024);
            using(var file=File.OpenRead(temporary))if(file.Length!=model.GetProperty("bytes").GetInt64()||!Convert.ToHexString(SHA256.HashData(file)).Equals(expected,StringComparison.OrdinalIgnoreCase))throw new Exception("Model integrity check failed: "+name);
            File.Move(temporary,target,true);Console.WriteLine("Prepared offline model: "+name);
        }
        finally{if(File.Exists(temporary))File.Delete(temporary);}
    }
    return;
}
if(mode=="auditapk")
{
    using var inventory=JsonDocument.Parse(File.ReadAllText(args[2]));using var archive=ZipFile.OpenRead(models);var auditReports=new List<object>();long total=0;
    foreach(var model in inventory.RootElement.EnumerateArray())
    {
        string name=model.GetProperty("file").GetString()!,expected=model.GetProperty("sha256").GetString()!;long expectedBytes=model.GetProperty("bytes").GetInt64();
        var entry=archive.GetEntry("assets/Models/"+name+".br")??archive.GetEntry("assets/Models/"+name)??throw new Exception("Missing offline model: "+name);
        using var encoded=entry.Open();using Stream input=entry.FullName.EndsWith(".br")?new BrotliStream(encoded,CompressionMode.Decompress,true):encoded;
        using var digest=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);byte[] buffer=new byte[1024*1024];long bytes=0;int read;
        while((read=input.Read(buffer))>0){digest.AppendData(buffer,0,read);bytes+=read;}
        string sha=Convert.ToHexString(digest.GetHashAndReset());if(bytes!=expectedBytes||!sha.Equals(expected,StringComparison.OrdinalIgnoreCase))throw new Exception("Offline model changed: "+name);
        total+=entry.CompressedLength;auditReports.Add(new{Name=name,Path=entry.FullName,RawBytes=bytes,EncodedBytes=entry.Length,StoredBytes=entry.CompressedLength,Sha256=sha});
    }
    string json=JsonSerializer.Serialize(new{Passed=true,Models=auditReports,ApkBytes=new FileInfo(models).Length,ModelStoredBytes=total,AppWithoutModelPayloadBytes=new FileInfo(models).Length-total},new JsonSerializerOptions{WriteIndented=true});
    File.WriteAllText(args[3],json);Console.WriteLine(json);return;
}
string[] names=["subject.onnx","lama.onnx","ggml-large-v3-turbo-q5_0.bin","ggml-silero-v6.2.0.bin"];
if(mode=="sample")
{
    var results=new List<object>();
    foreach(string name in names.Take(3))
    {
        using var input=File.OpenRead(Path.Combine(models,name));input.Position=input.Length/2;var sample=new byte[8*1024*1024];input.ReadExactly(sample);
        foreach(int quality in new[]{5,7,9})
        {
            var clock=Stopwatch.StartNew();using var buffer=new MemoryStream();
            using(var brotli=new BrotliStream(buffer,new BrotliCompressionOptions{Quality=quality},true))brotli.Write(sample);
            results.Add(new{Name=name,Quality=quality,Bytes=buffer.Length,Ratio=buffer.Length/(double)sample.Length,Seconds=clock.Elapsed.TotalSeconds});
        }
    }
    Console.WriteLine(JsonSerializer.Serialize(results,new JsonSerializerOptions{WriteIndented=true}));return;
}
string output=Path.GetFullPath(args[2]);Directory.CreateDirectory(output);int selected=int.Parse(args[3]);var reports=new List<object>();
foreach(string name in names)
{
    string original=Path.Combine(models,name),target=Path.Combine(output,name+".br");var clock=Stopwatch.StartNew();
    using(var source=File.OpenRead(original))using(var file=File.Create(target))using(var compressed=new BrotliStream(file,new BrotliCompressionOptions{Quality=selected},false))source.CopyTo(compressed,1024*1024);
    byte[] expected;using(var source=File.OpenRead(original))expected=SHA256.HashData(source);
    byte[] actual;using(var file=File.OpenRead(target))using(var decompressed=new BrotliStream(file,CompressionMode.Decompress))actual=SHA256.HashData(decompressed);
    if(!expected.SequenceEqual(actual))throw new Exception("Lossless verification failed: "+name);
    reports.Add(new{Name=name,RawBytes=new FileInfo(original).Length,CompressedBytes=new FileInfo(target).Length,Sha256=Convert.ToHexString(actual),Seconds=clock.Elapsed.TotalSeconds});
}
Console.WriteLine(JsonSerializer.Serialize(reports,new JsonSerializerOptions{WriteIndented=true}));
