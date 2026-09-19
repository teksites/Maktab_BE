-- Adds the overall Surah-assessment fields while preserving all existing completed-Surah data.
-- Safe to run repeatedly on MySQL versions that do not support ADD COLUMN IF NOT EXISTS.

SET @sql = IF(
    EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = 'maktab' AND TABLE_NAME = 'child_educational_profile' AND COLUMN_NAME = 'SurahCompletionStatus'),
    'SELECT 1',
    'ALTER TABLE `maktab`.`child_educational_profile` ADD COLUMN `SurahCompletionStatus` TINYINT UNSIGNED NOT NULL DEFAULT 0 AFTER `CompletedSurahsJson`'
);
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

SET @sql = IF(
    EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = 'maktab' AND TABLE_NAME = 'child_educational_profile' AND COLUMN_NAME = 'Remarks'),
    'SELECT 1',
    'ALTER TABLE `maktab`.`child_educational_profile` ADD COLUMN `Remarks` VARCHAR(500) NOT NULL DEFAULT '''' AFTER `SurahCompletionStatus`'
);
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

SET @sql = IF(
    EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = 'maktab_dev' AND TABLE_NAME = 'child_educational_profile' AND COLUMN_NAME = 'SurahCompletionStatus'),
    'SELECT 1',
    'ALTER TABLE `maktab_dev`.`child_educational_profile` ADD COLUMN `SurahCompletionStatus` TINYINT UNSIGNED NOT NULL DEFAULT 0 AFTER `CompletedSurahsJson`'
);
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

SET @sql = IF(
    EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = 'maktab_dev' AND TABLE_NAME = 'child_educational_profile' AND COLUMN_NAME = 'Remarks'),
    'SELECT 1',
    'ALTER TABLE `maktab_dev`.`child_educational_profile` ADD COLUMN `Remarks` VARCHAR(500) NOT NULL DEFAULT '''' AFTER `SurahCompletionStatus`'
);
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;
