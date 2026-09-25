-- WYD CDK server state, migration 002.
-- Numeric tokens are opaque six-byte values; never store a PIN as text here.

CREATE TABLE IF NOT EXISTS wyd_account_security (
    account_name VARCHAR(15) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
    numeric_token VARBINARY(6) NOT NULL,
    updated_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6) ON UPDATE CURRENT_TIMESTAMP(6),
    PRIMARY KEY (account_name),
    CONSTRAINT ck_wyd_account_security_token CHECK (OCTET_LENGTH(numeric_token) = 6)
) ENGINE = InnoDB;

INSERT INTO wyd_schema_migrations (version, name)
VALUES ('002', 'account numeric-token state')
ON DUPLICATE KEY UPDATE name = VALUES(name), applied_at = applied_at;
