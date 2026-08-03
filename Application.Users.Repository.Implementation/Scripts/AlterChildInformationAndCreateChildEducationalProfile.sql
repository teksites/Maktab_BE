SET @sql = IF (
    EXISTS (
        SELECT 1
        FROM INFORMATION_SCHEMA.COLUMNS
        WHERE TABLE_SCHEMA = 'maktab'
          AND TABLE_NAME = 'child_information'
          AND COLUMN_NAME = 'ArabicName'
    ),
    'SELECT 1',
    'ALTER TABLE `maktab`.`child_information` ADD COLUMN `ArabicName` VARCHAR(255) NOT NULL DEFAULT '''' AFTER `LastName`'
);
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

SET @sql = IF (
    EXISTS (
        SELECT 1
        FROM INFORMATION_SCHEMA.COLUMNS
        WHERE TABLE_SCHEMA = 'maktab_dev'
          AND TABLE_NAME = 'child_information'
          AND COLUMN_NAME = 'ArabicName'
    ),
    'SELECT 1',
    'ALTER TABLE `maktab_dev`.`child_information` ADD COLUMN `ArabicName` VARCHAR(255) NOT NULL DEFAULT '''' AFTER `LastName`'
);
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

SET @sql = IF (
    EXISTS (
        SELECT 1
        FROM INFORMATION_SCHEMA.COLUMNS
        WHERE TABLE_SCHEMA = 'maktab'
          AND TABLE_NAME = 'child_information'
          AND COLUMN_NAME = 'HasSurahCatalogBeenProvided'
    ),
    'SELECT 1',
    'ALTER TABLE `maktab`.`child_information` ADD COLUMN `HasSurahCatalogBeenProvided` BIT NOT NULL DEFAULT b''0'' AFTER `ArabicName`'
);
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

SET @sql = IF (
    EXISTS (
        SELECT 1
        FROM INFORMATION_SCHEMA.COLUMNS
        WHERE TABLE_SCHEMA = 'maktab_dev'
          AND TABLE_NAME = 'child_information'
          AND COLUMN_NAME = 'HasSurahCatalogBeenProvided'
    ),
    'SELECT 1',
    'ALTER TABLE `maktab_dev`.`child_information` ADD COLUMN `HasSurahCatalogBeenProvided` BIT NOT NULL DEFAULT b''0'' AFTER `ArabicName`'
);
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

CREATE TABLE IF NOT EXISTS `maktab`.`child_educational_profile` (
    `ChildEducationalProfileId` BINARY(16) NOT NULL,
    `ChildId` BINARY(16) NOT NULL,
    `FamilyId` BINARY(16) NOT NULL,
    `CompletedSurahsJson` LONGTEXT NOT NULL,
    `IsActive` BIT NOT NULL DEFAULT b'1',
    `CreatedAt` DATETIME NOT NULL,
    `UpdatedOn` DATETIME NOT NULL,
    PRIMARY KEY (`ChildEducationalProfileId`),
    UNIQUE KEY `uq_child_educational_profile_child` (`ChildId`),
    KEY `idx_child_educational_profile_family_active` (`FamilyId`, `IsActive`),
    KEY `idx_child_educational_profile_updated_on` (`UpdatedOn`)
);

CREATE TABLE IF NOT EXISTS `maktab_dev`.`child_educational_profile` (
    `ChildEducationalProfileId` BINARY(16) NOT NULL,
    `ChildId` BINARY(16) NOT NULL,
    `FamilyId` BINARY(16) NOT NULL,
    `CompletedSurahsJson` LONGTEXT NOT NULL,
    `IsActive` BIT NOT NULL DEFAULT b'1',
    `CreatedAt` DATETIME NOT NULL,
    `UpdatedOn` DATETIME NOT NULL,
    PRIMARY KEY (`ChildEducationalProfileId`),
    UNIQUE KEY `uq_child_educational_profile_child` (`ChildId`),
    KEY `idx_child_educational_profile_family_active` (`FamilyId`, `IsActive`),
    KEY `idx_child_educational_profile_updated_on` (`UpdatedOn`)
);
