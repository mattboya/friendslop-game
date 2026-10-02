using System;
using System.Collections.Generic;
using System.Text;

namespace Festival.Core
{
    // DIALOG-4: festivalgoer lines built from parts. A grammar is a set of named tables. Line(kind) draws one entry of the kind's
    // table, a whole-line template such as "{greeting} {who} {did} {what} {where}.", and fills each {slot} in it with an entry of
    // that slot's table, which may have slots of its own. Every slot draws once from the stream, left to right and depth first,
    // so the same seed (or the same ContentRandom) gives the same line on every machine.
    // - Festival tags, as DialogueGrammar's pools: an entry tagged "0:" is Palm Mirage only and "1:" is Ember Playa only, and an
    //   untagged one suits both. Every table needs something for every festival.
    // - Number: an entry starting "+" (after any tag) is plural, as in "+my friends". A choice {has|have} takes its first form
    //   after a singular part and its second after a plural one, going by the last part filled; a part's own number outlasts
    //   the slots inside it, so "+my friends from {where}" stays plural. "+" means "takes the plural verb", so "+I" and "+you".
    // - Dressing: a slot written {~name} fills like any other but is left out of the line's body, as are the parts inside it. A
    //   hello or a tail ("Hey!", "Anyway.") is dressing; the body is what the line is about, so the same sentence behind a new
    //   hello is the same body. The repeat memory and the line counts go by the body.
    // - The finished line is tidied: single spaces and none before punctuation, a/an by the sound of the next word, and a
    //   capital at the start of every sentence.
    public sealed class LineGrammar
    {
        private readonly Dictionary<string,string[][]> tables=new Dictionary<string,string[][]>();
        public LineGrammar(Dictionary<string,string[]> tables)
        {
            foreach(var table in tables)
            {
                var byFestival=new string[Festivals.Count][];
                for(int festival=0;festival<byFestival.Length;festival++)
                {
                    var entries=new List<string>(); string tag=festival+":";
                    foreach(var entry in table.Value) if(!(entry.Length>1&&char.IsDigit(entry[0])&&entry[1]==':')) entries.Add(entry); else if(entry.StartsWith(tag,StringComparison.Ordinal)) entries.Add(entry.Substring(2));
                    if(entries.Count==0) throw new ArgumentException("Line grammar table {"+table.Key+"} has nothing for festival "+festival);
                    byFestival[festival]=entries.ToArray();
                }
                this.tables[table.Key]=byFestival;
            }
        }
        public string Line(string kind,int festival,int seed) => Line(kind,festival,new ContentRandom(seed));
        public string Line(string kind,int festival,ContentRandom random) => Line(kind,festival,random,out _);
        public string Line(string kind,int festival,ContentRandom random,out string body)
        {
            if(festival<0||festival>=Festivals.Count) throw new ArgumentException("No festival "+festival);
            bool plural=false; var line=new StringBuilder(); var bare=new StringBuilder(); Fill(kind,festival,random,ref plural,0,line,bare);
            body=Tidy(bare.ToString()); return Tidy(line.ToString());
        }
        // Appends the part to the line, and to the body unless it is dressing (body null).
        private void Fill(string table,int festival,ContentRandom random,ref bool plural,int depth,StringBuilder line,StringBuilder body)
        {
            // ponytail: a depth cap, not a cycle check at build time; 8 is far past any real template's nesting.
            if(depth>8) throw new InvalidOperationException("Line grammar parts nest too deep at {"+table+"}: does a part name its own table?");
            if(!tables.TryGetValue(table,out var byFestival)) throw new ArgumentException("Line grammar has no table {"+table+"}");
            var entries=byFestival[festival]; string entry=entries[random.Next(entries.Length)];
            bool own=entry.StartsWith("+",StringComparison.Ordinal); if(own) entry=entry.Substring(1);
            for(int i=0;i<entry.Length;i++)
            {
                if(entry[i]!='{') { line.Append(entry[i]); body?.Append(entry[i]); continue; }
                int close=entry.IndexOf('}',i); if(close<0) throw new ArgumentException("Line grammar part has an unclosed slot: "+entry);
                string slot=entry.Substring(i+1,close-i-1); int bar=slot.IndexOf('|');
                if(bar<0) { bool dressing=slot.StartsWith("~",StringComparison.Ordinal); Fill(dressing?slot.Substring(1):slot,festival,random,ref plural,depth+1,line,dressing?null:body); }
                else { string form=plural?slot.Substring(bar+1):slot.Substring(0,bar); line.Append(form); body?.Append(form); }
                i=close;
            }
            plural=own;
        }
        private static string Tidy(string line)
        {
            var words=new List<string>();
            foreach(var word in line.Split(new[]{' '},StringSplitOptions.RemoveEmptyEntries))
                if(words.Count>0&&",.!?;:".IndexOf(word[0])>=0) words[words.Count-1]+=word; else words.Add(word);
            for(int i=0;i<words.Count;i++)
            {
                bool opens=i==0||EndsSentence(words[i-1]); int at=0; while(at<words[i].Length&&(words[i][at]=='"'||words[i][at]=='(')) at++;
                string bare=words[i].Substring(at);
                // A capital A inside a sentence is the letter (Plan A), so only a sentence's first word may be a capital article.
                if(i+1<words.Count&&(bare=="a"||bare=="an"||opens&&(bare=="A"||bare=="An"))) bare=(bare[0]=='A'?"A":"a")+(VowelSound(words[i+1].TrimStart('"','('))?"n":"");
                if(opens&&bare.Length>0) bare=char.ToUpperInvariant(bare[0])+bare.Substring(1);
                words[i]=words[i].Substring(0,at)+bare;
            }
            return string.Join(" ",words);
        }
        // An abbreviation's dots (a.m.) and an ellipsis (wait...) don't end a sentence.
        private static bool EndsSentence(string word) { word=word.TrimEnd('"',')'); return word.EndsWith("!",StringComparison.Ordinal)||word.EndsWith("?",StringComparison.Ordinal)||word.EndsWith(".",StringComparison.Ordinal)&&word.IndexOf('.')==word.Length-1; }
        // ponytail: a/an goes by the first letter except for these word starts, matched as written (acronyms) or lower-cased.
        // A word that sounds otherwise and isn't listed takes the wrong article: list it here when the content needs it.
        private static readonly string[] AnBefore={"hour","honest","honor","honour","heir","x-ray","8","LED","MC","MP3","NPC","RV","SOS","SUV"};
        private static readonly string[] ABefore={"one","once","unic","unif","union","uniq","unit","univ","use","usu","uku","eu","ewe","ufo"};
        private static bool VowelSound(string word)
        {
            string lower=word.ToLowerInvariant();
            foreach(var start in AnBefore) if(word.StartsWith(start,StringComparison.Ordinal)||lower.StartsWith(start,StringComparison.Ordinal)) return true;
            foreach(var start in ABefore) if(word.StartsWith(start,StringComparison.Ordinal)||lower.StartsWith(start,StringComparison.Ordinal)) return false;
            return lower.Length>0&&"aeiou".IndexOf(lower[0])>=0;
        }
    }
}
