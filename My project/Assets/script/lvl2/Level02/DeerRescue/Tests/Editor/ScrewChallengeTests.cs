using NUnit.Framework;
namespace Level02.DeerRescue.Tests
{
    public sealed class ScrewChallengeTests
    {
        [Test] public void FourthWaitingChipLosesAndCannotContinue()
        {
            var p=new ScrewPuzzle();foreach(int id in new[]{8,9,6,7})Assert.IsTrue(p.Remove(id));
            Assert.AreEqual(3,p.TrayCount);Assert.IsFalse(p.Failed);
            Assert.IsFalse(p.Remove(5));Assert.IsTrue(p.Failed);Assert.IsFalse(p.Remove(4));Assert.IsFalse(p.Won);
        }
        [Test] public void ThreeCoveredCutsLoseWithoutRemovingTimber()
        {
            var p=new ScrewPuzzle();for(int i=0;i<3;i++)Assert.IsFalse(p.Remove(0));
            Assert.AreEqual(3,p.Mistakes);Assert.AreEqual(0,p.Count);Assert.IsTrue(p.Failed);
        }
        [Test] public void TimeoutFailsAndNewAttemptResetsEverything()
        {
            var p=new ScrewPuzzle();p.Remove(8);p.TimeUp();Assert.IsTrue(p.Failed);Assert.IsFalse(p.Remove(9));
            p=new ScrewPuzzle(91);Assert.IsFalse(p.Failed);Assert.AreEqual(0,p.Count);Assert.AreEqual(0,p.TrayCount);Assert.AreEqual(0,p.Mistakes);
        }
        [Test] public void GeneratedLayoutsHaveValidatedSolutions()
        {
            for(int seed=1;seed<=100;seed++)
            {
                var p=new ScrewPuzzle(seed);Assert.AreEqual(6,p.BoardCount);Assert.AreEqual(12,p.TotalCuts);
                var path=p.GetSolution();Assert.AreEqual(12,path.Length,"Seed "+seed);
                foreach(int cut in path)Assert.IsTrue(p.Remove(cut),"Seed "+seed+" cut "+cut);
                Assert.IsTrue(p.Won);Assert.IsFalse(p.Failed);Assert.AreEqual(0,p.Mistakes);
            }
        }
        [Test] public void SeedReproducesColoursAndColumns()
        {
            var a=new ScrewPuzzle(42);var b=new ScrewPuzzle(42);
            for(int i=0;i<a.TotalCuts;i++)Assert.AreEqual(a.Colour(i),b.Colour(i));
            for(int i=0;i<a.BoardCount;i++)Assert.AreEqual(a.LayoutColumn(i),b.LayoutColumn(i));
        }
    }
}
