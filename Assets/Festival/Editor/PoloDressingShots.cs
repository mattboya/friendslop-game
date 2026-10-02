using System.IO;
using Festival.Core;
using Festival.Presentation;
using UnityEngine;

namespace Festival.Editor
{
    /// <summary>ART-1 review: renders Palm Mirage by day and night, and Ember Playa by day, from fixed spots into
    /// artifacts/polo-dressing/unity, through a camera like the game's (75° FOV, 130 m far clip).</summary>
    // ponytail: east-astronaut and corner eyes moved off the brief's spots, which sat inside the security booth and behind a corner palm (and off the east lane's walkers).
    public static class PoloDressingShots
    {
        static readonly (string Name,Vector3 Eye,Vector3 At)[] Views={
            ("path-to-stage",new Vector3(0,1.6f,-10),new Vector3(0,5,32)),("under-canopy",new Vector3(3,1.6f,22),new Vector3(0,9,30)),
            ("stage-front",new Vector3(0,1.6f,14),new Vector3(0,6,32)),("wheel",new Vector3(8,1.6f,-16),new Vector3(20,6,-28)),
            ("east-astronaut",new Vector3(25,1.6f,-1),new Vector3(54,5,8)),("northwest-tower",new Vector3(-20,1.6f,20),new Vector3(-50,10,48)),
            ("horizon-south",new Vector3(0,1.6f,0),new Vector3(0,10,-110)),("corner",new Vector3(34,1.6f,-33),new Vector3(-40,8,40))};
        public static void Capture()
        {
            var dir=Path.Combine(Directory.GetCurrentDirectory(),"artifacts/polo-dressing/unity");Directory.CreateDirectory(dir);
            var world=new GameObject("Shots").AddComponent<FestivalWorld>();world.Build();world.SetPhase("Playing");
            var cam=new GameObject("Shot camera").AddComponent<Camera>();cam.fieldOfView=75;cam.nearClipPlane=.05f;cam.farClipPlane=130;
            var target=new RenderTexture(1280,720,24);cam.targetTexture=target;FestivalCharacter.ViewTransform=cam.transform;
            foreach(var (festival,level,tag) in new[]{(Festivals.PoloFestival,0,"polo-day"),(Festivals.PoloFestival,1,"polo-night"),(Festivals.PlayaFestival,0,"playa-day")})
            {
                var state=new RoundState{Phase="Playing",FestivalIndex=festival,LevelIndex=level,DurationSeconds=Festivals.Level(festival,level,0).DurationSeconds};
                foreach(var v in Views)
                {
                    cam.transform.position=v.Eye;cam.transform.LookAt(v.At);world.SetTwists(state);world.SetLighting(state,"");
                    cam.Render();RenderTexture.active=target;var shot=new Texture2D(1280,720,TextureFormat.RGB24,false);shot.ReadPixels(new Rect(0,0,1280,720),0,0);shot.Apply();
                    File.WriteAllBytes(Path.Combine(dir,tag+"-"+v.Name+".png"),shot.EncodeToPNG());Object.DestroyImmediate(shot);
                }
            }
            RenderTexture.active=null;FestivalCharacter.ViewTransform=null;Debug.Log("POLO SHOTS WRITTEN: "+dir);
        }
    }
}
