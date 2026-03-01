-- ====================================================================
-- Notification & Email Management System - SQL Migration Queries
-- Run against: RestaurantManagementDB (SQL Server)
-- ====================================================================

-- ====================================================================
-- 1. Notifications Table
-- ====================================================================
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Notifications')
BEGIN
    CREATE TABLE [dbo].[Notifications] (
        [Id]          INT            IDENTITY(1,1) NOT NULL,
        [Title]       NVARCHAR(200)  NOT NULL,
        [Message]     NVARCHAR(1000) NOT NULL,
        [Type]        NVARCHAR(20)   NOT NULL DEFAULT N'Info',
        [UserId]      INT            NULL,
        [RoleTarget]  NVARCHAR(50)   NULL,
        [BranchId]    INT            NULL,
        [IsRead]      BIT            NOT NULL DEFAULT 0,
        [CreatedAt]   DATETIME2(7)   NOT NULL DEFAULT GETUTCDATE(),
        [CreatedBy]   INT            NULL,
        [RedirectUrl] NVARCHAR(500)  NULL,
        [Icon]        NVARCHAR(100)  NULL,
        CONSTRAINT [PK_Notifications] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_Notifications_Users_UserId] FOREIGN KEY ([UserId])
            REFERENCES [dbo].[Users]([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Notifications_Branches_BranchId] FOREIGN KEY ([BranchId])
            REFERENCES [dbo].[Branches]([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Notifications_Users_CreatedBy] FOREIGN KEY ([CreatedBy])
            REFERENCES [dbo].[Users]([Id]) ON DELETE NO ACTION
    );
    PRINT 'Created table: Notifications';
END
GO

-- Indexes for Notifications
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Notifications_UserId' AND object_id = OBJECT_ID('Notifications'))
    CREATE NONCLUSTERED INDEX [IX_Notifications_UserId] ON [dbo].[Notifications]([UserId]);
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Notifications_IsRead' AND object_id = OBJECT_ID('Notifications'))
    CREATE NONCLUSTERED INDEX [IX_Notifications_IsRead] ON [dbo].[Notifications]([IsRead]);
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Notifications_CreatedAt' AND object_id = OBJECT_ID('Notifications'))
    CREATE NONCLUSTERED INDEX [IX_Notifications_CreatedAt] ON [dbo].[Notifications]([CreatedAt]);
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Notifications_RoleTarget' AND object_id = OBJECT_ID('Notifications'))
    CREATE NONCLUSTERED INDEX [IX_Notifications_RoleTarget] ON [dbo].[Notifications]([RoleTarget]);
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Notifications_BranchId' AND object_id = OBJECT_ID('Notifications'))
    CREATE NONCLUSTERED INDEX [IX_Notifications_BranchId] ON [dbo].[Notifications]([BranchId]);
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Notifications_Type' AND object_id = OBJECT_ID('Notifications'))
    CREATE NONCLUSTERED INDEX [IX_Notifications_Type] ON [dbo].[Notifications]([Type]);
GO

-- ====================================================================
-- 2. EmailQueues Table
-- ====================================================================
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'EmailQueues')
BEGIN
    CREATE TABLE [dbo].[EmailQueues] (
        [Id]             INT            IDENTITY(1,1) NOT NULL,
        [ToEmail]        NVARCHAR(255)  NOT NULL,
        [ToName]         NVARCHAR(255)  NULL,
        [Subject]        NVARCHAR(300)  NOT NULL,
        [Body]           NVARCHAR(MAX)  NOT NULL,
        [IsSent]         BIT            NOT NULL DEFAULT 0,
        [RetryCount]     INT            NOT NULL DEFAULT 0,
        [CreatedAt]      DATETIME2(7)   NOT NULL DEFAULT GETUTCDATE(),
        [SentAt]         DATETIME2(7)   NULL,
        [ErrorMessage]   NVARCHAR(1000) NULL,
        [NotificationId] INT            NULL,
        CONSTRAINT [PK_EmailQueues] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_EmailQueues_Notifications_NotificationId] FOREIGN KEY ([NotificationId])
            REFERENCES [dbo].[Notifications]([Id]) ON DELETE SET NULL
    );
    PRINT 'Created table: EmailQueues';
END
GO

-- Indexes for EmailQueues
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_EmailQueues_IsSent' AND object_id = OBJECT_ID('EmailQueues'))
    CREATE NONCLUSTERED INDEX [IX_EmailQueues_IsSent] ON [dbo].[EmailQueues]([IsSent]);
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_EmailQueues_CreatedAt' AND object_id = OBJECT_ID('EmailQueues'))
    CREATE NONCLUSTERED INDEX [IX_EmailQueues_CreatedAt] ON [dbo].[EmailQueues]([CreatedAt]);
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_EmailQueues_NotificationId' AND object_id = OBJECT_ID('EmailQueues'))
    CREATE NONCLUSTERED INDEX [IX_EmailQueues_NotificationId] ON [dbo].[EmailQueues]([NotificationId]);
GO

-- ====================================================================
-- 3. NotificationPreferences Table
-- ====================================================================
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'NotificationPreferences')
BEGIN
    CREATE TABLE [dbo].[NotificationPreferences] (
        [Id]                     INT  IDENTITY(1,1) NOT NULL,
        [UserId]                 INT  NOT NULL,
        [InAppEnabled]           BIT  NOT NULL DEFAULT 1,
        [EmailEnabled]           BIT  NOT NULL DEFAULT 1,
        [OrderNotifications]     BIT  NOT NULL DEFAULT 1,
        [StaffNotifications]     BIT  NOT NULL DEFAULT 1,
        [InventoryNotifications] BIT  NOT NULL DEFAULT 1,
        [FinancialNotifications] BIT  NOT NULL DEFAULT 1,
        [SystemNotifications]    BIT  NOT NULL DEFAULT 1,
        CONSTRAINT [PK_NotificationPreferences] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_NotificationPreferences_Users_UserId] FOREIGN KEY ([UserId])
            REFERENCES [dbo].[Users]([Id]) ON DELETE CASCADE
    );
    PRINT 'Created table: NotificationPreferences';
END
GO

-- Unique index on UserId (one preference record per user)
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_NotificationPreferences_UserId' AND object_id = OBJECT_ID('NotificationPreferences'))
    CREATE UNIQUE NONCLUSTERED INDEX [IX_NotificationPreferences_UserId] ON [dbo].[NotificationPreferences]([UserId]);
GO

-- ====================================================================
-- 4. Verification Query
-- ====================================================================
SELECT 
    t.TABLE_NAME,
    (SELECT COUNT(*) FROM sys.indexes i WHERE i.object_id = OBJECT_ID(t.TABLE_NAME) AND i.is_primary_key = 0) AS IndexCount
FROM INFORMATION_SCHEMA.TABLES t
WHERE t.TABLE_NAME IN ('Notifications', 'EmailQueues', 'NotificationPreferences')
ORDER BY t.TABLE_NAME;
GO

PRINT '=== Notification System Migration Complete ===';
GO
