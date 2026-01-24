START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260119022107_AddLeaveTypeCodeEn') THEN
    ALTER TABLE "LeaveTypes" ADD "CodeEn" character varying(10) NOT NULL DEFAULT '';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260119022107_AddLeaveTypeCodeEn') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260119022107_AddLeaveTypeCodeEn', '9.0.1');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260119023430_AddEmployeePositionFields') THEN
    ALTER TABLE "Employees" ADD "AcademicTitle" character varying(50);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260119023430_AddEmployeePositionFields') THEN
    ALTER TABLE "Employees" ADD "IsNonHealthServices" boolean NOT NULL DEFAULT FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260119023430_AddEmployeePositionFields') THEN
    ALTER TABLE "Employees" ADD "PositionType" character varying(20);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260119023430_AddEmployeePositionFields') THEN
    ALTER TABLE "Employees" ADD "ShiftScore" integer NOT NULL DEFAULT 0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260119023430_AddEmployeePositionFields') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260119023430_AddEmployeePositionFields', '9.0.1');
    END IF;
END $EF$;
COMMIT;

