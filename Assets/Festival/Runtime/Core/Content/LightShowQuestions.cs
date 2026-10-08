using System;
using System.Collections.Generic;
using System.Globalization;

namespace Festival.Core
{
    /// <summary>Authored Light Show vocabulary and questions. Only the persisted real script can determine an answer.</summary>
    public static class LightShowQuestions
    {
        public const string Passers="Passers",Sweep="Sweep",Sign="Sign",Prop="Prop",FinalLights="FinalLights",Unsure="Not sure";
        public static readonly IReadOnlyList<string> Colours=new[]{"Red","Orange","Yellow","Green","Blue","Purple"};
        public static readonly IReadOnlyList<string> Shapes=new[]{"Circle","Triangle","Square","Star","Heart","Diamond"};
        public static readonly IReadOnlyList<string> Props=new[]{"Umbrella","Flamingo","Lantern","Fan","Balloon","Backpack"};
        static readonly string[] Categories={Passers,Sweep,Sign,Prop,FinalLights};
        static readonly string[] Prompts={"How many real people walked past?","Which colour swept over the show first?","Which shape flashed on the sign first?","What was the one real passer carrying?","What colour were the glover's lights at the end?"};
        static readonly IReadOnlyList<string> Counts=new[]{"1","2","3","4","5","6","7","8"};
        public static List<LightShowQuestion> Draw(IReadOnlyList<LightShowEvent> events,int seed)
        {
            var random=new ContentRandom(seed);random.Next(100);var categories=new List<int>{0,1,2,3,4};Shuffle(categories,random);
            var questions=new List<LightShowQuestion>();for(int slot=0;slot<3;slot++)
            {
                int category=categories[slot];string answer=Answer(events,Categories[category]);
                var pool=category==0?Counts:category==1||category==4?Colours:category==2?Shapes:Props;
                var wrong=new List<string>();foreach(string value in pool)if(value!=answer)wrong.Add(value);Shuffle(wrong,random);
                var choices=new List<string>{answer,wrong[0],wrong[1],wrong[2]};Shuffle(choices,random);
                questions.Add(new LightShowQuestion{Category=Categories[category],Prompt=Prompts[category],Choices=choices,CorrectIndex=choices.IndexOf(answer)});
            }
            return questions;
        }
        public static string Answer(IReadOnlyList<LightShowEvent> events,string category)
        {
            if(category==Passers){int count=0;foreach(var e in events)if(e.Kind==LightShowEventKind.Passer)count++;return count.ToString(CultureInfo.InvariantCulture);}
            if(category==Prop){foreach(var e in events)if(e.Kind==LightShowEventKind.Passer&&e.Prop!="")return e.Prop;throw new ArgumentException("Real script has no carried prop.",nameof(events));}
            LightShowEventKind kind;if(category==Sweep)kind=LightShowEventKind.Sweep;else if(category==Sign)kind=LightShowEventKind.Sign;else if(category==FinalLights)kind=LightShowEventKind.Glover;else throw new ArgumentException("Unknown light-show category.",nameof(category));
            LightShowEvent selected=null;foreach(var e in events)if(e.Kind==kind&&(selected==null||(category==FinalLights?e.StartsAt>selected.StartsAt:e.StartsAt<selected.StartsAt)))selected=e;
            if(selected==null)throw new ArgumentException("Real script has no event for this question.",nameof(events));return category==Sign?selected.Shape:selected.Colour;
        }
        static void Shuffle<T>(List<T> values,ContentRandom random){for(int k=values.Count-1;k>0;k--){int pick=random.Next(k+1);var value=values[k];values[k]=values[pick];values[pick]=value;}}
    }
}
