using NUnit.Framework;

namespace Level02.BridgeMission.Tests
{
    public sealed class BridgeQuestProgressTests
    {
        BridgeQuestProgress Begin(int logs=3)
        {
            var p=new BridgeQuestProgress();
            Assert.IsTrue(p.AcceptFisherTask());
            Assert.IsTrue(p.StartAttempt(logs,5,100,30)); return p;
        }
        void Win(BridgeQuestProgress p)
        {
            for(int i=0;i<p.RequiredLogs*5;i++) Assert.IsTrue(p.Hit(101+i*.1));
            Assert.AreEqual(ChopResult.Won,p.Attempt);
        }
        [Test] public void CannotStartBeforeFisherTask()
        { Assert.IsFalse(new BridgeQuestProgress().StartAttempt(3,5,0,30)); }
        [TestCase(3)] [TestCase(4)]
        public void ExactlyFiveHitsPrepareEachLog(int logs)
        {
            var p=Begin(logs);
            for(int i=0;i<4;i++) p.Hit(101);
            Assert.AreEqual(0,p.CompletedLogs); Assert.AreEqual(4,p.HitsOnLog);
            p.Hit(101); Assert.AreEqual(1,p.CompletedLogs); Assert.IsTrue(p.IsLogComplete(0));
            for(int i=5;i<logs*5;i++) p.Hit(102);
            Assert.AreEqual(ChopResult.Won,p.Attempt);
        }
        [Test] public void HitAtDeadlineFailsInsteadOfWinning()
        {
            var p=Begin(); for(int i=0;i<14;i++) p.Hit(101);
            Assert.IsFalse(p.Hit(130)); Assert.AreEqual(ChopResult.Failed,p.Attempt);
            Assert.IsFalse(p.TakePlanks()); Assert.AreEqual(0,p.Planks);
        }
        [Test] public void TimerFailsWithoutAHit()
        { var p=Begin(); p.Tick(131); Assert.AreEqual(ChopResult.Failed,p.Attempt); }
        [Test] public void RetryResetsHitsLogsAndDeadline()
        {
            var p=Begin(); for(int i=0;i<7;i++) p.Hit(101); p.Tick(130);
            Assert.IsTrue(p.StartAttempt(4,5,200,30));
            Assert.AreEqual(0,p.CompletedLogs); Assert.AreEqual(0,p.HitsOnLog);
            Assert.AreEqual(30,p.Remaining(200)); Assert.AreEqual(4,p.RequiredLogs);
        }
        [Test] public void ClosingRunningAttemptCancelsButDoesNotAward()
        { var p=Begin(); p.CancelAttempt(); Assert.AreEqual(ChopResult.Failed,p.Attempt); Assert.IsFalse(p.TakePlanks()); }
        [Test] public void SuccessfulAttemptSurvivesClosingAndRewardsOnlyOnce()
        {
            var p=Begin(); Win(p); p.CancelAttempt();
            Assert.AreEqual(ChopResult.Won,p.Attempt);
            Assert.IsTrue(p.TakePlanks()); Assert.IsFalse(p.TakePlanks()); Assert.AreEqual(3,p.Planks);
            Assert.IsFalse(p.StartAttempt(3,5,200,30));
        }
        [Test] public void WrongOrRepeatedPuzzlePlacementDoesNotConsumeWood()
        {
            var p=Begin(); Win(p); p.TakePlanks();
            Assert.IsFalse(p.PlacePlank(0,1)); Assert.AreEqual(3,p.Planks);
            Assert.IsTrue(p.PlacePlank(0,0)); Assert.AreEqual(2,p.Planks);
            Assert.IsFalse(p.PlacePlank(0,0)); Assert.AreEqual(2,p.Planks);
        }
        [Test] public void AllThreeSlotsUnlockBridgeAndFinalReport()
        {
            var p=Begin(); Assert.IsFalse(p.PlacePlank(0,0)); Win(p); p.TakePlanks();
            p.PlacePlank(2,2); Assert.IsFalse(p.Finish());
            p.PlacePlank(0,0); Assert.AreEqual(BridgeStage.RepairBridge,p.Stage);
            Assert.IsTrue(p.IsPlaced(2)); Assert.IsFalse(p.IsPlaced(1));
            p.PlacePlank(1,1); Assert.AreEqual(0,p.Planks); Assert.AreEqual(BridgeStage.ReportRepair,p.Stage);
            Assert.IsTrue(p.Finish()); Assert.AreEqual(BridgeStage.Complete,p.Stage); Assert.IsFalse(p.Finish());
        }
        [Test] public void LogsCanBeSelectedInAnyOrderAndKeepTheirDamage()
        {
            var p=Begin(4);
            Assert.IsTrue(p.SelectLog(3));
            p.HitLog(3,101); p.HitLog(3,102);
            Assert.IsTrue(p.SelectLog(0)); p.HitLog(0,103);
            Assert.AreEqual(2,p.HitsForLog(3)); Assert.AreEqual(1,p.HitsForLog(0));
            p.SelectLog(3); Assert.AreEqual(2,p.HitsOnLog);
            p.HitLog(3,104); p.HitLog(3,105); p.HitLog(3,106);
            Assert.AreEqual(1,p.CompletedLogs); Assert.IsTrue(p.IsLogComplete(3));
            Assert.IsFalse(p.SelectLog(3)); Assert.IsFalse(p.HitLog(3,107));
            Assert.AreEqual(1,p.CompletedLogs);
        }
        [Test] public void SelectingAnotherLogDoesNotResetTheDeadline()
        {
            var p=Begin(); p.SelectLog(2); p.HitLog(2,110); p.SelectLog(0);
            Assert.AreEqual(10,p.Remaining(120)); p.Tick(130);
            Assert.AreEqual(ChopResult.Failed,p.Attempt); Assert.IsFalse(p.TakePlanks());
        }
        [Test] public void InvalidLogIndicesCannotCountAsHits()
        {
            var p=Begin(); Assert.IsFalse(p.SelectLog(-1)); Assert.IsFalse(p.SelectLog(3));
            Assert.IsFalse(p.HitLog(-1,101)); Assert.IsFalse(p.HitLog(3,101));
            Assert.AreEqual(0,p.CompletedLogs);
        }
        [Test] public void InvalidTimingAndLogCountsAreRejected()
        {
            var p=new BridgeQuestProgress(); p.AcceptFisherTask();
            Assert.IsFalse(p.StartAttempt(2,5,0,30)); Assert.IsFalse(p.StartAttempt(5,5,0,30));
            Assert.IsFalse(p.StartAttempt(3,5,double.NaN,30)); Assert.IsFalse(p.StartAttempt(3,5,0,double.PositiveInfinity));
            Assert.IsFalse(p.StartAttempt(3,0,0,30)); Assert.IsFalse(p.StartAttempt(3,5,0,0));
        }
    }
}
