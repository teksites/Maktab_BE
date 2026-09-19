-- Existing-schema migration: results are stored once per child/course, not per enrollment.
-- Temporarily disable safe updates because legacy rows may need deduplication and backfill.
SET @previous_sql_safe_updates := @@SQL_SAFE_UPDATES;
SET SQL_SAFE_UPDATES = 0;

DELETE older
FROM `maktab`.`student_course_results` older
INNER JOIN `maktab`.`student_course_results` newer
    ON older.`ChildId` = newer.`ChildId`
   AND older.`CourseId` = newer.`CourseId`
   AND (
        older.`UpdatedOn` < newer.`UpdatedOn`
        OR (older.`UpdatedOn` = newer.`UpdatedOn` AND older.`CreatedAt` < newer.`CreatedAt`)
        OR (older.`UpdatedOn` = newer.`UpdatedOn` AND older.`CreatedAt` = newer.`CreatedAt`
            AND older.`StudentCourseResultId` < newer.`StudentCourseResultId`)
   );

SET @sql = IF (
    EXISTS (
        SELECT 1
        FROM INFORMATION_SCHEMA.STATISTICS
        WHERE TABLE_SCHEMA = 'maktab'
          AND TABLE_NAME = 'student_course_results'
          AND INDEX_NAME = 'uq_student_course_results_enrollment'
    ),
    'ALTER TABLE `maktab`.`student_course_results` DROP INDEX `uq_student_course_results_enrollment`',
    'SELECT 1'
);
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

SET @sql = IF (
    EXISTS (
        SELECT 1
        FROM INFORMATION_SCHEMA.STATISTICS
        WHERE TABLE_SCHEMA = 'maktab'
          AND TABLE_NAME = 'student_course_results'
          AND INDEX_NAME = 'idx_student_course_results_group_active'
    ),
    'ALTER TABLE `maktab`.`student_course_results` DROP INDEX `idx_student_course_results_group_active`',
    'SELECT 1'
);
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

SET @sql = IF (
    EXISTS (
        SELECT 1
        FROM INFORMATION_SCHEMA.COLUMNS
        WHERE TABLE_SCHEMA = 'maktab'
          AND TABLE_NAME = 'student_course_results'
          AND COLUMN_NAME = 'StudentCourseEnrollmentId'
    ),
    'ALTER TABLE `maktab`.`student_course_results` DROP COLUMN `StudentCourseEnrollmentId`',
    'SELECT 1'
);
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

SET @sql = IF (
    EXISTS (
        SELECT 1
        FROM INFORMATION_SCHEMA.COLUMNS
        WHERE TABLE_SCHEMA = 'maktab'
          AND TABLE_NAME = 'student_course_results'
          AND COLUMN_NAME = 'CourseEnrollmentGroupId'
    ),
    'ALTER TABLE `maktab`.`student_course_results` DROP COLUMN `CourseEnrollmentGroupId`',
    'SELECT 1'
);
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

UPDATE `maktab`.`student_course_results`
SET `AttendancePercentage` = 100.00
WHERE `AttendancePercentage` IS NULL;

ALTER TABLE `maktab`.`student_course_results`
    MODIFY COLUMN `AttendancePercentage` DECIMAL(5,2) NOT NULL DEFAULT 100.00;

SET @sql = IF (
    EXISTS (
        SELECT 1
        FROM INFORMATION_SCHEMA.STATISTICS
        WHERE TABLE_SCHEMA = 'maktab'
          AND TABLE_NAME = 'student_course_results'
          AND INDEX_NAME = 'uq_student_course_results_child_course'
    ),
    'SELECT 1',
    'ALTER TABLE `maktab`.`student_course_results` ADD UNIQUE KEY `uq_student_course_results_child_course` (`ChildId`, `CourseId`)'
);
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

SET @sql = IF (
    EXISTS (
        SELECT 1
        FROM INFORMATION_SCHEMA.STATISTICS
        WHERE TABLE_SCHEMA = 'maktab'
          AND TABLE_NAME = 'student_course_results'
          AND INDEX_NAME = 'idx_student_course_results_course_active'
    ),
    'SELECT 1',
    'ALTER TABLE `maktab`.`student_course_results` ADD KEY `idx_student_course_results_course_active` (`CourseId`, `IsActive`)'
);
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

