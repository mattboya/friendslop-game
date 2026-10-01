using System;
using System.Collections.Generic;

namespace Festival.Core
{
    /// <summary>The spinners: who trips this level and how hard. The result is public state, so every client animates the same spin.</summary>
    public sealed partial class FestivalSimulation
    {
        public const string DoseEffect="dose";
        // SPIN-2: the spin's one timeline, which FestivalSpinner animates. The wheels go one after another, each turning for
        // WheelSeconds and resting PauseSeconds on its result; then the tripper takes the dose and reacts. The level clock only
        // runs while Playing, so it waits out all of it.
        public const int WheelCount=2;
        public const double WheelSeconds=4,PauseSeconds=.6,TakeSeconds=1.4,ReactSeconds=1.6;
        public const double SpinSeconds=WheelCount*(WheelSeconds+PauseSeconds)+TakeSeconds+ReactSeconds;
        // Dose spinner slices in percent for 1-4 doses; the 4-dose slice is a thin sliver.
        static readonly int[] DoseWeights={40,30,22,8};
        // The dose wheel draws these same slices, so what players see is what the spinner rolls.
        public static IReadOnlyList<int> DoseSlices=>DoseWeights;
        static readonly float[] DoseSpeeds={.92f,.88f,.84f,.80f};
        public static float DoseMovementMultiplier(int dose)=>dose<1?1:DoseSpeeds[Math.Min(dose,DoseSpeeds.Length)-1];
        // The medical tent treats other effects but cannot talk anyone down from the spinner's dose.
        public static bool Treatable(ActiveEffect effect)=>effect.Id!=DoseEffect;

        // Both spinners run as the crew leaves camp. Night 2 doses everyone and hands the people spinner's pick the
        // highest dose rolled, so the tripper has the max and each friend still trips exactly once a weekend.
        void Spin(List<PlayerState> crew)
        {
            State.SpinSeed=unchecked(State.Seed*31+(int)State.Tick);var random=new ContentRandom(State.SpinSeed);
            var tripper=SpinPeople(crew,random);
            var dosed=State.LevelIndex==Festivals.LevelCount-1?crew:new List<PlayerState>{tripper};
            var doses=dosed.ConvertAll(p=>SpinDose(random));
            int mine=dosed.IndexOf(tripper),top=mine;for(int i=0;i<doses.Count;i++)if(doses[i]>doses[top])top=i;
            int highest=doses[top];doses[top]=doses[mine];doses[mine]=highest;
            State.Doses.Clear();for(int i=0;i<dosed.Count;i++)TakeDose(dosed[i],doses[i]);
            State.TripperId=tripper.Id;State.SpinEndsAt=State.SimulationSeconds+SpinSeconds;State.Phase="Spinning";
        }
        // People spinner: a shuffle bag kept for the weekend, so nobody goes twice until everyone has had a turn.
        PlayerState SpinPeople(List<PlayerState> candidates,ContentRandom random)
        {
            var due=candidates.FindAll(p=>State.TripperBag.Contains(p.Id));
            if(due.Count==0){State.TripperBag=State.Players.FindAll(p=>p.Connected).ConvertAll(p=>p.Id);due=candidates;}
            var pick=due[random.Next(due.Count)];State.TripperBag.Remove(pick.Id);return pick;
        }
        static int SpinDose(ContentRandom random){int roll=random.Next(100);for(int dose=0;;dose++)if((roll-=DoseWeights[dose])<0)return dose+1;}
        // A tripper who dies, leaves, or is revived without their dose is replaced by the people spinner among the
        // living. A stand-in without a dose takes 1; on Night 2 they keep the dose they already took.
        void KeepTripper()
        {
            if(State.TripperId==""||Tripping(Player(State.TripperId)))return;
            var living=State.Players.FindAll(p=>p.Connected&&p.Life!="Spirit");if(living.Count==0)return;
            var pick=SpinPeople(living,new ContentRandom(unchecked(State.SpinSeed+(int)State.Tick)));
            if(DoseOf(pick)==0)TakeDose(pick,1);
            State.TripperId=pick.Id;
        }
        static bool Tripping(PlayerState p)=>p!=null&&p.Connected&&p.Life!="Spirit"&&DoseOf(p)>0;
        static int DoseOf(PlayerState p)=>p.Effects.Find(e=>e.Id==DoseEffect)?.Intensity??0;
        // The dose lasts the whole level; effects only tick down while Playing.
        void TakeDose(PlayerState p,int dose)
        {
            p.Effects.Add(new ActiveEffect{Id=DoseEffect,InstanceId=Id("effect"),Intensity=dose,StartSeconds=State.SimulationSeconds,RemainingSeconds=State.DurationSeconds});
            var entry=State.Doses.Find(d=>d.PlayerId==p.Id);if(entry==null)State.Doses.Add(new PlayerDose{PlayerId=p.Id,Dose=dose});else entry.Dose=dose;
        }
    }
}
