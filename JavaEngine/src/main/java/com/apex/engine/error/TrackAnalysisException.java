package com.apex.engine.error;

/** Track data could not be analysed (too few points, non-finite values, degenerate geometry). */
public class TrackAnalysisException extends ApexException {
    public TrackAnalysisException(String message) {
        super("TRACK_ANALYSIS", message);
    }
}
