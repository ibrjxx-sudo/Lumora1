using System.IO;
using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using System.Globalization;

namespace Tayf.Core;

public static class PresetPackage
{
    public static void Save(string path,Preset preset)
    {
        if(Path.GetExtension(path).Equals(".xmp",StringComparison.OrdinalIgnoreCase)){XmpPreset.Save(path,preset);return;}if(Path.GetExtension(path).Equals(".cube",StringComparison.OrdinalIgnoreCase)){XmpPreset.SaveCube(path,preset);return;}
        preset.Recipe.Validate();using var stream=new FileStream(path,FileMode.Create);using var archive=new ZipArchive(stream,ZipArchiveMode.Create);var entry=archive.CreateEntry("preset.xml");using var writer=new StreamWriter(entry.Open(),Encoding.UTF8);
        var root=new XElement("LumoraPreset",new XAttribute("version",1),new XElement("Name",preset.Name),new XElement("Category",preset.Category));
        foreach(var value in preset.Recipe.Values.OrderBy(v=>v.Key))root.Add(new XElement("Control",new XAttribute("name",value.Key),value.Value.ToString("R",CultureInfo.InvariantCulture)));
        foreach(var channel in preset.Recipe.Curves)root.Add(new XElement("Curve",new XAttribute("channel",channel.Key),channel.Value.Select(p=>new XElement("Point",new XAttribute("x",p.X.ToString("R",CultureInfo.InvariantCulture)),new XAttribute("y",p.Y.ToString("R",CultureInfo.InvariantCulture))))));
        for(int i=0;i<8;i++)root.Add(ColorElement("Hsl",i.ToString(CultureInfo.InvariantCulture),preset.Recipe.Hsl[i]));foreach(var grade in preset.Recipe.Grading)root.Add(ColorElement("Grade",grade.Key,grade.Value));root.Save(writer);
    }
    static XElement ColorElement(string kind,string key,ColorAdjustment c)=>new(kind,new XAttribute("key",key),new XAttribute("h",c.H.ToString("R",CultureInfo.InvariantCulture)),new XAttribute("s",c.S.ToString("R",CultureInfo.InvariantCulture)),new XAttribute("l",c.L.ToString("R",CultureInfo.InvariantCulture)));
    static double Number(string? text)=>double.Parse(text??"0",CultureInfo.InvariantCulture);
    public static Preset Load(string path)
    {
        if(Path.GetExtension(path).Equals(".xmp",StringComparison.OrdinalIgnoreCase))return XmpPreset.Load(path);
        using var archive=ZipFile.OpenRead(path);var entry=archive.GetEntry("preset.xml")??throw new InvalidDataException("ملف بريسيت غير صالح.");if(entry.Length>2_000_000)throw new InvalidDataException("ملف بريسيت كبير جدًا.");using var stream=entry.Open();using var reader=System.Xml.XmlReader.Create(stream,new System.Xml.XmlReaderSettings{DtdProcessing=System.Xml.DtdProcessing.Prohibit,MaxCharactersInDocument=2_000_000});var root=XElement.Load(reader);
        if((root.Name!="LunoPreset"&&root.Name!="LumoraPreset")||(string?)root.Attribute("version")!="1")throw new InvalidDataException("إصدار بريسيت غير مدعوم.");var p=new Preset{Name=(string?)root.Element("Name")??"بريسيت",Category="فلاتري"};
        foreach(var value in root.Elements("Control"))p.Recipe.Set((string?)value.Attribute("name")??throw new InvalidDataException(),Number(value.Value));
        foreach(var curve in root.Elements("Curve")){string channel=(string?)curve.Attribute("channel")??"rgb";if(!p.Recipe.Curves.ContainsKey(channel))throw new InvalidDataException("قناة غير مدعومة.");p.Recipe.Curves[channel]=curve.Elements("Point").Select(v=>new CurvePoint(Number((string?)v.Attribute("x")),Number((string?)v.Attribute("y")))).ToList();}
        foreach(var c in root.Elements("Hsl")){int index=int.Parse((string?)c.Attribute("key")??"-1",CultureInfo.InvariantCulture);if(index is <0 or >7)throw new InvalidDataException();p.Recipe.Hsl[index]=ReadColor(c);}
        foreach(var c in root.Elements("Grade")){string key=(string?)c.Attribute("key")??"global";if(!p.Recipe.Grading.ContainsKey(key))throw new InvalidDataException();p.Recipe.Grading[key]=ReadColor(c);}p.Recipe.Validate();return p;
    }
    static ColorAdjustment ReadColor(XElement c)=>new(){H=Number((string?)c.Attribute("h")),S=Number((string?)c.Attribute("s")),L=Number((string?)c.Attribute("l"))};
}
