-- Run once, after MigrateUserRoleFlagsForAssistant.sql has completed successfully.
-- Current masks: SuperUser=32, Manager=64, Admin=128.
-- Target masks: Admin=32, SuperUser=64; Manager is removed.
-- Existing Admin users are promoted to SuperUser.
-- Existing SuperUser and Manager users become SchoolAdmin.
-- Take a backup first. Do not run this script a second time.

SET SQL_SAFE_UPDATES = 0;

-- Verify these queries return no rows before continuing. An unknown bit needs review.
SELECT UserId, UserRole
FROM `maktab`.`user_info`
WHERE (UserRole & ~255) <> 0;

SELECT UserId, UserRole
FROM `maktab`.`temp_user_info`
WHERE (UserRole & ~255) <> 0;

SELECT UserId, UserRole
FROM `maktab_dev`.`user_info`
WHERE (UserRole & ~255) <> 0;

SELECT UserId, UserRole
FROM `maktab_dev`.`temp_user_info`
WHERE (UserRole & ~255) <> 0;

START TRANSACTION;

UPDATE `maktab`.`user_info`
SET UserRole =
    (UserRole & 31)
    | CASE WHEN (UserRole & 96) <> 0 THEN 16 ELSE 0 END
    | CASE WHEN (UserRole & 128) <> 0 THEN 64 ELSE 0 END
WHERE (UserRole & 224) <> 0;

UPDATE `maktab`.`temp_user_info`
SET UserRole =
    (UserRole & 31)
    | CASE WHEN (UserRole & 96) <> 0 THEN 16 ELSE 0 END
    | CASE WHEN (UserRole & 128) <> 0 THEN 64 ELSE 0 END
WHERE (UserRole & 224) <> 0;

UPDATE `maktab_dev`.`user_info`
SET UserRole =
    (UserRole & 31)
    | CASE WHEN (UserRole & 96) <> 0 THEN 16 ELSE 0 END
    | CASE WHEN (UserRole & 128) <> 0 THEN 64 ELSE 0 END
WHERE (UserRole & 224) <> 0;

UPDATE `maktab_dev`.`temp_user_info`
SET UserRole =
    (UserRole & 31)
    | CASE WHEN (UserRole & 96) <> 0 THEN 16 ELSE 0 END
    | CASE WHEN (UserRole & 128) <> 0 THEN 64 ELSE 0 END
WHERE (UserRole & 224) <> 0;

COMMIT;

SET SQL_SAFE_UPDATES = 1;
