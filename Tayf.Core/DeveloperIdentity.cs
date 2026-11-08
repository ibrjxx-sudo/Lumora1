using System.Security.Cryptography;
using System.Text;
namespace Tayf.Core;
public static class DeveloperIdentity
{
    public const string Name="إبراهيم أبو حمد";
    public const string InstagramHandle="@ibrpro";
    public const string InstagramUrl="https://www.instagram.com/ibrpro/";
    public const string Copyright="© 2026 Lumora · إبراهيم أبو حمد. جميع الحقوق محفوظة.";
    public const string WelcomeTitle="كل صورة، بداية جديدة";
    public const string WelcomeDetail="أهلًا بك في Lumora. مساحة لإبداعك، طوّرها إبراهيم أبو حمد بعناية. تابع الحساب لتتعرف على الجديد وتشاركنا أفكارك.";
}
public static class FirstLaunchNotice
{
    static string Token(string installationId)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("developer-welcome-v1|"+installationId)));
    public static bool ShouldShow(string marker,string installationId)
    {try{return !File.Exists(marker)||File.ReadAllText(marker)!=Token(installationId);}catch(IOException){return true;}catch(UnauthorizedAccessException){return true;}}
    public static void Complete(string marker,string installationId)
    {Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(marker))!);string temp=marker+"."+Guid.NewGuid().ToString("N")+".tmp";try{File.WriteAllText(temp,Token(installationId));File.Move(temp,marker,true);}finally{if(File.Exists(temp))File.Delete(temp);}}
}
