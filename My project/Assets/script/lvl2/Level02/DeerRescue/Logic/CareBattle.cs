namespace Level02.DeerRescue
{
    public enum KitTool { Tweezers,Disinfectant }
    public sealed class CareBattle
    {
        public const int EnemyCount=6;
        readonly int[] health={2,2,2,2,2,2};
        public int Danger { get; private set; }
        public int Defeated { get; private set; }
        public bool Lost {get {return Danger>=5;}}
        public int Health(int id){return id>=0 && id<EnemyCount ? health[id] : 0;}
        public bool Hit(int id,KitTool tool)
        {
            if(Lost || id<0 || id>=EnemyCount || health[id]<=0)return false;
            if(tool!=KitTool.Disinfectant){Miss();return false;}
            health[id]--;if(health[id]==0)Defeated++;return true;
        }
        public bool RemoveSplinter(KitTool tool)
        {if(Lost)return false;if(tool!=KitTool.Tweezers){Miss();return false;}return true;}
        public void Miss(){if(!Lost)Danger++;}
        public bool Won(int activeSplinters){return !Lost && Defeated==EnemyCount && activeSplinters==0;}
    }
}
