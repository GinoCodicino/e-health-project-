using System;
using System.Collections.Generic;
namespace Level02.DeerRescue
{
    // Generazione verificata, indipendente da Unity. Lo schema seed=0 conserva i test precedenti.
    public sealed class ScrewPuzzle
    {
        readonly int[] colours,parents,columns;
        readonly bool[] removed;
        readonly List<int> tray;
        int[] solution;
        public int Seed {get;private set;}
        public int BoardCount {get;private set;}
        public int TotalCuts {get {return BoardCount*2;}}
        public bool Failed {get;private set;}
        public int Mistakes {get;private set;}
        public int BoxColour {get;private set;}
        public int InBox {get;private set;}
        public int BoxSize {get;private set;}=2;
        public int Count {get;private set;}
        public bool Won {get {return Count==TotalCuts;}}
        public int TrayCount {get {return tray.Count;}}
        public int TrayColour(int i){return tray[i];}
        public int Colour(int cut){return colours[cut];}
        public bool IsRemoved(int cut){return cut>=0 && cut<TotalCuts && removed[cut];}
        public bool BoardGone(int board){return board>=0 && board<BoardCount && removed[board*2] && removed[board*2+1];}
        public int LayoutColumn(int board){return columns[board];}
        public int[] GetSolution(){return solution==null?new int[0]:(int[])solution.Clone();}
        public int NextColour
        {
            get
            {
                for(int offset=1;offset<=3;offset++)
                {
                    int c=(BoxColour+offset)%3;
                    for(int i=0;i<TotalCuts;i++)if(!removed[i] && Colour(i)==c)return c;
                    if(tray.Contains(c))return c;
                }
                return BoxColour;
            }
        }
        public ScrewPuzzle(int seed=0)
        {
            Seed=seed;BoardCount=seed==0?5:6;
            colours=new int[TotalCuts];parents=new int[BoardCount];columns=new int[BoardCount];
            removed=new bool[TotalCuts];tray=new List<int>();
            if(seed==0)
            {
                int[] original={0,1,0,1,0,2,1,2,0,1};Array.Copy(original,colours,10);
                parents[0]=(1<<1)|(1<<2);parents[1]=parents[2]=1<<3;parents[3]=1<<4;
                solution=new[]{8,9,6,7,4,2,5,3,0,1};return;
            }
            var random=new Random(seed);BoxColour=random.Next(3);
            // Due cataste di tre tronchi: l'indice di sinistra/destra cambia per ciascuno strato.
            for(int layer=0;layer<3;layer++)
            {bool swap=random.Next(2)==0;columns[layer*2]=swap?1:0;columns[layer*2+1]=swap?0:1;}
            for(int board=0;board<4;board++)
            {
                int upper=(board/2+1)*2;
                parents[board]=1<<(columns[upper]==columns[board]?upper:upper+1);
            }
            // Rimescola davvero i 12 colori. Accetta solo schemi con soluzione e uso necessario dell'attesa.
            for(int attempt=0;attempt<72;attempt++)
            {
                for(int i=0;i<TotalCuts;i++)colours[i]=i%3;
                for(int i=TotalCuts-1;i>0;i--){int j=random.Next(i+1),c=colours[i];colours[i]=colours[j];colours[j]=c;}
                int[] path;
                if(TrySolve(3,out path))
                {
                    int[] easy;
                    if(!TrySolve(1,out easy)){solution=path;return;}
                }
            }
            // Ripiego deterministico sempre risolvibile: mai mostrare una catasta impossibile.
            int[] order={4,5,2,3,0,1};
            for(int i=0;i<order.Length;i++)colours[order[i]*2]=colours[order[i]*2+1]=(BoxColour+i)%3;
            int[] fallback;if(TrySolve(3,out fallback))solution=fallback;
            else throw new InvalidOperationException("Unable to build a solvable timber puzzle.");
        }
        ScrewPuzzle(ScrewPuzzle source)
        {
            Seed=source.Seed;BoardCount=source.BoardCount;colours=source.colours;parents=source.parents;columns=source.columns;
            removed=(bool[])source.removed.Clone();tray=new List<int>(source.tray);
            BoxColour=source.BoxColour;BoxSize=source.BoxSize;InBox=source.InBox;Count=source.Count;
        }
        public bool Accessible(int board)
        {
            if(board<0 || board>=BoardCount || BoardGone(board))return false;
            for(int i=0;i<BoardCount;i++)if((parents[board]&(1<<i))!=0 && !BoardGone(i))return false;
            return true;
        }
        public void WrongCut(){if(!Won && !Failed){Mistakes++;if(Mistakes>=3)Failed=true;}}
        public void TimeUp(){if(!Won)Failed=true;}
        public bool Remove(int cut)
        {
            if(Failed || Won || cut<0 || cut>=TotalCuts || removed[cut])return false;
            if(!Accessible(cut/2)){WrongCut();return false;}
            int colour=Colour(cut);
            if(colour!=BoxColour && tray.Count>=3){Failed=true;return false;}
            removed[cut]=true;Count++;
            if(colour==BoxColour)InBox++;else tray.Add(colour);
            Resolve();return true;
        }
        void Resolve()
        {
            for(int guard=0;guard<40 && !Won;guard++)
            {
                for(int i=tray.Count-1;i>=0 && InBox<BoxSize;i--)if(tray[i]==BoxColour){tray.RemoveAt(i);InBox++;}
                if(InBox<BoxSize)return;
                BoxColour=(BoxColour+1)%3;InBox=0;int remaining=0;
                for(int i=0;i<TotalCuts;i++)if(!removed[i] && Colour(i)==BoxColour)remaining++;
                foreach(int c in tray)if(c==BoxColour)remaining++;
                BoxSize=Math.Min(2,remaining);
            }
        }
        public bool TrySolve(int waitingCapacity,out int[] path)
        {
            if(Failed){path=new int[0];return false;}
            var result=new List<int>();int budget=16000;
            bool success=Search(new ScrewPuzzle(this),waitingCapacity,new HashSet<ulong>(),result,ref budget);
            path=success?result.ToArray():new int[0];return success;
        }
        static bool Search(ScrewPuzzle state,int capacity,HashSet<ulong> visited,List<int> path,ref int budget)
        {
            if(state.Won)return true;
            if(--budget<0 || !visited.Add(state.Key()))return false;
            // Visita prima i tagli del colore attivo: il controllo di generazione resta rapido.
            for(int pass=0;pass<2;pass++)for(int i=0;i<state.TotalCuts;i++)
            {
                if(state.removed[i] || !state.Accessible(i/2))continue;
                bool match=state.Colour(i)==state.BoxColour;
                if((pass==0)!=match || (!match && state.TrayCount>=capacity))continue;
                var next=new ScrewPuzzle(state);if(!next.Remove(i))continue;
                path.Add(i);if(Search(next,capacity,visited,path,ref budget))return true;path.RemoveAt(path.Count-1);
            }
            return false;
        }
        ulong Key()
        {
            ulong key=0;for(int i=0;i<TotalCuts;i++)if(removed[i])key|=1UL<<i;
            int a=0,b=0,c=0;foreach(int colour in tray){if(colour==0)a++;else if(colour==1)b++;else c++;}
            return key|((ulong)BoxColour<<12)|((ulong)InBox<<14)|((ulong)BoxSize<<16)|((ulong)a<<18)|((ulong)b<<21)|((ulong)c<<24);
        }
    }
}
