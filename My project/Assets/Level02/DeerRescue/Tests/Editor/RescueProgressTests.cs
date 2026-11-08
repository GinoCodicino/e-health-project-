using NUnit.Framework;
namespace Level02.DeerRescue.Tests
{
    public sealed class RescueProgressTests
    {
        [Test] public void GiftsAreRequiredBeforeFishing()
        {
            var p=new RescueProgress();p.AcceptRescue();Assert.IsFalse(p.AcceptFishing());
            p.GrantPeterGifts(false);Assert.IsFalse(p.HasNet);p.GrantPeterGifts(true);
            Assert.IsTrue(p.HasNet);Assert.IsTrue(p.HasMedicalKit);Assert.IsTrue(p.AcceptFishing());
        }
        [Test] public void TenFishRequiredAndNoExtraPeterTrip()
        {
            var p=new RescueProgress();p.GrantPeterGifts(true);p.AcceptRescue();p.AcceptFishing();
            Assert.IsFalse(p.FinishFishing(3));Assert.IsFalse(p.FinishFishing(9));Assert.IsTrue(p.FinishFishing(10));Assert.IsFalse(p.HasNet);
            Assert.IsTrue(p.ExchangeFish());Assert.IsFalse(p.ExchangeFish());Assert.IsTrue(p.CompleteRelease());Assert.IsFalse(p.HasAxe);
            Assert.AreEqual(RescueStage.TreatDeer,p.Stage);Assert.IsTrue(p.CompleteCare());
            Assert.AreEqual(RescueStage.Complete,p.Stage);Assert.IsFalse(p.HasMedicalKit);Assert.IsFalse(p.CompleteCare());p.GrantPeterGifts(true);Assert.IsFalse(p.HasMedicalKit);Assert.IsFalse(p.HasNet);Assert.IsFalse(p.HasAxe);
        }
        [Test] public void CannotSkipReleaseOrCare()
        {var p=new RescueProgress();p.GrantPeterGifts(true);Assert.IsFalse(p.CompleteRelease());Assert.IsFalse(p.CompleteCare());}
        [Test] public void DeadlineCheckedBeforeLastFish()
        {
            var f=new FishingRound();f.Begin(10,100,90);
            for(int i=0;i<9;i++)f.Catch(true,101+i);
            Assert.IsFalse(f.Catch(true,190));Assert.AreEqual(FishingResult.Failed,f.Result);
        }
        [Test] public void TrashBlocksBrieflyAndRetryResets()
        {
            var f=new FishingRound();f.Begin(10,100,90);f.Catch(true,101);f.Catch(false,102);
            Assert.IsFalse(f.Catch(true,102.5));Assert.AreEqual(1,f.Caught);Assert.IsTrue(f.Catch(true,102.7));
            f.Cancel();f.Begin(10,200,90);Assert.AreEqual(0,f.Caught);Assert.AreEqual(90,f.Remaining(200));
        }
        [Test] public void LayeredPuzzleHasACompleteLegalSolution()
        {
            var p=new ScrewPuzzle();Assert.IsFalse(p.Remove(0));
            // Ordine documentato: svuota le scatole senza saturare la vaschetta.
            foreach(int id in new[]{8,9,6,7,4,2,5,3,0,1})Assert.IsTrue(p.Remove(id),"Screw "+id);
            Assert.IsTrue(p.Won);Assert.AreEqual(10,p.Count);Assert.IsFalse(p.Remove(8));
        }
        [Test] public void FullTrayRejectsWithoutConsumingScrew()
        {
            var p=new ScrewPuzzle();p.Remove(9);p.Remove(8);p.Remove(6);p.Remove(7);
            Assert.AreEqual(3,p.TrayCount);Assert.IsFalse(p.Remove(5));Assert.IsFalse(p.IsRemoved(5));
        }
        [Test] public void BattleNeedsCorrectToolsTwoHitsAndAllFragmentsCleared()
        {
            var p=new CareBattle();Assert.IsFalse(p.Hit(0,KitTool.Tweezers));Assert.AreEqual(1,p.Danger);
            for(int i=0;i<6;i++){Assert.IsTrue(p.Hit(i,KitTool.Disinfectant));Assert.AreEqual(1,p.Health(i));Assert.IsTrue(p.Hit(i,KitTool.Disinfectant));}
            Assert.IsFalse(p.Won(1));Assert.IsTrue(p.Won(0));Assert.IsFalse(p.Hit(0,KitTool.Disinfectant));
        }
        [Test] public void FiveMistakesFailWithoutAwardingCompletion()
        {var p=new CareBattle();for(int i=0;i<5;i++)p.Miss();Assert.IsTrue(p.Lost);Assert.IsFalse(p.Hit(0,KitTool.Disinfectant));Assert.IsFalse(p.Won(0));}
    }
}