DELETE older
FROM `maktab_dev`.`student_course_results` older
INNER JOIN `maktab_dev`.`student_course_results` newer
    ON older.`ChildId` = newer.`ChildId`
   AND older.`CourseId` = newer.`CourseId`
   AND (
        older.`UpdatedOn` < newer.`UpdatedOn`
        OR (older.`UpdatedOn` = newer.`UpdatedOn` AND older.`CreatedAt` < newer.`CreatedAt`)
        OR (older.`UpdatedOn` = newer.`UpdatedOn` AND older.`CreatedAt` = newer.`CreatedAt`
            AND older.`StudentCourseResultId` < newer.`StudentCourseResultId`)
   );

SET @sql = IF (
    EXISTS (
        SELECT 1
        FROM INFORMATION_SCHEMA.STATISTICS
        WHERE TABLE_SCHEMA = 'maktab_dev'
          AND TABLE_NAME = 'student_course_results'
          AND INDEX_NAME = 'uq_student_course_results_enrollment'
    ),
    'ALTER TABLE `maktab_dev`.`student_course_results` DROP INDEX `uq_student_course_results_enrollment`',
    'SELECT 1'
);
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

SET @sql = IF (
    EXISTS (
        SELECT 1
        FROM INFORMATION_SCHEMA.STATISTICS
        WHERE TABLE_SCHEMA = 'maktab_dev'
          AND TABLE_NAME = 'student_course_results'
          AND INDEX_NAME = 'idx_student_course_results_group_active'
    ),
    'ALTER TABLE `maktab_dev`.`student_course_results` DROP INDEX `idx_student_course_results_group_active`',
    'SELECT 1'
);
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

SET @sql = IF (
    EXISTS (
        SELECT 1
        FROM INFORMATION_SCHEMA.COLUMNS
        WHERE TABLE_SCHEMA = 'maktab_dev'
          AND TABLE_NAME = 'student_course_results'
          AND COLUMN_NAME = 'StudentCourseEnrollmentId'
    ),
    'ALTER TABLE `maktab_dev`.`student_course_results` DROP COLUMN `StudentCourseEnrollmentId`',
    'SELECT 1'
);
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

SET @sql = IF (
    EXISTS (
        SELECT 1
        FROM INFORMATION_SCHEMA.COLUMNS
        WHERE TABLE_SCHEMA = 'maktab_dev'
          AND TABLE_NAME = 'student_course_results'
          AND COLUMN_NAME = 'CourseEnrollmentGroupId'
    ),
    'ALTER TABLE `maktab_dev`.`student_course_results` DROP COLUMN `CourseEnrollmentGroupId`',
    'SELECT 1'
);
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

UPDATE `maktab_dev`.`student_course_results`
SET `AttendancePercentage` = 100.00
WHERE `AttendancePercentage` IS NULL;

ALTER TABLE `maktab_dev`.`student_course_results`
    MODIFY COLUMN `AttendancePercentage` DECIMAL(5,2) NOT NULL DEFAULT 100.00;

SET @sql = IF (
    EXISTS (
        SELECT 1
        FROM INFORMATION_SCHEMA.STATISTICS
        WHERE TABLE_SCHEMA = 'maktab_dev'
          AND TABLE_NAME = 'student_course_results'
          AND INDEX_NAME = 'uq_student_course_results_child_course'
    ),
    'SELECT 1',
    'ALTER TABLE `maktab_dev`.`student_course_results` ADD UNIQUE KEY `uq_student_course_results_child_course` (`ChildId`, `CourseId`)'
);
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

SET @sql = IF (
    EXISTS (
        SELECT 1
        FROM INFORMATION_SCHEMA.STATISTICS
        WHERE TABLE_SCHEMA = 'maktab_dev'
          AND TABLE_NAME = 'student_course_results'
          AND INDEX_NAME = 'idx_student_course_results_course_active'
    ),
    'SELECT 1',
    'ALTER TABLE `maktab_dev`.`student_course_results` ADD KEY `idx_student_course_results_course_active` (`CourseId`, `IsActive`)'
);
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

SET SQL_SAFE_UPDATES = @previous_sql_safe_updates;
