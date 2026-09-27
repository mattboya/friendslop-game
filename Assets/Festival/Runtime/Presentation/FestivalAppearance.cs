using UnityEngine;

namespace Festival.Presentation
{
    /// <summary>Stable presentation-only selection. No gameplay state or network randomness.</summary>
    public readonly struct FestivalAppearance
    {
        public readonly int Gender, Shape, Face, Shirt, Pants, Shoes, Headgear, Sunglasses, FacialHair, Hairstyle, Accessory;
        public readonly int SkinColor, HairColor, ShirtColor, PantsColor, ShoeColor, AccentColor;
        public readonly string Role;

        private FestivalAppearance(string id,string role)
        {
            Role=role;
            Gender=Pick(id,"gender",2);Shape=Pick(id,"shape",3);Face=Pick(id,"face",6);
            Shirt=Pick(id,"shirt",4);Pants=Pick(id,"pants",4);Shoes=Pick(id,"shoes",4);
            Headgear=Optional(id,"headgear",4);Sunglasses=Optional(id,"sunglasses",4);
            FacialHair=Gender==0?Optional(id,"facialhair",4):-1;
            Hairstyle=Pick(id,"hairstyle",4);Accessory=Optional(id,"accessory",4);
            SkinColor=Pick(id,"skin",8);HairColor=Pick(id,"haircolor",10);
            ShirtColor=Pick(id,"shirtcolor",10);PantsColor=Pick(id,"pantscolor",10);
            ShoeColor=Pick(id,"shoecolor",8);AccentColor=Pick(id,"accentcolor",8);
        }

        public static FestivalAppearance For(string id,string role="Attendee") => new FestivalAppearance(id??"",role??"Attendee");
        public static int Pick(string id,string slot,int count)
        {
            unchecked
            {
                uint hash=2166136261;
                foreach(char c in id??""){hash^=c;hash*=16777619;}
                hash^='|';hash*=16777619;
                foreach(char c in slot){hash^=c;hash*=16777619;}
                // Mix the upper bits into the lower bits before small modulo
                // draws. Raw FNV low bits correlated optional presence with
                // variants and made headgear variant zero unreachable.
                hash^=hash>>16;hash*=0x7feb352du;
                hash^=hash>>15;hash*=0x846ca68bu;
                hash^=hash>>16;
                return (int)(hash%(uint)count);
            }
        }
        private static int Optional(string id,string slot,int count)
        {
            // A separate draw keeps the choice of variant independent of presence.
            return Pick(id,slot+"_present",4)==0?-1:Pick(id,slot,count);
        }

        public Vector3 Scale => Shape==0?new Vector3(.87f,1.07f,.88f):Shape==2?new Vector3(1.13f,.94f,1.12f):Vector3.one;

        static readonly Color[] Skin={
            new Color(.32f,.19f,.15f),new Color(.43f,.25f,.18f),new Color(.55f,.34f,.23f),new Color(.68f,.44f,.30f),
            new Color(.77f,.54f,.37f),new Color(.84f,.65f,.48f),new Color(.91f,.76f,.61f),new Color(.96f,.84f,.71f)};
        static readonly Color[] Hair={
            new Color(.10f,.07f,.07f),new Color(.23f,.12f,.09f),new Color(.41f,.23f,.12f),new Color(.66f,.43f,.19f),
            new Color(.85f,.68f,.35f),new Color(.85f,.83f,.73f),new Color(.78f,.29f,.13f),new Color(.22f,.77f,.69f),
            new Color(.81f,.31f,.66f),new Color(.39f,.39f,.85f)};
        static readonly Color[] Cloth={
            new Color(.94f,.32f,.42f),new Color(.19f,.79f,.69f),new Color(.95f,.67f,.24f),new Color(.64f,.40f,.84f),
            new Color(.26f,.49f,.84f),new Color(.83f,.78f,.48f),new Color(.27f,.36f,.29f),new Color(.80f,.48f,.31f),
            new Color(.93f,.83f,.68f),new Color(.18f,.22f,.33f)};
        static readonly Color[] Accent={
            new Color(.99f,.74f,.20f),new Color(.22f,.92f,.78f),new Color(.98f,.45f,.58f),new Color(.52f,.37f,.93f),
            new Color(.95f,.94f,.72f),new Color(.33f,.73f,.98f),new Color(.91f,.49f,.24f),new Color(.30f,.36f,.40f)};

        public Color SkinTint => Skin[SkinColor];
        public Color SleeveTint => Role=="Friend"?new Color(.99f,.44f,.22f):Role=="Security"?new Color(.30f,.48f,.72f):Cloth[ShirtColor];

        public Texture2D CreatePalette()
        {
            var shirt=SleeveTint;
            var pants=Cloth[PantsColor];
            var colors=new[]{Skin[SkinColor],shirt,pants,Cloth[ShoeColor],new Color(.98f,.95f,.82f),
                new Color(.035f,.04f,.08f),Hair[HairColor],Role=="Medic"?new Color(.20f,.92f,.71f):Accent[AccentColor]};
            const int width=512,height=128,cell=width/8;
            var texture=new Texture2D(width,height,TextureFormat.RGBA32,true){name="Festival fabric palette",filterMode=FilterMode.Trilinear,wrapMode=TextureWrapMode.Clamp};
            var pixels=new Color32[width*height];
            for(int y=0;y<height;y++)for(int x=0;x<width;x++)
            {
                int slot=x/cell;
                float shade=1;
                if(slot>=1&&slot<=3)
                {
                    int grain=((x*73+y*151)^(x*y*13))&31;
                    shade=.974f+grain*.0012f+((x+y)%3==0?.010f:0);
                }
                var color=colors[slot]*shade;color.a=1;pixels[y*width+x]=color;
            }
            texture.SetPixels32(pixels);texture.Apply(true,true);
            return texture;
        }
    }
}
