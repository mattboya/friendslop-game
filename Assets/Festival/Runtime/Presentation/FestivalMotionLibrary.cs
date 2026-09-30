using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Festival.Presentation
{
    /// <summary>
    /// Authored acting clips from the package 03/04 people work, baked once onto the shared
    /// 15-bone festival rig. The rigs match bone for bone, so baked local rotations drive
    /// FestivalCharacter directly; sampling afterwards is plain interpolation per actor.
    /// </summary>
    public static class FestivalMotionLibrary
    {
        public static readonly string[] Bones={"Hips","Spine","Head","ArmL","ArmR","ForearmL","ForearmR","HandL","HandR","LegL","LegR","ShinL","ShinR","FootL","FootR"};
        const float SampleRate=30;
        static readonly string[] Sources={"FestivalMotion","FestivalMotionActing"};
        static Dictionary<string,Clip> clips;

        public sealed class Clip
        {
            public string Name{get;internal set;}
            public float Length{get;internal set;}
            internal Quaternion[][] Rotations;
            internal Vector3[] HipsOffset;
            public void Sample(float time,bool loop,Quaternion[] rotations,out Vector3 hipsOffset)
            {
                int frames=Rotations.Length;
                float frame=(loop?Mathf.Repeat(time,Length):Mathf.Clamp(time,0,Length))*SampleRate;
                int a=Mathf.Clamp(Mathf.FloorToInt(frame),0,frames-1),b=Mathf.Min(frames-1,a+1);
                float u=Mathf.Clamp01(frame-a);
                for(int i=0;i<Bones.Length;i++)rotations[i]=Quaternion.Slerp(Rotations[a][i],Rotations[b][i],u);
                hipsOffset=Vector3.Lerp(HipsOffset[a],HipsOffset[b],u);
            }
        }

        public static int Count{get{Load();return clips.Count;}}
        public static Clip Get(string name){Load();return clips.TryGetValue(name,out var clip)?clip:null;}

        static void Load()
        {
            if(clips!=null)return;
            clips=new Dictionary<string,Clip>();
            var asset=Resources.Load<GameObject>(Sources[0]);
            if(asset==null)return;
            var template=Object.Instantiate(asset);
            template.name="Festival motion bake";template.hideFlags=HideFlags.HideAndDontSave;
            var transforms=new Dictionary<string,Transform>();
            foreach(var t in template.GetComponentsInChildren<Transform>(true))if(!transforms.ContainsKey(t.name))transforms[t.name]=t;
            if(!transforms.TryGetValue("Hips",out var hips)){Object.Destroy(template);return;}
            var rig=hips.parent!=null?hips.parent.gameObject:hips.gameObject;
            var restHips=hips.localPosition;
            // AnimationClip.SampleAnimation leaves non-legacy clips unapplied in players,
            // so bake through a manually evaluated Playables graph that works everywhere.
            var animator=rig.GetComponent<Animator>();
            if(animator==null)animator=rig.AddComponent<Animator>();
            animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
            var graph=PlayableGraph.Create("Festival motion bake");
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            var output=AnimationPlayableOutput.Create(graph,"Bake",animator);
            foreach(var source in Sources)
                foreach(var animation in Resources.LoadAll<AnimationClip>(source))
                {
                    if(animation.name.StartsWith("__preview__",System.StringComparison.Ordinal)||clips.ContainsKey(animation.name))continue;
                    int frames=Mathf.Max(2,Mathf.RoundToInt(animation.length*SampleRate)+1);
                    var playable=AnimationClipPlayable.Create(graph,animation);
                    output.SetSourcePlayable(playable);
                    var clip=new Clip{Name=animation.name,Length=(frames-1)/SampleRate,Rotations=new Quaternion[frames][],HipsOffset=new Vector3[frames]};
                    for(int f=0;f<frames;f++)
                    {
                        playable.SetTime(f/SampleRate);graph.Evaluate();
                        clip.Rotations[f]=new Quaternion[Bones.Length];
                        for(int i=0;i<Bones.Length;i++)clip.Rotations[f][i]=transforms[Bones[i]].localRotation;
                        clip.HipsOffset[f]=hips.localPosition-restHips;
                    }
                    playable.Destroy();
                    clips[clip.Name]=clip;
                }
            graph.Destroy();
            if(Application.isPlaying)Object.Destroy(template);else Object.DestroyImmediate(template);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            int moving=0;
            foreach(var baked in clips.Values)
                for(int f=1;f<baked.Rotations.Length;f++)
                    if(Quaternion.Angle(baked.Rotations[0][3],baked.Rotations[f][3])>5||Quaternion.Angle(baked.Rotations[0][1],baked.Rotations[f][1])>5){moving++;break;}
            Debug.Log("[Festival.Motion] action=clips_baked count="+clips.Count+" moving="+moving);
#endif
        }
    }
}
