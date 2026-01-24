-- =============================================
-- Birim Yönetimi için Veritabanı Tabloları
-- Bu SQL'i production veritabanında çalıştırın
-- =============================================

-- UnitTypes tablosu
CREATE TABLE IF NOT EXISTS "UnitTypes" (
    "Id" INTEGER PRIMARY KEY AUTOINCREMENT,
    "OrganizationId" INTEGER NOT NULL,
    "Name" TEXT NOT NULL,
    "Description" TEXT,
    "DefaultCoefficient" TEXT NOT NULL DEFAULT '1.0',
    "Color" TEXT,
    "Icon" TEXT,
    "SortOrder" INTEGER NOT NULL DEFAULT 0,
    "IsActive" INTEGER NOT NULL DEFAULT 1,
    "IsSystem" INTEGER NOT NULL DEFAULT 0,
    "CreatedAt" TEXT NOT NULL DEFAULT (datetime('now')),
    CONSTRAINT "FK_UnitTypes_Organizations_OrganizationId" FOREIGN KEY ("OrganizationId") REFERENCES "Organizations" ("Id") ON DELETE CASCADE
);

-- UnitTypes indeksi
CREATE UNIQUE INDEX IF NOT EXISTS "IX_UnitTypes_OrganizationId_Name" ON "UnitTypes" ("OrganizationId", "Name");

-- Units tablosu (eğer yoksa)
CREATE TABLE IF NOT EXISTS "Units" (
    "Id" INTEGER PRIMARY KEY AUTOINCREMENT,
    "OrganizationId" INTEGER NOT NULL,
    "UnitTypeId" INTEGER,
    "Name" TEXT NOT NULL,
    "Description" TEXT,
    "Coefficient" TEXT NOT NULL DEFAULT '1.0',
    "Color" TEXT,
    "IsDefault" INTEGER NOT NULL DEFAULT 0,
    "IsActive" INTEGER NOT NULL DEFAULT 1,
    "SortOrder" INTEGER NOT NULL DEFAULT 0,
    "CreatedAt" TEXT NOT NULL DEFAULT (datetime('now')),
    "UpdatedAt" TEXT NOT NULL DEFAULT (datetime('now')),
    CONSTRAINT "FK_Units_Organizations_OrganizationId" FOREIGN KEY ("OrganizationId") REFERENCES "Organizations" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_Units_UnitTypes_UnitTypeId" FOREIGN KEY ("UnitTypeId") REFERENCES "UnitTypes" ("Id") ON DELETE SET NULL
);

-- Units indeksi
CREATE UNIQUE INDEX IF NOT EXISTS "IX_Units_OrganizationId_Name" ON "Units" ("OrganizationId", "Name");
CREATE INDEX IF NOT EXISTS "IX_Units_UnitTypeId" ON "Units" ("UnitTypeId");

-- Employee tablosuna UnitId kolonu ekle (eğer yoksa)
-- SQLite'de ALTER TABLE ile column eklemek için:
-- Önce var mı kontrol edin, yoksa ekleyin

-- Not: SQLite'de IF NOT EXISTS ile column eklenemez
-- Manuel olarak kontrol edin:
-- PRAGMA table_info(Employees);
-- Eğer UnitId yoksa:
-- ALTER TABLE "Employees" ADD COLUMN "UnitId" INTEGER REFERENCES "Units"("Id") ON DELETE SET NULL;

