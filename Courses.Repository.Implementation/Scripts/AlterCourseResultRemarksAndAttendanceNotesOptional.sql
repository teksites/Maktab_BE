-- Existing-schema migration: optional course-result remarks and attendance notes.
-- New installations already receive these nullable columns from the create-table scripts.

ALTER TABLE `maktab`.`student_course_results`
    MODIFY COLUMN `Remarks` TEXT NULL;

ALTER TABLE `maktab`.`student_course_attendance`
    MODIFY COLUMN `Notes` TEXT NULL;

ALTER TABLE `maktab_dev`.`student_course_results`
    MODIFY COLUMN `Remarks` TEXT NULL;

ALTER TABLE `maktab_dev`.`student_course_attendance`
    MODIFY COLUMN `Notes` TEXT NULL;
