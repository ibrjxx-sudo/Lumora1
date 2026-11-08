namespace Tayf.Core;

public static class FilterLibrary
{
    public static readonly string[] Categories=["سينمائي","بورتريه","أفلام كلاسيكية","أعراس","شارع ومدن","سفر","مناظر طبيعية","ليل ونيون","أبيض وأسود","موضة وتحريري","ذهبي ودافئ","بارد وهادئ","منتجات","طعام","بحر وساحل","غابات","صحراء","باستيل","درامي","نظيف وطبيعي"];
    static readonly Lazy<IReadOnlyList<Preset>> all=new(Create);
    public static IReadOnlyList<Preset> All=>all.Value;
    static readonly string[][] Titles=
    [ ["Obsidian","Blue Hour","Amber Frame","Silver Story","Velvet Scene","Tungsten","Daybreak","Nightfall","Prism","Auteur"],
      ["Skin Silk","Windowlight","Rose Muse","Soft Ivory","Honey Face","Porcelain","Bronze Study","Quiet Skin","Studio Glow","Natural Muse"],
      ["Analog Diary","Celluloid","Faded Print","Silver Halide","Archive","Dusk Roll","Contact Sheet","Warm Negative","Grainroom","Memory Lane"],
      ["Vow","Veil","Champagne","Garden Kiss","White Linen","Blush Letter","Golden Promise","Ceremony","Pearl Day","Forever"],
      ["Concrete","Metro","After Rain","Sidewalk","Steel City","Crosswalk","Neon Alley","Architecture","Quiet Block","Day Shift"],
      ["Postcard","Wander","Terrace","Faraway","Atlas","Open Road","Summer Trip","Old Town","Morning Train","Vista"],
      ["Alpine","Summit","Riverstone","Horizon","Highland","Wild Earth","Clear Lake","Mountain Air","Field Notes","Valley"],
      ["Ultraviolet","Neon Rain","Midnight Taxi","Electric Rose","Cobalt","Night Arcade","Magenta Hour","Vaporlight","City Lights","Indigo"],
      ["Silver Noir","Graphite","Ink Study","Platinum","Carbon","Panchrome","Gallery Mono","Paperwhite","Charcoal","Sculpture"],
      ["Runway","Atelier","Cover Story","Gloss","Couture","Softbox","Magazine","Avant","Studio Edit","Flash"],
      ["Saffron","Apricot Sun","Honeygold","Harvest","Amberlight","Cinnamon","Gilded","Summer Silk","Solstice","Warm Sand"],
      ["Glacier","Blue Mist","Arctic","Quiet Steel","Polar","Moonstone","Frostline","Cool Linen","Northlight","Icewater"],
      ["Catalog","Object Study","Clean Shelf","Minimal","Sculpted","Ceramic","Glasswork","Fine Detail","Brandroom","Showcase"],
      ["Bistro","Golden Crust","Fresh Table","Olive Oil","Berry Bowl","Spice","Sunday Brunch","Citrus","Caramel","Market"],
      ["Salt Air","Lagoon","Azure","Sea Glass","Tidal","Marina","Coastal Dawn","Bluewater","Sailcloth","Shoreline"],
      ["Canopy","Fern","Mosslight","Evergreen","Pinewalk","Woodland","Botanical","Rainforest","Wild Leaf","Cedar"],
      ["Dune","Terracotta","Oasis","Sandstorm","Copper Earth","Nomad","Sahara Glow","Clay","Dry Canyon","Desert Moon"],
      ["Powder","Lilac Air","Sorbet","Cotton","Petal","Watercolour","Soft Haze","Lavender","Apricot Milk","Dreamlight"],
      ["Storm","Thunder","Deep Focus","Epic","Hard Light","Midnight Contrast","Shadowplay","Darkroom","Intensity","Monument"],
      ["True Light","Clearview","Everyday","Balanced","Fresh Air","Pure Tone","Daylight","Honest Colour","Soft Neutral","Essential"] ];
    static readonly string[] Finish=["Air","Rich","Silk","Matte","Chrome","Pearl","Dusk","Bloom","Crisp","Depth"];
    static readonly double[] Temperature=[-9,6,13,8,-8,4,-5,-16,0,-3,24,-23,1,11,-10,-7,23,8,-10,0];
    static readonly double[] Contrast=[17,-6,-10,-6,24,12,18,22,25,19,8,-4,10,15,12,16,20,-20,34,2];
    static readonly double[] Saturation=[-13,-5,-20,-9,-17,8,10,12,-100,-9,2,-10,2,12,9,8,-2,-23,-23,0];
    static double H(int index,int prime){double result=0,f=1d/prime;for(int i=index;i>0;i/=prime){result+=f*(i%prime);f/=prime;}return result;}
    static Recipe Design(int family,int look,int finish,int seed)
    {
        double a=H(seed,2)*2-1,b=H(seed,3)*2-1,c=H(seed,5)*2-1,d=H(seed,7)*2-1,e=H(seed,11)*2-1;
        bool gentle=family is 1 or 3 or 12 or 19,mono=family==8;var p=new Recipe();
        double[] toe=[0,.012,.025,.08,0,.035,.015,.06,0,.008],shoulder=[1,.98,.97,.96,.995,1,.965,.98,1,.97];
        double[] styleTemp=[-14,16,7,-7,0,19,-20,9,-3,4],styleTint=[-3,2,6,9,-8,0,10,-5,3,0];
        double[] styleContrast=[-12,22,-4,-20,12,-8,24,-15,30,8],styleSat=[-10,6,-8,-18,-25,2,-16,-20,5,-4];
        p.Set("temperature",Math.Clamp(Temperature[family]+styleTemp[look]*(gentle?.45:1)+a*(gentle?8:16),-65,65));
        p.Set("tint",styleTint[look]+b*(gentle?5:12));p.Set("exposure",a*.22+(finish is 0 or 5 or 7?.16:finish is 6 or 9?-.2:0));
        p.Set("contrast",Math.Clamp(Contrast[family]+styleContrast[look]*(gentle?.5:1)+c*12,-50,65));
        p.Set("saturation",mono?-100:Math.Clamp(Saturation[family]+styleSat[look]*(gentle?.5:1)+d*14,-60,40));p.Set("vibrance",mono?0:6+e*16);
        p.Set("highlights",-18-b*22+(finish==8?14:0));p.Set("shadows",a*25+(finish is 0 or 7?18:finish==9?-18:5));p.Set("whites",c*15);p.Set("blacks",d*18+(finish==3?16:finish==9?-14:0));
        p.Set("texture",family is 12 or 13?8:family==1?-4:finish==8?7:0);p.Set("clarity",gentle?-3:family==18?14:finish==9?9:finish==2?-5:2);p.Set("dehaze",family is 6 or 15 or 16?4+c*4:0);
        p.Set("grain",family is 0 or 2 or 8?(finish==2?4:8+look*1.1):0);p.Set("vignette",family is 0 or 7 or 18?-8-finish*1.2:0);
        double fade=toe[finish]*(gentle?.55:1),black=fade,white=shoulder[finish],mid=.5+b*.035+(finish is 0 or 5?.04:finish is 6 or 9?-.035:0);
        p.Curves["rgb"]=[new(0,black),new(.18,Math.Clamp(.15+fade+a*.035,black+.015,.30)),new(.50,mid),new(.82,Math.Clamp(.85+c*.035,.73,white-.01)),new(1,white)];
        if(!mono)
        {
            double[] hues=[205,32,265,175,225,47,285,90,195,20];
            double split=gentle?3.5:family==7?17:family is 0 or 9?11:6;
            p.Grading["shadows"]=new(){H=(hues[look]+b*22+360)%360,S=Math.Max(0,split+d*split*.55),L=0};
            p.Grading["highlights"]=new(){H=(look%2==0?38:200)+c*18,S=gentle?2.5:4+finish*.8,L=0};
            p.Grading["midtones"]=new(){H=(look*37+family*13)%360,S=gentle?0:finish is 4 or 6?3.5:0,L=0};
            for(int channel=0;channel<8;channel++)
            {
                double h=(H(seed+channel*29,13)*2-1)*14,s=(H(seed+channel*11,17)*2-1)*18,l=(H(seed+channel*43,19)*2-1)*10;
                if(channel==1){h*=.35;s*=.45;l=family is 1 or 3?4+e*3:l*.4;}
                if(channel==3){h+=family==15?-17:family==0?12:0;s+=family==15?6:family is 0 or 4?-17:0;}
                if(channel==5){h+=family==14?-12:0;s+=family==14?10:0;}
                p.Hsl[channel]=new(){H=h,S=s,L=l};
            }
            double red=(look is 1 or 5?.018:look is 0 or 6?-.014:0)+e*.009,blue=-red*.8;
            p.Curves["r"]=[new(0,Math.Max(0,red)),new(.5,.5+red),new(1,Math.Min(1,1+red))];p.Curves["b"]=[new(0,Math.Max(0,blue)),new(.5,.5+blue),new(1,Math.Min(1,1+blue))];
        }
        else for(int i=0;i<8;i++)p.Hsl[i]=new(){L=(H(seed+i*31,13)*2-1)*35,S=(H(seed+i*17,11)*2-1)*35};
        p.Validate();return p;
    }
    public static ImageFrame SimilarityChart()
    {
        const int w=12,h=8;byte[] bytes=new byte[w*h*4];for(int y=0;y<h;y++)for(int x=0;x<w;x++){int i=(y*w+x)*4;var(r,g,b)=ImageEngine.HslToRgb(x*30,.25+y*.09,.12+y*.10);bytes[i]=(byte)(r*255);bytes[i+1]=(byte)(g*255);bytes[i+2]=(byte)(b*255);bytes[i+3]=255;}return new(w,h,bytes);
    }
    public static byte[] VisualSignature(Recipe recipe)
    {
        var copy=recipe.Clone();foreach(string key in new[]{"grain","vignette","texture","clarity","dehaze"})copy.Values.Remove(key);var output=ImageEngine.Render(SimilarityChart(),copy);return output.Pixels.Where((_,i)=>i%4!=3).ToArray();
    }
    static IReadOnlyList<Preset> Create()
    {
        var result=new List<Preset>(2000);
        for(int family=0;family<20;family++)
        {
            var signatures=new List<byte[]>();for(int index=0;index<100;index++)
            {
                int look=index%10,finish=index/10,seed=family*1009+index*37+1;Recipe p=null!;byte[] signature=[];
                for(int attempt=0;attempt<80;attempt++)
                {
                    p=Design(family,look,finish,seed+attempt*997);signature=VisualSignature(p);bool similar=signatures.Any(other=>other.Zip(signature,(x,y)=>Math.Abs(x-y)).Average()<2.4);if(!similar)break;if(attempt==79)throw new InvalidOperationException("تعذر تكوين فلاتر متمايزة.");
                }
                signatures.Add(signature);result.Add(new(){Id=$"lumora-{family:00}-{index:00}",Category=Categories[family],Name=Titles[family][look]+" / "+Finish[finish],Description=look switch{0=>"ظلال باردة وإضاءة هادئة",1=>"دفء غني وتباين تحريري",2=>"درجات ناعمة وملمس فيلمي",3=>"سواد مرفوع وألوان مطفية",4=>"لون محايد بلمسة معدنية",5=>"إشراق ذهبي ولمعان ناعم",6=>"ظلال عميقة ولون بارد",7=>"باستيل ناعم وتدرج عضوي",8=>"فصل واضح وألوان نظيفة",_=>"عمق وتباين متوازن"},Recipe=p});
            }
        }
        return result;
    }
}
