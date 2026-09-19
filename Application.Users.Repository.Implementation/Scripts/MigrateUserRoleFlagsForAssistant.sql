-- Run once against both maktab and maktab_dev after deploying the UserRoleType Assistant flag.
-- Legacy masks: Normal=1, SchoolTeacher=2, SchoolSupervisor=4, SchoolAdmin=8,
-- SuperUser=16, Manager=32, Admin=64.
-- New masks reserve 2 for Assistant and shift every legacy staff/admin bit left.
-- Take a backup first. Do not run this script a second time.

SET SQL_SAFE_UPDATES = 0;

-- Verify these queries return no rows before continuing. An unknown bit needs review.
SELECT UserId, UserRole
FROM `maktab`.`user_info`
WHERE (UserRole & ~127) <> 0;

SELECT UserId, UserRole
FROM `maktab`.`temp_user_info`
WHERE (UserRole & ~127) <> 0;

SELECT UserId, UserRole
FROM `maktab_dev`.`user_info`
WHERE (UserRole & ~127) <> 0;

SELECT UserId, UserRole
FROM `maktab_dev`.`temp_user_info`
WHERE (UserRole & ~127) <> 0;

ALTER TABLE `maktab`.`user_info`
    MODIFY COLUMN UserRole BIGINT UNSIGNED NOT NULL DEFAULT 1;

ALTER TABLE `maktab`.`temp_user_info`
    MODIFY COLUMN UserRole BIGINT UNSIGNED NOT NULL DEFAULT 1;

ALTER TABLE `maktab_dev`.`user_info`
    MODIFY COLUMN UserRole BIGINT UNSIGNED NOT NULL DEFAULT 1;

ALTER TABLE `maktab_dev`.`temp_user_info`
    MODIFY COLUMN UserRole BIGINT UNSIGNED NOT NULL DEFAULT 1;

START TRANSACTION;

UPDATE `maktab`.`user_info`
SET UserRole = (UserRole & 1) | ((UserRole & 126) << 1)
WHERE UserRole <> 0;

UPDATE `maktab`.`temp_user_info`
SET UserRole = (UserRole & 1) | ((UserRole & 126) << 1)
WHERE UserRole <> 0;

UPDATE `maktab_dev`.`user_info`
SET UserRole = (UserRole & 1) | ((UserRole & 126) << 1)
WHERE UserRole <> 0;

UPDATE `maktab_dev`.`temp_user_info`
SET UserRole = (UserRole & 1) | ((UserRole & 126) << 1)
WHERE UserRole <> 0;

COMMIT;

SET SQL_SAFE_UPDATES = 1;
