-- WYD CDK server state, migration 003.
-- The listing is canonical JSON owned by the server; the account blob remains
-- in wyd_world_account_state and is committed with it for purchases.

CREATE TABLE IF NOT EXISTS wyd_autotrade_state (
    world_key VARCHAR(3) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
    account_name VARCHAR(15) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
    character_slot TINYINT UNSIGNED NOT NULL,
    listing_json LONGTEXT CHARACTER SET utf8mb4 COLLATE utf8mb4_bin NOT NULL,
    created_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    updated_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6) ON UPDATE CURRENT_TIMESTAMP(6),
    PRIMARY KEY (world_key, account_name, character_slot),
    KEY ix_wyd_autotrade_state_world (world_key, account_name),
    CONSTRAINT ck_wyd_autotrade_state_world CHECK (world_key IN ('UP', 'PVP')),
    CONSTRAINT ck_wyd_autotrade_state_slot CHECK (character_slot < 4),
    CONSTRAINT ck_wyd_autotrade_state_json CHECK (JSON_VALID(listing_json))
) ENGINE = InnoDB;

INSERT INTO wyd_schema_migrations (version, name)
VALUES ('003', 'autotrade listing state')
ON DUPLICATE KEY UPDATE name = VALUES(name), applied_at = applied_at;
