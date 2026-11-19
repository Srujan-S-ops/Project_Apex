package com.apex.engine.persistence;

import com.apex.engine.persistence.Entities.LapRecordEntity;
import com.apex.engine.persistence.Entities.PlayerStatsEntity;
import com.apex.engine.persistence.Entities.RaceEntity;
import com.apex.engine.persistence.Entities.RaceResultEntity;
import org.springframework.jdbc.core.JdbcTemplate;
import org.springframework.jdbc.core.RowMapper;
import org.springframework.jdbc.support.GeneratedKeyHolder;
import org.springframework.jdbc.support.KeyHolder;
import org.springframework.stereotype.Repository;

import java.sql.PreparedStatement;
import java.sql.ResultSet;
import java.sql.SQLException;
import java.sql.Statement;
import java.sql.Timestamp;
import java.util.List;
import java.util.Optional;

/** Concrete repositories (each extends the generic JdbcRepository). */
public final class Repositories {
    private Repositories() {
    }

    static Double nullableDouble(ResultSet rs, String col) throws SQLException {
        double v = rs.getDouble(col);
        return rs.wasNull() ? null : v;
    }

    static java.time.LocalDateTime ts(ResultSet rs, String col) throws SQLException {
        Timestamp t = rs.getTimestamp(col);
        return t == null ? null : t.toLocalDateTime();
    }

    @Repository
    public static class RaceRepository extends JdbcRepository<RaceEntity, Long> {
        private static final RowMapper<RaceEntity> MAPPER = (rs, i) -> new RaceEntity(
                rs.getLong("id"), rs.getString("session_id"), rs.getString("mode"), rs.getString("track_id"),
                rs.getString("difficulty"), rs.getString("car"), rs.getInt("laps"), rs.getInt("field_size"),
                rs.getInt("player_position"), nullableDouble(rs, "player_best_lap"), nullableDouble(rs, "player_total"),
                ts(rs, "created_at"));

        public RaceRepository(JdbcTemplate jdbc) {
            super(jdbc);
        }

        @Override protected String table() { return "race"; }
        @Override protected String idColumn() { return "id"; }
        @Override protected RowMapper<RaceEntity> mapper() { return MAPPER; }

        public long insert(RaceEntity r) {
            return guard(() -> {
                KeyHolder kh = new GeneratedKeyHolder();
                jdbc.update(con -> {
                    PreparedStatement ps = con.prepareStatement(
                            "INSERT INTO race(session_id, mode, track_id, difficulty, car, laps, field_size, player_position, player_best_lap, player_total) VALUES (?,?,?,?,?,?,?,?,?,?)",
                            Statement.RETURN_GENERATED_KEYS);
                    ps.setString(1, r.sessionId());
                    ps.setString(2, r.mode());
                    ps.setString(3, r.trackId());
                    ps.setString(4, r.difficulty());
                    ps.setString(5, r.car());
                    ps.setInt(6, r.laps());
                    ps.setInt(7, r.fieldSize());
                    ps.setInt(8, r.playerPosition());
                    ps.setObject(9, r.playerBestLap());
                    ps.setObject(10, r.playerTotal());
                    return ps;
                }, kh);
                Number key = kh.getKeys() != null && kh.getKeys().get("ID") != null ? (Number) kh.getKeys().get("ID") : kh.getKey();
                return key.longValue();
            });
        }

        public Optional<RaceEntity> findBySession(String sessionId) {
            return guard(() -> jdbc.query("SELECT * FROM race WHERE session_id = ?", MAPPER, sessionId).stream().findFirst());
        }

        public List<RaceEntity> recent(int limit) {
            return guard(() -> jdbc.query("SELECT * FROM race ORDER BY created_at DESC, id DESC LIMIT ?", MAPPER, limit));
        }
    }

    @Repository
    public static class RaceResultRepository extends JdbcRepository<RaceResultEntity, Long> {
        private static final RowMapper<RaceResultEntity> MAPPER = (rs, i) -> new RaceResultEntity(
                rs.getLong("id"), rs.getLong("race_id"), rs.getInt("driver_id"), rs.getString("name"), rs.getBoolean("is_player"),
                rs.getInt("position"), nullableDouble(rs, "total_time"), nullableDouble(rs, "best_lap"), rs.getInt("laps"), rs.getBoolean("dnf"));

        public RaceResultRepository(JdbcTemplate jdbc) {
            super(jdbc);
        }

        @Override protected String table() { return "race_result"; }
        @Override protected String idColumn() { return "id"; }
        @Override protected RowMapper<RaceResultEntity> mapper() { return MAPPER; }

