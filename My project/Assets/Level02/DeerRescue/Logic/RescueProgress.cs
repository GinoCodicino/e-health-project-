using System;

namespace Level02.DeerRescue
{
    public enum RescueStage { WaitingForCrossing, FindWoodcutter, CatchFish, DeliverFish, FreeDeer, GetBandages, TreatDeer, Complete }
    public enum FishingResult { Idle, Running, Failed, Won }
    public sealed class FishingRound
    {
        public FishingResult Result { get; private set; }
        public int Goal { get; private set; }
        public int Caught { get; private set; }
        double deadline,blockedUntil;
        public bool Begin(int goal,double now,double seconds)
        {
            if(goal<3 || goal>4 || double.IsNaN(now) || double.IsInfinity(now)
                || double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds<=0) return false;
            Goal=goal; Caught=0; deadline=now+seconds; blockedUntil=now; Result=FishingResult.Running; return true;
        }
        public double Remaining(double now) { return Math.Max(0,deadline-now); }
        public bool IsBlocked(double now) { return now<blockedUntil; }
        public void Tick(double now) { if(Result==FishingResult.Running && now>=deadline) Result=FishingResult.Failed; }
        public bool Catch(bool fish,double now)
        {
            Tick(now);
            if(Result!=FishingResult.Running || IsBlocked(now)) return false;
            if(fish)
            {
                Caught++;
                if(Caught>=Goal) Result=FishingResult.Won;
            }
            else blockedUntil=now+.65;
            return true;
        }
        public void Cancel() { if(Result==FishingResult.Running) Result=FishingResult.Failed; }
    }
    public sealed class RescueProgress
    {
        public RescueStage Stage { get; private set; }
        public int Fish { get; private set; }
        public bool HasAxe { get; private set; }
        public bool HasBandages { get; private set; }
        public int ClearedBranches { get; private set; }
        public int HitsOnBranch { get; private set; }
        public bool DressingPlaced { get; private set; }
        public int WrapSteps { get; private set; }
        public bool AcceptRescue()
        { if(Stage!=RescueStage.WaitingForCrossing) return false; Stage=RescueStage.FindWoodcutter; return true; }
        public bool AcceptFishing()
        { if(Stage!=RescueStage.FindWoodcutter) return false; Stage=RescueStage.CatchFish; return true; }
        public bool FinishFishing(int fish)
        { if(Stage!=RescueStage.CatchFish || fish<3 || fish>4) return false; Fish=fish; Stage=RescueStage.DeliverFish; return true; }
        public bool ExchangeFish()
        { if(Stage!=RescueStage.DeliverFish || Fish<3) return false; Fish=0; HasAxe=true; Stage=RescueStage.FreeDeer; return true; }
        public bool HitBranch(int branch)
        {
            if(Stage!=RescueStage.FreeDeer || !HasAxe || branch!=ClearedBranches) return false;
            HitsOnBranch++;
            if(HitsOnBranch==3)
            {
                HitsOnBranch=0; ClearedBranches++;
                if(ClearedBranches==3) Stage=RescueStage.GetBandages;
            }
            return true;
        }
        public bool ReceiveBandages(bool firstQuestComplete)
        {
            if(Stage!=RescueStage.GetBandages || !firstQuestComplete) return false;
            HasBandages=true; Stage=RescueStage.TreatDeer; return true;
        }
        public bool PlaceDressing()
        { if(Stage!=RescueStage.TreatDeer || !HasBandages || DressingPlaced) return false; DressingPlaced=true; return true; }
        public bool Wrap(int step)
        {
            if(Stage!=RescueStage.TreatDeer || !DressingPlaced || step!=WrapSteps) return false;
            WrapSteps++;
            if(WrapSteps==3) { HasBandages=false; Stage=RescueStage.Complete; }
            return true;
        }
    }
}
