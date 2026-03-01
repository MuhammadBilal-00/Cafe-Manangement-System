-- ============================================================
-- Run These SQL Queries If Needed
-- Migration: AddBranchIdToAuditLog
-- Description: Adds BranchId column to AuditLogs table with
--              FK reference to Branches and an index.
-- ============================================================

-- Step 1: Add BranchId colum
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'AuditLogs' AND COLUMN_NAME = 'BranchId'
)
BEGIN
    ALTER TABLE [AuditLogs] ADD [BranchId] INT NULL;
    PRINT 'Column BranchId added to AuditLogs.';
END
ELSE
BEGIN
    PRINT 'Column BranchId already exists on AuditLogs. Skipping.';
END
GO

-- Step 2: Create index on BranchId
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'IX_AuditLogs_BranchId' AND object_id = OBJECT_ID('AuditLogs')
)
BEGIN
    CREATE INDEX [IX_AuditLogs_BranchId] ON [AuditLogs] ([BranchId]);
    PRINT 'Index IX_AuditLogs_BranchId created.';
END
ELSE
BEGIN
    PRINT 'Index IX_AuditLogs_BranchId already exists. Skipping.';
END
GO

-- Step 3: Add foreign key to Branches table
IF NOT EXISTS (
    SELECT 1 FROM sys.foreign_keys
    WHERE name = 'FK_AuditLogs_Branches_BranchId'
)
BEGIN
    ALTER TABLE [AuditLogs]
        ADD CONSTRAINT [FK_AuditLogs_Branches_BranchId]
        FOREIGN KEY ([BranchId]) REFERENCES [Branches]([Id]);
    PRINT 'Foreign key FK_AuditLogs_Branches_BranchId created.';
END
ELSE
BEGIN
    PRINT 'Foreign key FK_AuditLogs_Branches_BranchId already exists. Skipping.';
END
GO

-- Step 4: Record migration in EF history (if running manually)
IF NOT EXISTS (
    SELECT 1 FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = '20260225004941_AddBranchIdToAuditLog'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES ('20260225004941_AddBranchIdToAuditLog', '8.0.11');
    PRINT 'Migration history entry recorded.';
END
GO
