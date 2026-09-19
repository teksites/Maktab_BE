-- Run this script once, after deploying the UserType enum that defines Self = 1.
-- Existing values 1, 2, and 3 represented Mother, Father, and Guardian in child_information.
-- The parent/guardian relationship remains unchanged in user_info; only the mirrored child row is normalized to Self.

-- Workbench safe-update mode rejects this migration because UserType is not indexed.
-- This affects only the current database session and is restored after the transaction.
SET SQL_SAFE_UPDATES = 0;

START TRANSACTION;

UPDATE `maktab`.`child_information`
SET `UserType` = 1
WHERE `UserType` IN (1, 2, 3);

UPDATE `maktab_dev`.`child_information`
SET `UserType` = 1
WHERE `UserType` IN (1, 2, 3);

COMMIT;

SET SQL_SAFE_UPDATES = 1;
