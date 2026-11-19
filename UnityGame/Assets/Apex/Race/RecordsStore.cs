using System;
using System.Collections.Generic;
using UnityEngine;

namespace Apex.Race
{
    [Serializable]
    public sealed class LapData
    {
        public int lap;
        public float time;
        public float s1, s2, s3;
        public bool valid;
    }

    [Serializable]
    public sealed class ResultEntry
    {
        public int driverId;
        public string name;
        public string code;
        public string car;
        public bool isPlayer;
        public int position;
        public float totalTime;
        public float bestLap;
        public int laps;
        public bool dnf;
        public float gap;          // seconds behind winner (if same lap)
        public int lapsDown;
    }

    [Serializable]
    public sealed class RaceRecord
    {
        public string date;
        public string mode;
        public string trackId;
        public string difficulty;
        public int laps;
        public int playerPosition;
        public int fieldSize;
        public float playerBestLap;
        public float playerTotal;
    }

    /// <summary>
    /// Local persistence: personal bests, reference laps (for delta timing) and an offline race
    /// history used when the Java backend is unavailable. The backend's H2 database is the primary
    /// store; this is the resilience layer.
    /// </summary>
    public sealed class RecordsStore
    {
        private const string Key = "apex.records.v1";

        [Serializable]
        private sealed class Data
        {
            public List<BestEntry> bests = new List<BestEntry>();
            public List<RaceRecord> history = new List<RaceRecord>();
            public int races, wins, podiums, totalLaps;
        }

        [Serializable]
        private sealed class BestEntry
        {
            public string key;
            public float time;
            public float s1, s2, s3;
            public float[] reference;     // elapsed time every 10 m of the lap
            public string car;
            public string date;
        }

        private Data _data;

        public RecordsStore()
        {
            try
            {
                string json = PlayerPrefs.GetString(Key, string.Empty);
                _data = string.IsNullOrEmpty(json) ? new Data() : JsonUtility.FromJson<Data>(json) ?? new Data();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Records] corrupt local records reset: " + e.Message);
                _data = new Data();
            }
        }

        private void Save()
        {
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(_data));
            PlayerPrefs.Save();
        }

        private static string BestKey(string track, string mode) => track + "|" + mode;

        public float BestLap(string track, string mode, out float[] sectors, out float[] reference)
        {
            var e = _data.bests.Find(b => b.key == BestKey(track, mode));
            sectors = e != null ? new[] { e.s1, e.s2, e.s3 } : null;
            reference = e?.reference;
            return e?.time ?? 0f;
        }

        /// <summary>Returns true if the lap is a new personal best.</summary>
        public bool SubmitLap(string track, string mode, string car, LapData lap, float[] reference)
        {
            if (!lap.valid) return false;
            string k = BestKey(track, mode);
            var e = _data.bests.Find(b => b.key == k);
            if (e != null && e.time > 0f && e.time <= lap.time) return false;
            if (e == null) { e = new BestEntry { key = k }; _data.bests.Add(e); }
            e.time = lap.time; e.s1 = lap.s1; e.s2 = lap.s2; e.s3 = lap.s3;
            e.reference = reference; e.car = car; e.date = DateTime.Now.ToString("yyyy-MM-dd");
            Save();
            return true;
        }

        public void AddRace(RaceRecord r, int laps)
        {
            _data.history.Insert(0, r);
            if (_data.history.Count > 25) _data.history.RemoveAt(_data.history.Count - 1);
            if (r.mode == "QuickRace")
            {
                _data.races++;
                if (r.playerPosition == 1) _data.wins++;
                if (r.playerPosition <= 3) _data.podiums++;
            }
            _data.totalLaps += laps;
            Save();
        }

        public IReadOnlyList<RaceRecord> History => _data.history;
        public int Races => _data.races;
        public int Wins => _data.wins;
        public int Podiums => _data.podiums;
        public int TotalLaps => _data.totalLaps;
    }
}
