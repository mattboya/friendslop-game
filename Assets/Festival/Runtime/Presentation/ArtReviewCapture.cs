#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Festival.Core;
using Festival.Network;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Festival.Presentation
{
    /// <summary>Explicit opt-in review captures: --art-review &lt;dir&gt; stills of the set dressing,
    /// --motion-review &lt;dir&gt; 30 fps frame sequences of people moving. Absent from release builds.</summary>
    public sealed class ArtReviewCapture : MonoBehaviour
    {
        struct Shot{public string Name,Phase,Site;public Vector3 Eye,At;public float Fov;}
        static Shot S(string name,string phase,Vector3 eye,Vector3 at,float fov=60,string site="")=>new Shot{Name=name,Phase=phase,Site=site,Eye=eye,At=at,Fov=fov};
        static readonly Shot[] Shots={
            S("camp-overview","Shopping",new Vector3(0,10,-21),new Vector3(0,1,3),55),
            S("camp-dj","Shopping",new Vector3(1.4f,1.75f,-1.9f),new Vector3(4.3f,1.1f,-5.6f)),
            S("camp-seller","Shopping",new Vector3(0,1.75f,3.6f),new Vector3(0,1.4f,9.1f),65),
            S("camp-seller-counter","Shopping",new Vector3(-1.9f,2.2f,7.3f),new Vector3(-.1f,1.45f,8.95f),40),
            S("camp-picnic-podium","Shopping",new Vector3(-1.2f,1.9f,-.4f),new Vector3(-5.2f,.8f,-4.2f)),
            S("camp-cars-east","Shopping",new Vector3(16.5f,4.2f,-7.5f),new Vector3(16.5f,1.7f,-15)),
            S("camp-cars-west","Shopping",new Vector3(-16.5f,4.2f,-7.5f),new Vector3(-16.5f,1.7f,-15)),
            S("camp-trailhead","Shopping",new Vector3(0,1.7f,12),new Vector3(0,2.5f,19)),
            S("interior-car","Shopping",new Vector3(0,1.62f,-1.5f),new Vector3(0,1.25f,2.2f),70,"car_1"),
            S("interior-tent","Shopping",new Vector3(0,1.62f,-1.5f),new Vector3(0,1.25f,2.4f),70,"tent_1"),
            S("interior-potty","Shopping",new Vector3(0,1.62f,-1.5f),new Vector3(0,1.25f,2.2f),70,"potty_1"),
            S("festival-market","Playing",new Vector3(-18,1.9f,-26.5f),new Vector3(-18,1.3f,-20.5f),65),
            S("festival-market-stock","Playing",new Vector3(-19.4f,1.55f,-23.3f),new Vector3(-19.8f,1.15f,-21.1f),50),
            S("festival-stage-dj","Playing",new Vector3(0,3,23.5f),new Vector3(0,2,30.3f)),
            S("festival-bunting","Playing",new Vector3(-6,2.2f,-24),new Vector3(-14,5.5f,-14)),
            S("festival-light-pole","Playing",new Vector3(-28.5f,2.5f,-22),new Vector3(-33,5,-28)),
            S("festival-picnic","Playing",new Vector3(-20.5f,2,-1.5f),new Vector3(-24,.7f,2)),
            S("festival-poi","Playing",new Vector3(6,1.5f,-27.2f),new Vector3(6,1.3f,-24),55),
        };
        IEnumerator Start()
        {
            var args=Environment.GetCommandLineArgs();
            int flag=Array.IndexOf(args,"--art-review"),motion=Array.IndexOf(args,"--motion-review");
            if(motion>=0)flag=motion;
            if(flag<0||flag+1>=args.Length)yield break;
            string dir=args[flag+1];Directory.CreateDirectory(dir);
            FestivalWorld world;
            while((world=FindFirstObjectByType<FestivalWorld>())==null||!world.IsReady)yield return null;
            // A scripted shop round fills both shelves with every offer and its price tag.
            var state=new RoundState{RoundId="art-review"};
            state.VendorOffers.AddRange(Catalog.VendorOffers(3));
            foreach(var id in state.VendorOffers)state.ShopStock.Add(new ShopStockState{ItemId=id,CampAvailable=2,MarketAvailable=2});
            world.UpdateShop(state);
            var target=new RenderTexture(1600,900,24);
            var camera=new GameObject("Art review camera").AddComponent<Camera>();
            var view=GetComponent<FestivalSession>().ViewCamera;
            if(view!=null)camera.CopyFrom(view);
            camera.targetTexture=target;camera.depth=100;
            camera.GetUniversalAdditionalCameraData().renderPostProcessing=true;
            // Signs, price tags and character detail are culled by distance from this view.
            FestivalCharacter.ViewTransform=camera.transform;
            if(motion>=0)
            {
                camera.targetTexture=null;camera.targetTexture=target=new RenderTexture(1280,720,24);
                yield return Motion(world,camera,target,dir);
                Debug.Log("FESTIVAL MOTION REVIEW PASSED: "+dir);
                Application.Quit();yield break;
            }
            var performerRoot=new GameObject("Art review poi performer");
            var performer=FestivalCharacter.Create(performerRoot.transform,"art_review_poi",Color.white);
            performer.Pose="Poi";performer.transform.position=new Vector3(6,0,-24);performer.transform.rotation=Quaternion.Euler(0,180,0);
            var pixels=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);
            foreach(var shot in Shots)
            {
                world.SetPhase(shot.Phase);world.SetInterior(shot.Site);
                var offset=Vector3.zero;
                if(shot.Site!="")
                {
                    var site=CampFeatures.Find(shot.Site);
                    offset=new Vector3(CampFeatures.InteriorSlotX(site),0,CampFeatures.InteriorSlotZ(site));
                }
                Vector3 eye=world.transform.TransformPoint(shot.Eye+offset),at=world.transform.TransformPoint(shot.At+offset);
                camera.transform.SetPositionAndRotation(eye,Quaternion.LookRotation(at-eye));camera.fieldOfView=shot.Fov;
                performerRoot.SetActive(shot.Phase=="Playing");
                yield return new WaitForSeconds(.6f);
                yield return new WaitForEndOfFrame();
                RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,target.width,target.height),0,0);pixels.Apply();RenderTexture.active=null;
                File.WriteAllBytes(Path.Combine(dir,shot.Name+".png"),pixels.EncodeToPNG());
            }
            Debug.Log("FESTIVAL ART REVIEW PASSED: "+dir);
            Application.Quit();
        }
        // Deterministic 30 fps sequences on open camp ground: tracking gait views, standing
        // life, the acting states, dance styles and a chain of pose transitions.
        IEnumerator Motion(FestivalWorld world,Camera camera,RenderTexture target,string dir)
        {
            world.SetPhase("Shopping");world.SetInterior("");
            Time.captureFramerate=30;
            var pixels=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);
            var cast=new GameObject("Motion review cast").transform;
            FestivalCharacter Actor(string name,Vector3 at,float yaw,string pose)
            {
                var actor=FestivalCharacter.Create(cast,name,Color.white);
                actor.transform.SetPositionAndRotation(at,Quaternion.Euler(0,yaw,0));
                actor.transform.localScale=Vector3.Scale(Vector3.one*.82f,actor.ShapeScale);actor.Pose=pose;return actor;
            }
            void Look(Vector3 eye,Vector3 at,float fov){camera.transform.SetPositionAndRotation(eye,Quaternion.LookRotation(at-eye));camera.fieldOfView=fov;}
            IEnumerator Record(string name,int frames,Action<int> step)
            {
                string folder=Path.Combine(dir,name);Directory.CreateDirectory(folder);
                for(int f=0;f<frames;f++)
                {
                    step(f);
                    yield return new WaitForEndOfFrame();
                    RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,target.width,target.height),0,0);pixels.Apply();RenderTexture.active=null;
                    File.WriteAllBytes(Path.Combine(folder,f.ToString("D4")+".jpg"),pixels.EncodeToJPG(90));
                }
            }
            foreach(var (name,speed) in new[]{("walk",1.4f),("jog",4f),("run",6f)})
            {
                var runner=Actor("Motion gait "+name,new Vector3(-10,0,-10.5f),90,"Idle");float x=-10;
                yield return Record("gait-"+name,105,f=>
                {
                    x+=speed/30f;runner.transform.position=new Vector3(x,0,-10.5f);
                    Look(runner.transform.position+new Vector3(0,1f,-3.8f),runner.transform.position+new Vector3(0,.8f,0),45);
                });
                Destroy(runner.gameObject);
            }
            var group=new List<FestivalCharacter>();
            for(int i=0;i<4;i++)group.Add(Actor("Motion idle "+i,new Vector3(-3.6f+i*2.4f,0,-10),180,"Idle"));
            yield return Record("idle",150,f=>Look(new Vector3(0,1.3f,-15.5f),new Vector3(0,1f,-10),42));
            foreach(var actor in group)Destroy(actor.gameObject);group.Clear();
            var poses=new[]{"Detained","Questioning","Accusing","Swarming","Downed","Extract","Drag"};
            for(int i=0;i<poses.Length;i++)group.Add(Actor("Motion acting "+poses[i],new Vector3(-6.6f+i*2.2f,0,-10),180,poses[i]));
            yield return Record("acting",120,f=>Look(new Vector3(0,1.7f,-20f),new Vector3(0,.9f,-10),42));
            foreach(var actor in group)Destroy(actor.gameObject);group.Clear();
            var styles=new HashSet<int>();
            for(int i=0;styles.Count<3&&i<80;i++)
            {
                var dancer=Actor("Motion dancer "+i,new Vector3(-3.9f+styles.Count*2.6f,0,-10),180,"Dance");
                if(!styles.Add(dancer.DanceStyle)){Destroy(dancer.gameObject);continue;}
                group.Add(dancer);
            }
            group.Add(Actor("Motion poi",new Vector3(3.9f,0,-10),180,"Poi"));
            yield return Record("dance",150,f=>Look(new Vector3(0,1.4f,-16f),new Vector3(0,1f,-10),42));
            foreach(var actor in group)actor.SetEquippedItem("poi_led");
            yield return Record("poi-dance",150,f=>Look(new Vector3(0,1.4f,-16f),new Vector3(0,1f,-10),42));
            foreach(var actor in group)Destroy(actor.gameObject);group.Clear();
            var mover=Actor("Motion transitions",new Vector3(0,0,-10),180,"Idle");
            yield return Record("transitions",190,f=>
            {
                mover.Pose=f<36?"Idle":f<72?"Accusing":f<108?"Idle":f<150?"Downed":"Idle";
                Look(new Vector3(-2.6f,1.3f,-14f),new Vector3(0,.8f,-10),45);
            });
            var carrier=Actor("Motion poi carrier",new Vector3(-8,0,-10.5f),90,"Idle");carrier.SetEquippedItem("poi_led");float cx=-8;
            yield return Record("poi-carry",180,f=>
            {
                if(f<90)cx+=1.4f/30f;carrier.transform.position=new Vector3(cx,0,-10.5f);
                Look(carrier.transform.position+new Vector3(-1.4f,1.1f,-3.4f),carrier.transform.position+new Vector3(0,.8f,0),45);
            });
            Destroy(carrier.gameObject);
            var hands=FestivalHands.Create(camera,"motion_first_person");
            if(hands!=null)
            {
                hands.SetState(new PlayerState{Id="motion_first_person",EquippedItemId="poi_practice"});
                float fz=-13;
                yield return Record("poi-first-person",180,f=>{if(f>=30&&f<120)fz+=1.4f/30f;Look(new Vector3(-2,1.55f,fz),new Vector3(-2,1.35f,fz+4),60);});
                Destroy(hands.gameObject);
            }
            Destroy(cast.gameObject);Time.captureFramerate=0;
        }
    }
}
#endif
