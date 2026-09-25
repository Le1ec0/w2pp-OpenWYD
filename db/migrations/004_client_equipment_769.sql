-- WYD CDK server state, migration 004.
-- Keeps the 7.69 eighteen-slot client equipment state versioned and separate
-- from the confirmed 7.59/W2PP account blob (which remains 7945 bytes).

CREATE TABLE IF NOT EXISTS wyd_client_equipment_769 (
    world_key VARCHAR(3) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
    account_name VARCHAR(15) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
    character_slot TINYINT UNSIGNED NOT NULL,
    equipment_blob VARBINARY(144) NOT NULL,
    version BIGINT UNSIGNED NOT NULL DEFAULT 0,
    created_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    updated_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6) ON UPDATE CURRENT_TIMESTAMP(6),
    PRIMARY KEY (world_key, account_name, character_slot),
    KEY ix_wyd_client_equipment_769_account (world_key, account_name),
    CONSTRAINT ck_wyd_client_equipment_769_world CHECK (world_key IN ('UP', 'PVP')),
    CONSTRAINT ck_wyd_client_equipment_769_slot CHECK (character_slot < 4),
    CONSTRAINT ck_wyd_client_equipment_769_size CHECK (OCTET_LENGTH(equipment_blob) = 144)
) ENGINE = InnoDB;

INSERT INTO wyd_schema_migrations (version, name)
VALUES ('004', 'client 7.69 eighteen-slot equipment state')
ON DUPLICATE KEY UPDATE name = VALUES(name), applied_at = applied_at;
