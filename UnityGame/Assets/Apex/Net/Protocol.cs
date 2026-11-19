using System;

// Wire format shared with the Java backend (com.apex.engine.net.dto). Field names are the contract.
namespace Apex.Net
{
    public static class MsgType
    {
        public const string Hello = "hello", Welcome = "welcome", Ping = "ping", Pong = "pong";
        public const string TrackData = "trackData", TrackAnalysis = "trackAnalysis";
        public const string SessionStart = "sessionStart", Telemetry = "telemetry", AiCommands = "aiCommands", Standings = "standings";
        public const string LapCompleted = "lapCompleted", RaceFinished = "raceFinished", RaceSaved = "raceSaved";
        public const string GetProfile = "getProfile", Profile = "profile", Error = "error";
    }

    [Serializable] public sealed class Envelope { public string type; public long seq; public string payload; }
    [Serializable] public sealed class HelloMsg { public string clientVersion; }
    [Serializable] public sealed class WelcomeMsg { public string serverVersion; public string connectionId; }
    [Serializable] public sealed class PingMsg { public double t; }

    [Serializable]
    public sealed class TrackDataMsg
    {
        public string trackId;
        public float[] x, y, z, width;
        public float mu, massKg, downforceCoef, maxAccel, maxBrake, topSpeed;
    }

    [Serializable] public sealed class CornerDto { public int start, apex, end; public float minSpeed; public string type; }

    [Serializable]
    public sealed class TrackAnalysisMsg
    {
        public string trackId;
        public float[] lineOffset, speed, curvature;
        public CornerDto[] corners;
        public int[] brakingZones;
    }

    [Serializable] public sealed class DriverDto { public int id; public string name; public string personality; public bool isPlayer; public string car; }

    [Serializable]
    public sealed class SessionStartMsg
    {
        public string sessionId, mode, difficulty, trackId;
        public int laps;
        public float trackLength;
        public DriverDto[] drivers;
    }

    [Serializable] public sealed class CarTelemetryDto { public int id; public float distance, lapDistance, lateral, speed; public int lap, position; }
    [Serializable] public sealed class TelemetryMsg { public string sessionId; public float raceTime; public CarTelemetryDto[] cars; }
    [Serializable] public sealed class AICommandDto { public int id; public string mode; public float speedScale, lateralTarget, aggression; }
    [Serializable] public sealed class AICommandsMsg { public string sessionId; public AICommandDto[] commands; }
    [Serializable] public sealed class StandingDto { public int id, position, lap; public float gapToLeader, interval, bestLap; }
    [Serializable] public sealed class StandingsMsg { public string sessionId; public StandingDto[] entries; }

    [Serializable]
    public sealed class LapCompletedMsg
    {
        public string sessionId;
        public int driverId, lap;
        public float lapTime, s1, s2, s3;
        public bool valid;
    }

    [Serializable]
    public sealed class ResultDto
    {
        public int driverId;
        public string name;
        public bool isPlayer;
        public int position;
        public float totalTime, bestLap;
        public int laps;
        public bool dnf;
    }

    [Serializable]
    public sealed class RaceFinishedMsg
    {
        public string sessionId, mode, trackId, difficulty, car;
        public int laps;
        public ResultDto[] results;
    }

    [Serializable] public sealed class RaceSavedMsg { public long raceId; public bool newPersonalBest; public float personalBest; }
    [Serializable] public sealed class GetProfileMsg { public string trackId; }

    [Serializable]
    public sealed class RecentRaceDto
    {
        public long raceId;
        public string date, mode, trackId, difficulty;
        public int position, fieldSize;
        public float bestLap, totalTime;
    }

    [Serializable] public sealed class LapRecordDto { public string driver, car, mode, date; public float lapTime; public bool isPlayer; }

    [Serializable]
    public sealed class ProfileMsg
    {
        public int races, wins, podiums, totalLaps;
        public float bestLap;
        public RecentRaceDto[] recent;
        public LapRecordDto[] records;
    }

    [Serializable] public sealed class ErrorMsg { public string code, message; }
}
