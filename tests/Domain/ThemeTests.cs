using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using Festival.Core;

public static class ThemeTests
{
    private static void Check(bool condition,string message) { if(!condition) throw new Exception("Theme: "+message); }
    // THEME-1's real-drug words, matched in any case and inside other words too, so "Shrooms" or "drugstore" also fail.
    private static readonly string[] Banned={"lsd","acid","mushroom","shroom","ecstasy","molly","mdma","ketamine","weed","marijuana","cannabis","cocaine","drug"};
    // THEME-2: internal effect id and the name a player reads (the ids never change, so snapshots and models keep working).
    private static readonly string[][] EffectNames={new[]{"lsd","Tongue Stamps"},new[]{"mushrooms","Fun Guys"},new[]{"ecstasy","Rolly Pollies"},new[]{"ketamine","Pony Dust"},new[]{"alcohol","Shot"},new[]{"weed",WeedName}};
    // THEME-1's stand-ins that THEME-2 retired, matched like the real-drug words. "Couch Lock" moved from ketamine to weed, so
    // only the weed effect's own name may read it. Never bare "moon": the MOON totem, its map pin and MOONLIT DISCO stay.
    private static readonly string[] Retired={"prism","moon cap","hug drop","snack leaf","couch lock"};
    private const string WeedName="Couch Lock";
    // Every source of HUD text: FestivalHud, any FestivalHud* helper it moves strings into, and the Core guidance lines it shows. Paths are from the repo root, the runner's working directory.
    private static List<string> HudSources()
    {
        var files=new List<string>(Directory.GetFiles("Assets/Festival/Runtime/Presentation","FestivalHud*.cs"));
        files.Add("Assets/Festival/Runtime/Core/Simulation/FestivalGuidance.cs");
        return files;
    }
    public static void Run()
    {
        foreach(var pair in EffectNames) Check(Catalog.FindEffect(pair[0])?.Name==pair[1],"effect "+pair[0]+" reads as "+pair[1]);
        // The stock a player buys, sells and drops, and its short name on the shelf tags.
        foreach(var item in new[]{new[]{"stock_lsd","Tongue Stamps","TONGUE STAMPS"},new[]{"stock_mushrooms","Fun Guys","FUN GUYS"}})
            Check(Catalog.FindItem(item[0])?.Name==item[1]&&Catalog.ShopTag(item[0])==item[2],"item "+item[0]+" reads as "+item[1]+" and tags as "+item[2]+", got "+Catalog.FindItem(item[0])?.Name+" / "+Catalog.ShopTag(item[0]));
        Check(Catalog.EffectsLine(new List<ActiveEffect>{new ActiveEffect{Id="lsd",RemainingSeconds=42.4},new ActiveEffect{Id="mushrooms",RemainingSeconds=9.6}})=="Tongue Stamps 42s, Fun Guys 10s","the HUD's effects line uses display names, got "+Catalog.EffectsLine(new List<ActiveEffect>{new ActiveEffect{Id="lsd",RemainingSeconds=42.4},new ActiveEffect{Id="mushrooms",RemainingSeconds=9.6}}));
        Check(Catalog.EffectsLine(new List<ActiveEffect>())=="clear"&&Catalog.EffectsLine(null)=="clear","no effects reads as clear");
        Check(Catalog.EffectsLine(new List<ActiveEffect>{new ActiveEffect{Id="dose",RemainingSeconds=5}})=="dose 5s","an effect with no catalog entry falls back to its id");

        var texts=new List<string>();
        foreach(var item in Catalog.Items) { texts.Add(item.Name); texts.Add(item.Description); texts.Add(Catalog.ShopTag(item.Id)); }
        foreach(var effect in Catalog.Effects) { texts.Add(effect.Name); texts.Add(effect.PresentationContract); }
        foreach(var line in DialogueCatalog.Lines) { texts.Add(line.Text); texts.Add(line.AwkwardText); }
        int before=texts.Count;
        foreach(var field in typeof(DialogueGrammar).GetFields(BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic)) Collect(field.GetValue(null),texts);
        Check(texts.Count-before>=400,"every DialogueGrammar pool is read, got "+(texts.Count-before)+" strings");
        foreach(var text in texts) Check(Offence(text)==null,"real-drug or retired word '"+Offence(text)+"' in: "+text);

        // Internal ids may stay as they are, so a literal that is exactly a catalog id (e.Id=="lsd") is code, not text.
        // Ceiling: a label that is exactly an id, or an id joined into a label at runtime, passes this scan. The PlayMode
        // ThemeHudTests reads the text the HUD actually renders for that.
        var ids=new HashSet<string>(); foreach(var item in Catalog.Items) ids.Add(item.Id); foreach(var effect in Catalog.Effects) ids.Add(effect.Id);
        var sources=HudSources();
        Check(sources.Contains("Assets/Festival/Runtime/Presentation/FestivalHud.cs"),"FestivalHud.cs is scanned");
        int literals=0;
        foreach(var path in sources) foreach(var literal in Literals(File.ReadAllText(path)))
        {
            literals++;
            if(!ids.Contains(literal)) Check(Offence(literal)==null,"real-drug or retired word '"+Offence(literal)+"' in "+path+": \""+literal+"\"");
        }
        Check(literals>=300,"HUD string literals are read, got "+literals);
        // The scanner itself: it finds a real word in text, and skips comments, char literals and escapes.
        var sample=Literals("a=\"Weed 5s\"; // \"lsd\"\n/* \"acid\" */ c='\"'; d=@\"say \"\"hi\"\"\"; e=\"x\\\"y\";");
        Check(string.Join("|",sample)=="Weed 5s|say \"hi\"|x y"&&Offence(sample[0])=="weed","the literal scanner reads strings only, got "+string.Join("|",sample));
        // Retired names fail in any case and inside a line; only the weed effect's own name may read "Couch Lock".
        Check(Offence("PRISM 42S")=="prism"&&Offence("one Moon cap short")=="moon cap"&&Offence("a Hug Drop")=="hug drop"&&Offence("Snack Leaf 9s")=="snack leaf","the scanner finds retired names");
        Check(Offence(WeedName)==null&&Offence("Got Couch Lock?")=="couch lock"&&Offence("MOONLIT DISCO")==null,"only the weed effect is Couch Lock, and bare moon stays");
    }
    private static string Offence(string text)
    {
        string lower=(text??"").ToLowerInvariant();
        foreach(var word in Banned) if(lower.Contains(word)) return word;
        if(text==WeedName) return null;
        foreach(var word in Retired) if(lower.Contains(word)) return word;
        return null;
    }
    private static void Collect(object value,List<string> into)
    {
        if(value is string text) into.Add(text);
        else if(value is IEnumerable list) foreach(var x in list) Collect(x,into);
    }
    // String literals in C# 9 source (no raw strings). An escape reads as a space. Ceiling: a nested string inside an
    // interpolation hole ($"{("a")}") ends the literal early; the HUD has none.
    private static List<string> Literals(string source)
    {
        var found=new List<string>();
        for(int i=0;i<source.Length;i++)
        {
            char c=source[i]; char next=i+1<source.Length?source[i+1]:'\0';
            if(c=='/'&&next=='/') { i=source.IndexOf('\n',i); if(i<0) break; }
            else if(c=='/'&&next=='*') { i=source.IndexOf("*/",i+2,StringComparison.Ordinal)+1; if(i<=0) break; }
            else if(c=='\'') { i++; while(i<source.Length&&source[i]!='\'') i+=source[i]=='\\'?2:1; }
            else if(c=='"')
            {
                bool verbatim=i>0&&source[i-1]=='@'||i>1&&source[i-2]=='@'&&source[i-1]=='$';
                var text=new StringBuilder();
                for(i++;i<source.Length;i++)
                {
                    if(verbatim&&source[i]=='"'&&i+1<source.Length&&source[i+1]=='"') { text.Append('"'); i++; }
                    else if(source[i]=='"') break;
                    else if(!verbatim&&source[i]=='\\') { text.Append(' '); i++; }
                    else text.Append(source[i]);
                }
                found.Add(text.ToString());
            }
        }
        return found;
    }
}
