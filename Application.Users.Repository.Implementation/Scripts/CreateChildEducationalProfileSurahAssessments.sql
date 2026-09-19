-- Apply before deploying the per-Surah assessment API.
-- Each row holds one Surah's status and remarks. The profile table remains the parent record.

CREATE TABLE IF NOT EXISTS `maktab`.`child_educational_profile_surah` (
    `ChildEducationalProfileSurahId` BINARY(16) NOT NULL,
    `ChildEducationalProfileId` BINARY(16) NOT NULL,
    `Surah` TINYINT UNSIGNED NOT NULL,
    `CompletionStatus` TINYINT UNSIGNED NOT NULL DEFAULT 0,
    `Remarks` VARCHAR(500) NOT NULL DEFAULT '',
    `IsActive` TINYINT(1) NOT NULL DEFAULT 1,
    `CreatedAt` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    `UpdatedOn` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    PRIMARY KEY (`ChildEducationalProfileSurahId`),
    UNIQUE KEY `UX_child_educational_profile_surah_profile_surah` (`ChildEducationalProfileId`, `Surah`),
    KEY `IX_child_educational_profile_surah_profile_active` (`ChildEducationalProfileId`, `IsActive`),
    KEY `IX_child_educational_profile_surah_status` (`CompletionStatus`, `IsActive`),
    CONSTRAINT `FK_child_educational_profile_surah_profile`
        FOREIGN KEY (`ChildEducationalProfileId`)
        REFERENCES `maktab`.`child_educational_profile` (`ChildEducationalProfileId`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

CREATE TABLE IF NOT EXISTS `maktab_dev`.`child_educational_profile_surah` (
    `ChildEducationalProfileSurahId` BINARY(16) NOT NULL,
    `ChildEducationalProfileId` BINARY(16) NOT NULL,
    `Surah` TINYINT UNSIGNED NOT NULL,
    `CompletionStatus` TINYINT UNSIGNED NOT NULL DEFAULT 0,
    `Remarks` VARCHAR(500) NOT NULL DEFAULT '',
    `IsActive` TINYINT(1) NOT NULL DEFAULT 1,
    `CreatedAt` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    `UpdatedOn` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    PRIMARY KEY (`ChildEducationalProfileSurahId`),
    UNIQUE KEY `UX_child_educational_profile_surah_profile_surah` (`ChildEducationalProfileId`, `Surah`),
    KEY `IX_child_educational_profile_surah_profile_active` (`ChildEducationalProfileId`, `IsActive`),
    KEY `IX_child_educational_profile_surah_status` (`CompletionStatus`, `IsActive`),
    CONSTRAINT `FK_child_educational_profile_surah_profile`
        FOREIGN KEY (`ChildEducationalProfileId`)
        REFERENCES `maktab_dev`.`child_educational_profile` (`ChildEducationalProfileId`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
