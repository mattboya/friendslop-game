using System.Collections.Generic;
using System.Reflection;
using Festival.Core;
using Festival.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Festival.Tests
{
    public sealed class HudMapTests
    {
        private readonly List<GameObject> made=new List<GameObject>();
        [TearDown]public void Cleanup()
        {
            foreach(var go in made)if(go!=null)Object.DestroyImmediate(go);
            if(made.Count>0)UnityEditor.EditorUtility.UnloadUnusedAssetsImmediate();
            made.Clear();
        }
        private FestivalHud Hud(bool fullHd)
        {
            var go=new GameObject("HUD map under test");made.Add(go);var hud=go.AddComponent<FestivalHud>();
            typeof(FestivalHud).GetMethod("Awake",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(hud,null);
            // The batch screen is 640x480, a 4:3 canvas; a world-space 1920x1080 canvas lays the HUD out as a 16:9 screen does.
            if(fullHd){var canvas=go.GetComponentInChildren<Canvas>();Object.DestroyImmediate(canvas.GetComponent<CanvasScaler>());canvas.renderMode=RenderMode.WorldSpace;((RectTransform)canvas.transform).sizeDelta=new Vector2(1920,1080);}
            Canvas.ForceUpdateCanvases();return hud;
        }
        private static readonly bool[] Screens={false,true};
        private static Transform Find(Transform root,string name){if(root.name==name)return root;foreach(Transform child in root){var found=Find(child,name);if(found!=null)return found;}return null;}
        private static Rect ScreenRect(RectTransform rect){var corners=new Vector3[4];rect.GetWorldCorners(corners);return Rect.MinMaxRect(corners[0].x,corners[0].y,corners[2].x,corners[2].y);}

        [Test]public void TheMapPinsBothStagesWithLabelsClearOfEveryOtherPin([ValueSource(nameof(Screens))]bool fullHd)
        {
            var map=Find(Hud(fullHd).transform,"Festival landmarks");Assert.That(map,Is.Not.Null,"setup: the festival map");
            foreach(var name in new[]{"MAIN STAGE","SMALL STAGE"})
            {
                var label=map.Find(name);Assert.That(label,Is.Not.Null,"the map pins the "+name);Assert.That(map.Find(name+" dot"),Is.Not.Null,"with a dot");
                Assert.That(label.GetComponent<Text>().text,Is.EqualTo(name));
            }
            Assert.That(map.Find("STAGE"),Is.Null,"the old lone STAGE pin is now the MAIN STAGE");
            var small=map.Find("SMALL STAGE").GetComponent<Text>();float scale=ScreenRect(small.rectTransform).width/small.rectTransform.rect.width;
            var camp=map.Find("BACK TO CAMP").GetComponent<Text>();
            Assert.That(small.preferredWidth,Is.LessThanOrEqualTo(camp.preferredWidth),"SMALL STAGE is no longer than BACK TO CAMP, which shares its label box");
            // A label's box is wider than its words, so a neighbour counts only where the drawn words reach.
            Rect Words(Text t){var rect=ScreenRect(t.rectTransform);float width=Mathf.Min(rect.width,t.preferredWidth*scale);return t.alignment==TextAnchor.MiddleRight?Rect.MinMaxRect(rect.xMax-width,rect.yMin,rect.xMax,rect.yMax):Rect.MinMaxRect(rect.xMin,rect.yMin,rect.xMin+width,rect.yMax);}
            var words=Words(small);
            Assert.That(words.xMax,Is.LessThanOrEqualTo(ScreenRect((RectTransform)map.Find("SMALL STAGE dot")).xMin),"SMALL STAGE reads to the left of its dot, not stacked over BACK TO CAMP's name");
            foreach(Transform other in map)
            {
                if(other.name.StartsWith("SMALL STAGE",System.StringComparison.Ordinal)||other.GetComponent<Graphic>()==null||other.name=="Footpath")continue;
                var text=other.GetComponent<Text>();var rect=text!=null?Words(text):ScreenRect((RectTransform)other);
                Assert.That(words.Overlaps(rect),Is.False,"SMALL STAGE's words "+words+" overlap "+other.name+" "+rect);
            }
        }

        [Test]public void TheMapTextNamesEachStagesSongAndFitsWithAFullCrew([ValueSource(nameof(Screens))]bool fullHd)
        {
            var hud=Hud(fullHd);var text=Find(hud.transform,"Map text").GetComponent<Text>();Assert.That(text,Is.Not.Null,"setup: the map's side text");
            var s=new RoundState{Phase="Playing",FestivalIndex=1,LevelIndex=1,SpinSeed=0,DurationSeconds=600,ElapsedSeconds=570};
            for(int i=0;i<8;i++)s.Players.Add(new PlayerState{Id="p"+i,Name=new string((char)('A'+i),24),Life="Alive"});
            // The two longest song names the stages can play.
            for(int seed=0;seed<5000;seed++){s.SpinSeed=seed;if(StageMusic.Track(s,StageMusic.Main)==7&&StageMusic.Track(s,StageMusic.Small)==6)break;}
            Assert.That(StageMusic.Track(s,StageMusic.Main),Is.EqualTo(7),"setup: INDUSTRIAL TECHNO on the main stage");
            float height=text.rectTransform.rect.height;
            foreach(var (level,sales,what) in new[]{(1,0,"a night"),(2,0,"a day's last minute"),(2,100000,"a day with the quota met")})
            {
                s.LevelIndex=level;s.LevelSales=sales;
                string shown=FestivalHudText.MapText(s,s.Players[0]);
                StringAssert.Contains("MAIN STAGE  /  INDUSTRIAL TECHNO\nSMALL STAGE  /  DESERT RHYTHM",shown,"on "+what+", the map names both stages' songs");
                text.text=shown;var drawn=new TextGenerator();drawn.Populate(shown,text.GetGenerationSettings(text.rectTransform.rect.size));
                TestContext.WriteLine(what+": map text preferred "+text.preferredHeight+" of "+height+", drawn at "+drawn.fontSizeUsedForBestFit/text.pixelsPerUnit);
                Assert.That(drawn.characterCountVisible,Is.EqualTo(shown.Length),"on "+what+", a full crew's map text, songs and all, shows every line in its box");
            }
        }
    }
}
