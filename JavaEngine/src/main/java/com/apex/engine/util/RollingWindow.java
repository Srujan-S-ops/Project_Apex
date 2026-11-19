package com.apex.engine.util;

import java.util.ArrayDeque;
import java.util.Deque;

/**
 * Thread-safe fixed-capacity window of numeric samples (bounded generic type).
 *
 * @param <T> numeric sample type
 */
public final class RollingWindow<T extends Number> {
    private final int capacity;
    private final Deque<T> samples;

    public RollingWindow(int capacity) {
        if (capacity <= 0) {
            throw new IllegalArgumentException("capacity must be positive");
        }
        this.capacity = capacity;
        this.samples = new ArrayDeque<>(capacity);
    }

    public synchronized void add(T value) {
        if (samples.size() == capacity) {
            samples.removeFirst();
        }
        samples.addLast(value);
    }

    public synchronized double average() {
        if (samples.isEmpty()) {
            return 0.0;
        }
        double sum = 0.0;
        for (T s : samples) {
            sum += s.doubleValue();
        }
        return sum / samples.size();
    }

    public synchronized double max() {
        return samples.stream().mapToDouble(Number::doubleValue).max().orElse(0.0);
    }

    public synchronized int size() {
        return samples.size();
    }

    public synchronized T latest() {
        return samples.peekLast();
    }
}
