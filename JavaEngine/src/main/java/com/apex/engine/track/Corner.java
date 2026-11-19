package com.apex.engine.track;

/** A detected corner: sample indices of entry, apex and exit plus its classification. */
public record Corner(int start, int apex, int end, double minSpeed, int direction, CornerType type) {

    /** Number of samples covered, accounting for wrap-around. */
    public int length(int count) {
        return ((end - start) % count + count) % count;
    }
}
