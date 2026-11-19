package com.apex.engine.ai;

/** Strategy interface for anything able to make a tactical decision. */
public interface DrivingBehaviour {
    AICommand decide(DecisionContext context);

    String name();
}
