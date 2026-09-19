-- Existing-schema migration: optional course-result remarks and attendance notes,
-- plus a 100% default when a participant has no attendance records.
-- New installations already receive these nullable columns from the create-table scripts.

SET @previous_sql_safe_updates := @@SQL_SAFE_UPDATES;
SET SQL_SAFE_UPDATES = 0;

ALTER TABLE `maktab`.`student_course_results`
    MODIFY COLUMN `Remarks` TEXT NULL;

ALTER TABLE `maktab`.`student_course_attendance`
    MODIFY COLUMN `Notes` TEXT NULL;

UPDATE `maktab`.`student_course_results`
SET `AttendancePercentage` = 100.00
WHERE `AttendancePercentage` IS NULL;

ALTER TABLE `maktab`.`student_course_results`
    MODIFY COLUMN `AttendancePercentage` DECIMAL(5,2) NOT NULL DEFAULT 100.00;

ALTER TABLE `maktab_dev`.`student_course_results`
    MODIFY COLUMN `Remarks` TEXT NULL;

ALTER TABLE `maktab_dev`.`student_course_attendance`
    MODIFY COLUMN `Notes` TEXT NULL;

UPDATE `maktab_dev`.`student_course_results`
SET `AttendancePercentage` = 100.00
WHERE `AttendancePercentage` IS NULL;

ALTER TABLE `maktab_dev`.`student_course_results`
    MODIFY COLUMN `AttendancePercentage` DECIMAL(5,2) NOT NULL DEFAULT 100.00;

SET SQL_SAFE_UPDATES = @previous_sql_safe_updates;
