-- WYD CDK server state, migration 001.
-- The Site-owned `accounts` table is intentionally not created here.

CREATE TABLE IF NOT EXISTS wyd_schema_migrations (
    version VARCHAR(32) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
    name VARCHAR(128) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
    applied_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    PRIMARY KEY (version)
) ENGINE = InnoDB;

CREATE TABLE IF NOT EXISTS wyd_world_account_state (
    account_name VARCHAR(15) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
    world_key VARCHAR(3) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
    account_blob BLOB NOT NULL,
    version BIGINT UNSIGNED NOT NULL DEFAULT 0,
    created_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    updated_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6) ON UPDATE CURRENT_TIMESTAMP(6),
    PRIMARY KEY (account_name, world_key),
    KEY ix_wyd_world_account_state_world (world_key, account_name),
    CONSTRAINT ck_wyd_world_account_state_world CHECK (world_key IN ('UP', 'PVP')),
    CONSTRAINT ck_wyd_world_account_state_blob CHECK (OCTET_LENGTH(account_blob) = 7945)
) ENGINE = InnoDB;

CREATE TABLE IF NOT EXISTS wyd_character_names (
    world_key VARCHAR(3) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
    character_name VARCHAR(15) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
    account_name VARCHAR(15) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
    slot_index TINYINT UNSIGNED NULL,
    created_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    updated_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6) ON UPDATE CURRENT_TIMESTAMP(6),
    PRIMARY KEY (world_key, character_name),
    KEY ix_wyd_character_names_account (world_key, account_name, slot_index),
    CONSTRAINT ck_wyd_character_names_world CHECK (world_key IN ('UP', 'PVP')),
    CONSTRAINT ck_wyd_character_names_slot CHECK (slot_index IS NULL OR slot_index < 4)
) ENGINE = InnoDB;

INSERT INTO wyd_schema_migrations (version, name)
VALUES ('001', 'world state and character-name reservations')
ON DUPLICATE KEY UPDATE name = VALUES(name), applied_at = applied_at;
