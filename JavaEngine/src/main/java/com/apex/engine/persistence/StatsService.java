package com.apex.engine.persistence;

import com.apex.engine.net.dto.Dtos;
import com.apex.engine.persistence.Entities.LapRecordEntity;
import com.apex.engine.persistence.Entities.PlayerStatsEntity;
import com.apex.engine.persistence.Entities.RaceEntity;
import com.apex.engine.persistence.Entities.RaceResultEntity;
import com.apex.engine.persistence.Repositories.LapRecordRepository;
import com.apex.engine.persistence.Repositories.PlayerStatsRepository;
import com.apex.engine.persistence.Repositories.RaceRepository;
import com.apex.engine.persistence.Repositories.RaceResultRepository;
import com.apex.engine.race.LapRecord;
import com.apex.engine.race.RaceSession;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

import java.time.format.DateTimeFormatter;
import java.util.List;
import java.util.Optional;

/** Race history, lap records, fastest laps, classifications and player statistics. */
@Service
public class StatsService {
    private static final Logger LOG = LoggerFactory.getLogger(StatsService.class);
    private static final DateTimeFormatter DATE = DateTimeFormatter.ofPattern("yyyy-MM-dd HH:mm");

    private final RaceRepository races;
    private final RaceResultRepository results;
    private final LapRecordRepository laps;
    private final PlayerStatsRepository stats;

    public StatsService(RaceRepository races, RaceResultRepository results, LapRecordRepository laps, PlayerStatsRepository stats) {
        this.races = races;
        this.results = results;
        this.laps = laps;
        this.stats = stats;
    }

    /** Persists one lap as soon as it is completed (survives a crash before the race ends). */
    public void saveLap(RaceSession session, LapRecord lap) {
        var driver = session.driver(lap.driverId());
        String name = driver != null ? driver.name() : "DRIVER " + lap.driverId();
        String car = driver != null ? driver.car() : null;
        boolean player = driver != null && driver.isPlayer();
        laps.insert(new LapRecordEntity(0, null, session.id(), session.trackId(), session.mode(), name, car, player,
                lap.lap(), lap.lapTime(), lap.s1(), lap.s2(), lap.s3(), lap.valid(), null));
    }

    /**
     * Stores the final classification. Idempotent per session id: clients resend results after a
     * reconnection, duplicates return the existing race id.
     */
    @Transactional
    public Dtos.RaceSaved saveRace(Dtos.RaceFinished f) {
        Optional<RaceEntity> existing = races.findBySession(f.sessionId());
        double previousBest = laps.playerBest(f.trackId(), f.mode()).orElse(0.0);
        if (existing.isPresent()) {
            LOG.info("Race {} already stored as #{}", f.sessionId(), existing.get().id());
            return new Dtos.RaceSaved(existing.get().id(), false, (float) previousBest);
        }
        List<Dtos.ResultDto> rs = f.results() == null ? List.of() : f.results();
        Dtos.ResultDto player = rs.stream().filter(Dtos.ResultDto::isPlayer).findFirst().orElse(null);
        long raceId = races.insert(new RaceEntity(0, f.sessionId(), f.mode(), f.trackId(), f.difficulty(), f.car(), f.laps(), rs.size(),
                player != null ? player.position() : 0,
                player != null && player.bestLap() > 0 ? (double) player.bestLap() : null,
                player != null ? (double) player.totalTime() : null, null));
        for (Dtos.ResultDto r : rs) {
            results.insert(new RaceResultEntity(0, raceId, r.driverId(), r.name(), r.isPlayer(), r.position(),
                    (double) r.totalTime(), r.bestLap() > 0 ? (double) r.bestLap() : null, r.laps(), r.dnf()));
        }
        int linked = laps.linkSession(f.sessionId(), raceId);

        PlayerStatsEntity s = stats.get();
        boolean race = "QuickRace".equalsIgnoreCase(f.mode());
        int pos = player != null ? player.position() : 0;
        double best = laps.playerBest(f.trackId(), null).orElse(0.0);
        stats.save(new PlayerStatsEntity(PlayerStatsRepository.PLAYER,
                s.races() + (race ? 1 : 0),
                s.wins() + (race && pos == 1 ? 1 : 0),
                s.podiums() + (race && pos >= 1 && pos <= 3 ? 1 : 0),
                (int) laps.countPlayerLaps(),
                best > 0 ? best : null));
        double newBest = laps.playerBest(f.trackId(), f.mode()).orElse(0.0);
        boolean pb = newBest > 0 && (previousBest <= 0 || newBest < previousBest - 1e-6);
        LOG.info("Stored race #{} ({} {} results, {} laps linked, PB {})", raceId, f.mode(), rs.size(), linked, pb);
        return new Dtos.RaceSaved(raceId, pb, (float) newBest);
    }

    public Dtos.Profile profile(String trackId) {
        PlayerStatsEntity s = stats.get();
        String track = trackId == null ? "harbour_gp" : trackId;
        double best = laps.playerBest(track, null).orElse(0.0);
        List<Dtos.RecentRaceDto> recent = races.recent(5).stream()
                .map(r -> new Dtos.RecentRaceDto(r.id(), r.createdAt() == null ? "" : r.createdAt().format(DATE), r.mode(), r.trackId(),
                        r.difficulty(), r.playerPosition(), r.fieldSize(),
                        r.playerBestLap() == null ? 0f : r.playerBestLap().floatValue(),
                        r.playerTotal() == null ? 0f : r.playerTotal().floatValue()))
                .toList();
        List<Dtos.LapRecordDto> records = laps.fastest(track, 10).stream()
                .map(l -> new Dtos.LapRecordDto(l.driverName(), l.car(), l.mode(), l.createdAt() == null ? "" : l.createdAt().format(DATE),
                        (float) l.lapTime(), l.isPlayer()))
                .toList();
        return new Dtos.Profile(s.races(), s.wins(), s.podiums(), s.totalLaps(), (float) best, recent, records);
    }
}
