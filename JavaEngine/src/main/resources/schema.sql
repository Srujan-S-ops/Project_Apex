CREATE TABLE IF NOT EXISTS race (
    id              BIGINT AUTO_INCREMENT PRIMARY KEY,
    session_id      VARCHAR(64)  NOT NULL UNIQUE,
    mode            VARCHAR(24)  NOT NULL,
    track_id        VARCHAR(64)  NOT NULL,
    difficulty      VARCHAR(16)  NOT NULL,
    car             VARCHAR(64),
    laps            INT          NOT NULL,
    field_size      INT          NOT NULL,
    player_position INT          NOT NULL,
    player_best_lap DOUBLE,
    player_total    DOUBLE,
    created_at      TIMESTAMP    NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE IF NOT EXISTS race_result (
    id          BIGINT AUTO_INCREMENT PRIMARY KEY,
    race_id     BIGINT      NOT NULL REFERENCES race(id) ON DELETE CASCADE,
    driver_id   INT         NOT NULL,
    name        VARCHAR(64) NOT NULL,
    is_player   BOOLEAN     NOT NULL,
    position    INT         NOT NULL,
    total_time  DOUBLE,
    best_lap    DOUBLE,
    laps        INT         NOT NULL,
    dnf         BOOLEAN     NOT NULL
);

CREATE TABLE IF NOT EXISTS lap_record (
    id          BIGINT AUTO_INCREMENT PRIMARY KEY,
    race_id     BIGINT REFERENCES race(id) ON DELETE SET NULL,
    session_id  VARCHAR(64) NOT NULL,
    track_id    VARCHAR(64) NOT NULL,
    mode        VARCHAR(24) NOT NULL,
    driver_name VARCHAR(64) NOT NULL,
    car         VARCHAR(64),
    is_player   BOOLEAN     NOT NULL,
    lap         INT         NOT NULL,
    lap_time    DOUBLE      NOT NULL,
    s1          DOUBLE,
    s2          DOUBLE,
    s3          DOUBLE,
    valid       BOOLEAN     NOT NULL,
    created_at  TIMESTAMP   NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX IF NOT EXISTS idx_lap_track ON lap_record(track_id, valid, lap_time);

CREATE TABLE IF NOT EXISTS player_stats (
    player_id   VARCHAR(32) PRIMARY KEY,
    races       INT NOT NULL DEFAULT 0,
    wins        INT NOT NULL DEFAULT 0,
    podiums     INT NOT NULL DEFAULT 0,
    total_laps  INT NOT NULL DEFAULT 0,
    best_lap    DOUBLE,
    updated_at  TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
);
