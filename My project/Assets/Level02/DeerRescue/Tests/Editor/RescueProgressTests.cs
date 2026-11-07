using NUnit.Framework;
namespace Level02.DeerRescue.Tests
{
    public sealed class RescueProgressTests
    {
        RescueProgress AtDeer()
        {
            var p=new RescueProgress();Assert.IsTrue(p.AcceptRescue());Assert.IsTrue(p.AcceptFishing());
            Assert.IsTrue(p.FinishFishing(3));Assert.IsTrue(p.ExchangeFish());return p;
        }
        void Free(RescueProgress p)
        {for(int branch=0;branch<3;branch++)for(int hit=0;hit<3;hit++)Assert.IsTrue(p.HitBranch(branch));}
        [TestCase(3)] [TestCase(4)] public void FishingNeedsExactlyTheTarget(int goal)
        {
            var f=new FishingRound();Assert.IsTrue(f.Begin(goal,100,45));
            for(int i=0;i<goal-1;i++)Assert.IsTrue(f.Catch(true,101+i));
            Assert.AreEqual(FishingResult.Running,f.Result);f.Catch(true,110);
            Assert.AreEqual(FishingResult.Won,f.Result);Assert.AreEqual(goal,f.Caught);
            Assert.IsFalse(f.Catch(true,111));Assert.AreEqual(goal,f.Caught);
        }
        [Test] public void DeadlineIsCheckedBeforeAwardingLastFish()
        {
            var f=new FishingRound();f.Begin(3,100,45);f.Catch(true,101);f.Catch(true,102);
            Assert.IsFalse(f.Catch(true,145));Assert.AreEqual(FishingResult.Failed,f.Result);Assert.AreEqual(2,f.Caught);
        }
        [Test] public void ObstaclesBlockTheNetBrieflyWithoutRemovingFish()
        {
            var f=new FishingRound();f.Begin(3,100,45);f.Catch(true,101);f.Catch(false,102);
            Assert.IsTrue(f.IsBlocked(102.5));Assert.IsFalse(f.Catch(true,102.5));Assert.AreEqual(1,f.Caught);
            Assert.IsTrue(f.Catch(true,102.7));Assert.AreEqual(2,f.Caught);
        }
        [Test] public void RetryResetsClockFishAndBlock()
        {
            var f=new FishingRound();f.Begin(3,100,45);f.Catch(true,101);f.Catch(false,102);f.Tick(145);
            f.Begin(4,200,45);Assert.AreEqual(0,f.Caught);Assert.AreEqual(45,f.Remaining(200));Assert.IsFalse(f.IsBlocked(200));
        }
        [Test] public void CancelDoesNotTurnFailureIntoSuccess()
        {var f=new FishingRound();f.Begin(3,100,45);f.Cancel();Assert.AreEqual(FishingResult.Failed,f.Result);Assert.IsFalse(f.Catch(true,101));}
        [Test] public void CannotGetAxeBeforeFishOrExchangeTwice()
        {
            var p=new RescueProgress();Assert.IsFalse(p.ExchangeFish());Assert.IsFalse(p.FinishFishing(3));
            p.AcceptRescue();p.AcceptFishing();Assert.IsFalse(p.FinishFishing(2));p.FinishFishing(4);
            Assert.AreEqual(4,p.Fish);Assert.IsTrue(p.ExchangeFish());Assert.IsTrue(p.HasAxe);Assert.AreEqual(0,p.Fish);Assert.IsFalse(p.ExchangeFish());
        }
        [Test] public void WrongBranchOrRepeatedOldBranchCannotAdvance()
        {
            var p=AtDeer();Assert.IsFalse(p.HitBranch(1));Assert.AreEqual(0,p.HitsOnBranch);
            p.HitBranch(0);p.HitBranch(0);p.HitBranch(0);Assert.AreEqual(1,p.ClearedBranches);
            Assert.IsFalse(p.HitBranch(0));Assert.AreEqual(1,p.ClearedBranches);
        }
        [Test] public void ClosingScreensCanKeepPartialBranchProgress()
        {var p=AtDeer();p.HitBranch(0);p.HitBranch(0);Assert.AreEqual(2,p.HitsOnBranch);p.HitBranch(0);Assert.AreEqual(1,p.ClearedBranches);}
        [Test] public void BandagesRequireDeerFreedAndFirstQuestCompleted()
        {
            var p=AtDeer();Assert.IsFalse(p.ReceiveBandages(true));Free(p);
            Assert.AreEqual(RescueStage.GetBandages,p.Stage);Assert.IsFalse(p.ReceiveBandages(false));
            Assert.IsTrue(p.ReceiveBandages(true));Assert.IsFalse(p.ReceiveBandages(true));
        }
        [Test] public void TreatmentRequiresDressingAndWrapOrder()
        {
            var p=AtDeer();Free(p);p.ReceiveBandages(true);Assert.IsFalse(p.Wrap(0));
            Assert.IsTrue(p.PlaceDressing());Assert.IsFalse(p.PlaceDressing());Assert.IsFalse(p.Wrap(1));
            Assert.IsTrue(p.Wrap(0));Assert.IsFalse(p.Wrap(0));Assert.IsTrue(p.Wrap(1));Assert.IsTrue(p.Wrap(2));
            Assert.AreEqual(RescueStage.Complete,p.Stage);Assert.IsFalse(p.HasBandages);Assert.IsFalse(p.Wrap(2));
        }
    }
}
