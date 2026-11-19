package com.apex.engine.persistence;

import com.apex.engine.error.PersistenceException;
import org.springframework.dao.DataAccessException;
import org.springframework.jdbc.core.JdbcTemplate;
import org.springframework.jdbc.core.RowMapper;

import java.util.List;
import java.util.Optional;
import java.util.function.Supplier;

/**
 * Generic base repository: common finders over a single table, typed by entity and key.
 *
 * @param <T>  entity type
 * @param <ID> primary-key type
 */
public abstract class JdbcRepository<T, ID> {
    protected final JdbcTemplate jdbc;

    protected JdbcRepository(JdbcTemplate jdbc) {
        this.jdbc = jdbc;
    }

    protected abstract String table();

    protected abstract String idColumn();

    protected abstract RowMapper<T> mapper();

    public Optional<T> findById(ID id) {
        return guard(() -> jdbc.query("SELECT * FROM " + table() + " WHERE " + idColumn() + " = ?", mapper(), id).stream().findFirst());
    }

    public List<T> findAll(int limit) {
        return guard(() -> jdbc.query("SELECT * FROM " + table() + " ORDER BY " + idColumn() + " DESC LIMIT ?", mapper(), limit));
    }

    public long count() {
        Long n = guard(() -> jdbc.queryForObject("SELECT COUNT(*) FROM " + table(), Long.class));
        return n == null ? 0 : n;
    }

    /** Translates Spring data-access failures into the engine's exception hierarchy. */
    protected <R> R guard(Supplier<R> action) {
        try {
            return action.get();
        } catch (DataAccessException e) {
            throw new PersistenceException(table() + ": " + e.getMostSpecificCause().getMessage(), e);
        }
    }
}
