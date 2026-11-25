using System;

namespace Level02.BridgeMission
{
    public enum BridgeStage { MeetFisher, FindWoodcutter, PrepareWood, RepairBridge, ReportRepair, Complete }
    public enum ChopResult { Idle, Running, Failed, Won }

    // Logica senza dipendenze da Unity: ricompense e transizioni hanno un unico proprietario.
    public sealed class BridgeQuestProgress
    {
        public BridgeStage Stage { get; private set; } = BridgeStage.MeetFisher;
        public ChopResult Attempt { get; private set; }
        public int RequiredLogs { get; private set; }
        public int CompletedLogs { get; private set; }
        public int HitsOnLog { get { return SelectedLog >= 0 && logHits != null ? logHits[SelectedLog] : 0; } }
        public int SelectedLog { get; private set; } = -1;
        int[] logHits;
        public int HitsPerLog { get; private set; }
        public int Planks { get; private set; }
        public int PlacedCount { get; private set; }
        readonly bool[] placed = new bool[3];
        double deadline;

        public bool AcceptFisherTask()
        {
            if (Stage != BridgeStage.MeetFisher) return false;
            Stage = BridgeStage.FindWoodcutter; return true;
        }
        public bool StartAttempt(int logs, int hits, double now, double duration)
        {
            if ((Stage != BridgeStage.FindWoodcutter && Stage != BridgeStage.PrepareWood)
                || Attempt == ChopResult.Won || logs < 3 || logs > 4 || hits < 1
                || !Finite(now) || !Finite(duration) || duration <= 0 || !Finite(now+duration)) return false;
            Stage = BridgeStage.PrepareWood;
            RequiredLogs = logs; HitsPerLog = hits;
            CompletedLogs = 0; SelectedLog = -1; logHits = new int[logs];
            deadline = now + duration; Attempt = ChopResult.Running; return true;
        }
        public double Remaining(double now) { return Math.Max(0, deadline-now); }
        public void Tick(double now)
        {
            if (Attempt == ChopResult.Running && now >= deadline) Attempt = ChopResult.Failed;
        }
        public int HitsForLog(int index)
        { return logHits != null && index >= 0 && index < logHits.Length ? logHits[index] : 0; }
        public bool IsLogComplete(int index)
        { return logHits != null && index >= 0 && index < logHits.Length && logHits[index] >= HitsPerLog; }
        public bool SelectLog(int index)
        {
            if (Attempt != ChopResult.Running || index < 0 || index >= RequiredLogs || IsLogComplete(index)) return false;
            SelectedLog = index; return true;
        }
        public bool HitLog(int index, double now)
        {
            Tick(now);
            if (Attempt != ChopResult.Running || index < 0 || index >= RequiredLogs || IsLogComplete(index)) return false;
            SelectedLog = index; logHits[index]++;
            if (IsLogComplete(index))
            {
                CompletedLogs++;
                if (CompletedLogs == RequiredLogs) Attempt = ChopResult.Won;
            }
            return true;
        }
        // Compatibilita con i test e con eventuali chiamanti della prima versione.
        public bool Hit(double now)
        {
            int index = SelectedLog;
            if (index < 0 || IsLogComplete(index))
            {
                index = 0;
                while (index < RequiredLogs && IsLogComplete(index)) index++;
            }
            return HitLog(index, now);
        }
        public void CancelAttempt()
        {
            if (Attempt == ChopResult.Running) Attempt = ChopResult.Failed;
        }
        public bool TakePlanks()
        {
            if (Stage != BridgeStage.PrepareWood || Attempt != ChopResult.Won) return false;
            Planks = 3; Stage = BridgeStage.RepairBridge; return true;
        }
        public bool IsPlaced(int slot) { return slot >= 0 && slot < 3 && placed[slot]; }
        public bool PlacePlank(int plank, int slot)
        {
            if (Stage != BridgeStage.RepairBridge || slot < 0 || slot >= 3
                || plank != slot || placed[slot] || Planks < 1) return false;
            placed[slot] = true; PlacedCount++; Planks--;
            if (PlacedCount == 3) Stage = BridgeStage.ReportRepair;
            return true;
        }
        public bool Finish()
        {
            if (Stage != BridgeStage.ReportRepair) return false;
            Stage = BridgeStage.Complete; return true;
        }
        static bool Finite(double value) { return !double.IsNaN(value) && !double.IsInfinity(value); }
    }
}
