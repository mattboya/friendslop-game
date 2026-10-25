using System;

namespace Festival.Core
{
    /// <summary>Each level's spin seed shuffles two different loops of the seven (StageBeats.Tracks, one-based) onto the main and the
    /// small stage. The pick reads only the public spin seed, so every view works it out as the host does, and a rejoin or a
    /// restored snapshot keeps it.</summary>
    public static class StageMusic
    {
        public const int Main=0,Small=1;
        // Each stage's front, where its speakers stand.
        public const float MainX=0,MainZ=27.5f,SmallX=-12,SmallZ=-28.5f;
        // One warm-up draw, as the Giggle Tank's stream takes, so the pick never reads a fresh stream's first value.
        public static int Track(int spinSeed,int stage)
        {
            if(stage!=Main&&stage!=Small)throw new ArgumentOutOfRangeException(nameof(stage));
            var random=new ContentRandom(unchecked(spinSeed*47+23));random.Next(100);
            int count=StageBeats.Tracks.Length,main=1+random.Next(count);if(stage==Main)return main;
            int small=1+random.Next(count-1);return small>=main?small+1:small;
        }
        public static int Track(RoundState s,int stage)=>Track(s.SpinSeed,stage);
        /// <summary>The stage whose front is nearer (x, z); the main stage on a tie.</summary>
        public static int Nearer(float x,float z)=>Square(x-SmallX,z-SmallZ)<Square(x-MainX,z-MainZ)?Small:Main;
        /// <summary>The song a performance started at (x, z) charts to.</summary>
        public static int TrackAt(RoundState s,float x,float z)=>Track(s,Nearer(x,z));
        static double Square(double dx,double dz)=>dx*dx+dz*dz;
    }
}
