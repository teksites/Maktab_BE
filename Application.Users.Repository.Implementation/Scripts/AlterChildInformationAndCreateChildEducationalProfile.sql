ALTER TABLE `maktab`.`child_information`
ADD COLUMN IF NOT EXISTS `ArabicName` VARCHAR(255) NOT NULL DEFAULT '' AFTER `LastName`;

ALTER TABLE `maktab_dev`.`child_information`
ADD COLUMN IF NOT EXISTS `ArabicName` VARCHAR(255) NOT NULL DEFAULT '' AFTER `LastName`;

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
