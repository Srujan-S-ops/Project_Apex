package com.apex.engine.ai;

import com.apex.engine.race.DriverState;
import com.apex.engine.race.RaceSession;
import com.apex.engine.track.Corner;
import com.apex.engine.track.TrackAnalysis;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.beans.factory.annotation.Qualifier;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.stereotype.Service;

import java.util.ArrayList;
import java.util.Comparator;
import java.util.List;
import java.util.concurrent.Callable;
import java.util.concurrent.CancellationException;
import java.util.concurrent.ExecutionException;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Future;
import java.util.concurrent.TimeUnit;
import java.util.concurrent.atomic.AtomicLong;

/**
 * Evaluates every AI driver of a session in parallel (one task per driver on the AI ExecutorService).
 * Each task works on an immutable snapshot; a task that fails or misses the deadline falls back to a
 * neutral RACE command so one bad decision can never stall the race.
 */
@Service
public class AIDecisionEngine {
    private static final Logger LOG = LoggerFactory.getLogger(AIDecisionEngine.class);
    private final ExecutorService executor;
    private final long timeoutMs;
    private final AtomicLong decisions = new AtomicLong();
    private final AtomicLong fallbacks = new AtomicLong();

    public AIDecisionEngine(@Qualifier("aiExecutor") ExecutorService executor,
                            @Value("${apex.ai.decision-timeout-ms:40}") long timeoutMs) {
        this.executor = executor;
        this.timeoutMs = timeoutMs;
    }

    public List<AICommand> decideAll(RaceSession session, TrackAnalysis analysis) {
        List<DriverState> snapshot = session.snapshot();
        snapshot.sort(Comparator.comparingDouble(DriverState::distance).reversed());
        List<Callable<AICommand>> tasks = new ArrayList<>();
        List<Integer> ids = new ArrayList<>();
        for (DriverState d : snapshot) {
            if (d.isPlayer() || d.personality() == null) {
                continue;
            }
            DecisionContext ctx = buildContext(d, snapshot, session, analysis);
            DrivingBehaviour behaviour = d.personality();
            tasks.add(() -> behaviour.decide(ctx));
            ids.add(d.id());
        }
        List<AICommand> out = new ArrayList<>(tasks.size());
        if (tasks.isEmpty()) {
            return out;
        }
        try {
            List<Future<AICommand>> futures = executor.invokeAll(tasks, timeoutMs, TimeUnit.MILLISECONDS);
            for (int i = 0; i < futures.size(); i++) {
                out.add(resolve(futures.get(i), ids.get(i)));
            }
        } catch (InterruptedException e) {
            Thread.currentThread().interrupt();
            ids.forEach(id -> out.add(AICommand.neutral(id)));
        }
        decisions.addAndGet(out.size());
        return out;
    }

    private AICommand resolve(Future<AICommand> f, int id) {
        try {
            AICommand c = f.get();
            return c != null ? c : fallback(id, "null decision");
        } catch (CancellationException e) {
            return fallback(id, "deadline exceeded");
        } catch (ExecutionException e) {
            return fallback(id, e.getCause() == null ? e.getMessage() : e.getCause().toString());
        } catch (InterruptedException e) {
            Thread.currentThread().interrupt();
            return fallback(id, "interrupted");
        }
    }

    private AICommand fallback(int id, String reason) {
        fallbacks.incrementAndGet();
        LOG.debug("AI decision for driver {} fell back: {}", id, reason);
        return AICommand.neutral(id);
    }

    static DecisionContext buildContext(DriverState me, List<DriverState> field, RaceSession s, TrackAnalysis a) {
        DriverState ahead = null, behind = null;
        double gapAhead = Double.POSITIVE_INFINITY, gapBehind = Double.POSITIVE_INFINITY;
        for (DriverState o : field) {
            if (o.id() == me.id()) {
                continue;
            }
            double dd = o.distance() - me.distance();
            if (dd > 0 && dd < gapAhead) {
                gapAhead = dd;
                ahead = o;
            } else if (dd <= 0 && -dd < gapBehind) {
                gapBehind = -dd;
                behind = o;
            }
        }
        double v = Math.max(5.0, me.speed());
        double halfWidth = 6.5;
        int dir = 0;
        double toCorner = Double.POSITIVE_INFINITY;
        com.apex.engine.track.CornerType type = null;
        if (a != null) {
            double centreDistance = me.lapDistance();
            int idx = a.indexAt(centreDistance);
            halfWidth = a.model().width(idx) * 0.5;
            Corner c = a.nextCorner(idx);
            if (c != null) {
                dir = c.direction();
                type = c.type();
                int n = a.model().count();
                toCorner = (((c.start() - idx) % n + n) % n) * a.model().spacing();
            }
        }
        return new DecisionContext(me.id(), me.position(), field.size(), s.raceTime(), me.speed(), me.lateral(), halfWidth,
                me.lap(), s.laps(),
                gapAhead, ahead == null ? Double.POSITIVE_INFINITY : gapAhead / v, ahead == null ? 0 : ahead.lateral(), ahead == null ? 0 : ahead.speed(),
                gapBehind, behind == null ? Double.POSITIVE_INFINITY : gapBehind / Math.max(5.0, behind.speed()), behind == null ? 0 : behind.speed(),
                dir, toCorner, type, s.difficulty());
    }

    public long decisionCount() {
        return decisions.get();
    }

    public long fallbackCount() {
        return fallbacks.get();
    }
}
