ALTER TABLE helcim_saved_card
    ADD COLUMN CardFingerprint CHAR(64) NULL AFTER CardTokenHash;

-- Existing token hashes have no stable physical-card identity. They remain usable and will be replaced
-- with a keyed first-six/last-four fingerprint on the card's next verification or saved-card checkout.
SET SQL_SAFE_UPDATES = 0;

UPDATE helcim_saved_card
SET CardFingerprint = CardTokenHash
WHERE CardFingerprint IS NULL OR CardFingerprint = '';

SET SQL_SAFE_UPDATES = 1;

ALTER TABLE helcim_saved_card
    MODIFY COLUMN CardFingerprint CHAR(64) NOT NULL,
    ADD UNIQUE KEY UX_helcim_saved_card_user_fingerprint (UserId, CardFingerprint);