        public void insert(RaceResultEntity r) {
            guard(() -> jdbc.update("INSERT INTO race_result(race_id, driver_id, name, is_player, position, total_time, best_lap, laps, dnf) VALUES (?,?,?,?,?,?,?,?,?)",
                    r.raceId(), r.driverId(), r.name(), r.isPlayer(), r.position(), r.totalTime(), r.bestLap(), r.laps(), r.dnf()));
        }

        public List<RaceResultEntity> forRace(long raceId) {
            return guard(() -> jdbc.query("SELECT * FROM race_result WHERE race_id = ? ORDER BY position", MAPPER, raceId));
        }
    }

    @Repository
    public static class LapRecordRepository extends JdbcRepository<LapRecordEntity, Long> {
        private static final RowMapper<LapRecordEntity> MAPPER = (rs, i) -> new LapRecordEntity(
                rs.getLong("id"), (Long) rs.getObject("race_id"), rs.getString("session_id"), rs.getString("track_id"), rs.getString("mode"),
                rs.getString("driver_name"), rs.getString("car"), rs.getBoolean("is_player"), rs.getInt("lap"), rs.getDouble("lap_time"),
                nullableDouble(rs, "s1"), nullableDouble(rs, "s2"), nullableDouble(rs, "s3"), rs.getBoolean("valid"), ts(rs, "created_at"));

        public LapRecordRepository(JdbcTemplate jdbc) {
            super(jdbc);
        }

        @Override protected String table() { return "lap_record"; }
        @Override protected String idColumn() { return "id"; }
        @Override protected RowMapper<LapRecordEntity> mapper() { return MAPPER; }

        public void insert(LapRecordEntity l) {
            guard(() -> jdbc.update("INSERT INTO lap_record(race_id, session_id, track_id, mode, driver_name, car, is_player, lap, lap_time, s1, s2, s3, valid) VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?)",
                    l.raceId(), l.sessionId(), l.trackId(), l.mode(), l.driverName(), l.car(), l.isPlayer(), l.lap(), l.lapTime(), l.s1(), l.s2(), l.s3(), l.valid()));
        }

        public int linkSession(String sessionId, long raceId) {
            return guard(() -> jdbc.update("UPDATE lap_record SET race_id = ? WHERE session_id = ?", raceId, sessionId));
        }

        public List<LapRecordEntity> fastest(String trackId, int limit) {
            return guard(() -> jdbc.query("SELECT * FROM lap_record WHERE track_id = ? AND valid = TRUE ORDER BY lap_time ASC LIMIT ?", MAPPER, trackId, limit));
        }

        public Optional<Double> playerBest(String trackId, String mode) {
            return guard(() -> {
                Double v = mode == null
                        ? jdbc.queryForObject("SELECT MIN(lap_time) FROM lap_record WHERE track_id = ? AND is_player = TRUE AND valid = TRUE", Double.class, trackId)
                        : jdbc.queryForObject("SELECT MIN(lap_time) FROM lap_record WHERE track_id = ? AND mode = ? AND is_player = TRUE AND valid = TRUE", Double.class, trackId, mode);
                return Optional.ofNullable(v);
            });
        }

        public long countPlayerLaps() {
            Long n = guard(() -> jdbc.queryForObject("SELECT COUNT(*) FROM lap_record WHERE is_player = TRUE", Long.class));
            return n == null ? 0 : n;
        }
    }

    @Repository
    public static class PlayerStatsRepository extends JdbcRepository<PlayerStatsEntity, String> {
        public static final String PLAYER = "PLAYER";
        private static final RowMapper<PlayerStatsEntity> MAPPER = (rs, i) -> new PlayerStatsEntity(
                rs.getString("player_id"), rs.getInt("races"), rs.getInt("wins"), rs.getInt("podiums"), rs.getInt("total_laps"), nullableDouble(rs, "best_lap"));

        public PlayerStatsRepository(JdbcTemplate jdbc) {
            super(jdbc);
        }

        @Override protected String table() { return "player_stats"; }
        @Override protected String idColumn() { return "player_id"; }
        @Override protected RowMapper<PlayerStatsEntity> mapper() { return MAPPER; }

        public PlayerStatsEntity get() {
            return findById(PLAYER).orElse(new PlayerStatsEntity(PLAYER, 0, 0, 0, 0, null));
        }

        public void save(PlayerStatsEntity s) {
            guard(() -> jdbc.update("MERGE INTO player_stats(player_id, races, wins, podiums, total_laps, best_lap, updated_at) KEY(player_id) VALUES (?,?,?,?,?,?,CURRENT_TIMESTAMP)",
                    s.playerId(), s.races(), s.wins(), s.podiums(), s.totalLaps(), s.bestLap()));
        }
    }
}
