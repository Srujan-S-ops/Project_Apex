using System.Collections.Generic;
using Apex.AI;
using Apex.Core;
using Apex.Vehicle;
using UnityEngine;

namespace Apex.Race
{
    /// <summary>Per-car race state: tracking, timing, validity, position and statistics.</summary>
    public sealed class RaceParticipant
    {
        public int Id;
        public string Name;
        public string Code;
        public string Team;
        public string CarName;
        public int Number;
        public bool IsPlayer;
        public Personality Personality;
        public Color TeamColor;
        public CarController Car;
        public AIDriver AI;
        public ICarInput HumanInput;

        // track tracking
        public int TrackIndex = -1;
        public float CentreDistance;
        public float DistFromLine;
        public float Lateral;
        public float TotalDistance;
        public bool OffTrack;
        public bool WrongWay;
        public float WrongWayTimer;

        // timing
        public int Lap = 1;                 // current lap (1-based)
        public int LapsCompleted;
        public int NextCheckpoint;
        public bool LapValid = true;
        public float LapStartTime;
        public float SectorStartTime;
        public int CurrentSector;
        public readonly float[] CurrentSectors = new float[3];
        public readonly List<LapData> Laps = new List<LapData>();
        public float BestLap;
        public readonly float[] BestSectors = new float[3];
        public float LastLap;
        public readonly List<float> ReferenceSamples = new List<float>();   // elapsed every 10 m this lap

        // classification
        public int Position;
        public bool Finished;
        public float FinishTime;
        public int FinishOrder;
        public bool Dnf;
        public float GapToLeader;
        public float Interval;
        public readonly List<float> MarkerTimes = new List<float>();      // race time every 20 m of total distance
        public int StartPosition;

        // statistics
        public float TopSpeed;
        public int TrackLimitWarnings;
        public int Recoveries;

        // recovery
        public float StuckTimer;
        public float FlipTimer;
        public float GhostTimer;

        public float CurrentLapTime(float raceTime) => raceTime - LapStartTime;

        public float AverageLap
        {
            get
            {
                if (Laps.Count == 0) return 0f;
                float s = 0f; foreach (var l in Laps) s += l.time;
                return s / Laps.Count;
            }
        }

        public float LapStdDev
        {
            get
            {
                if (Laps.Count < 2) return 0f;
                float avg = AverageLap, s = 0f;
                foreach (var l in Laps) s += (l.time - avg) * (l.time - avg);
                return Mathf.Sqrt(s / (Laps.Count - 1));
            }
        }
    }
}
