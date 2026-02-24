-- ============================================================
-- Run These SQL Queries If Needed
-- Attendance, Salary & Financial Management Tables
-- ============================================================

-- 1. ATTENDANCES TABLE
CREATE TABLE [Attendances] (
    [Id]            INT             IDENTITY(1,1) NOT NULL,
    [StaffId]       INT             NOT NULL,
    [BranchId]      INT             NOT NULL,
    [Date]          DATE            NOT NULL,
    [CheckInTime]   TIME            NULL,
    [CheckOutTime]  TIME            NULL,
    [Status]        NVARCHAR(20)    NOT NULL DEFAULT 'Present',
    [LateMinutes]   INT             NOT NULL DEFAULT 0,
    [Notes]         NVARCHAR(500)   NULL,
    [MarkedById]    INT             NULL,
    [CreatedAt]     DATETIME2       NOT NULL DEFAULT GETDATE(),
    [UpdatedAt]     DATETIME2       NULL,
    CONSTRAINT [PK_Attendances]                 PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Attendances_Staff]           FOREIGN KEY ([StaffId])    REFERENCES [Staff]([Id])    ON DELETE CASCADE,
    CONSTRAINT [FK_Attendances_Branches]        FOREIGN KEY ([BranchId])   REFERENCES [Branches]([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Attendances_Users_MarkedBy]  FOREIGN KEY ([MarkedById]) REFERENCES [Users]([Id])    ON DELETE NO ACTION,
    CONSTRAINT [CK_Attendance_Status]           CHECK ([Status] IN ('Present','Absent','Late','Half-Day'))
);

-- Unique index: one attendance record per staff per date
CREATE UNIQUE INDEX [IX_Attendances_StaffId_Date] ON [Attendances] ([StaffId], [Date]);

-- ============================================================
-- 2. SALARY RECORDS TABLE
CREATE TABLE [SalaryRecords] (
    [Id]                    INT             IDENTITY(1,1) NOT NULL,
    [StaffId]               INT             NOT NULL,
    [BranchId]              INT             NOT NULL,
    [Year]                  INT             NOT NULL,
    [Month]                 INT             NOT NULL,
    [BaseSalary]            DECIMAL(10,2)   NOT NULL,
    [TotalWorkingDays]      INT             NOT NULL DEFAULT 0,
    [DaysPresent]           INT             NOT NULL DEFAULT 0,
    [DaysAbsent]            INT             NOT NULL DEFAULT 0,
    [DaysLate]              INT             NOT NULL DEFAULT 0,
    [DaysHalfDay]           INT             NOT NULL DEFAULT 0,
    [AttendancePercentage]  DECIMAL(5,2)    NOT NULL DEFAULT 0,
    [BonusAmount]           DECIMAL(10,2)   NOT NULL DEFAULT 0,
    [DeductionAmount]       DECIMAL(10,2)   NOT NULL DEFAULT 0,
    [BonusReason]           NVARCHAR(500)   NULL,
    [DeductionReason]       NVARCHAR(500)   NULL,
    [FinalSalary]           DECIMAL(10,2)   NOT NULL,
    [PaymentStatus]         NVARCHAR(20)    NOT NULL DEFAULT 'Pending',
    [PaidDate]              DATETIME2       NULL,
    [Notes]                 NVARCHAR(500)   NULL,
    [GeneratedById]         INT             NULL,
    [GeneratedAt]           DATETIME2       NOT NULL DEFAULT GETDATE(),
    CONSTRAINT [PK_SalaryRecords]                   PRIMARY KEY ([Id]),
    CONSTRAINT [FK_SalaryRecords_Staff]              FOREIGN KEY ([StaffId])       REFERENCES [Staff]([Id])    ON DELETE CASCADE,
    CONSTRAINT [FK_SalaryRecords_Branches]           FOREIGN KEY ([BranchId])      REFERENCES [Branches]([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_SalaryRecords_Users_GeneratedBy]  FOREIGN KEY ([GeneratedById]) REFERENCES [Users]([Id])    ON DELETE NO ACTION,
    CONSTRAINT [CK_SalaryRecord_PaymentStatus]       CHECK ([PaymentStatus] IN ('Pending','Paid','Cancelled')),
    CONSTRAINT [CK_SalaryRecord_Month]               CHECK ([Month] >= 1 AND [Month] <= 12)
);

-- Unique index: one salary record per staff per month
CREATE UNIQUE INDEX [IX_SalaryRecords_StaffId_Year_Month] ON [SalaryRecords] ([StaffId], [Year], [Month]);

-- ============================================================
-- 3. EXPENSES TABLE
CREATE TABLE [Expenses] (
    [Id]                    INT             IDENTITY(1,1) NOT NULL,
    [BranchId]              INT             NOT NULL,
    [Title]                 NVARCHAR(100)   NOT NULL,
    [Description]           NVARCHAR(500)   NULL,
    [Category]              NVARCHAR(50)    NOT NULL DEFAULT 'General',
    [Amount]                DECIMAL(10,2)   NOT NULL,
    [ExpenseDate]           DATETIME2       NOT NULL,
    [PaymentMethod]         NVARCHAR(20)    NOT NULL DEFAULT 'Cash',
    [ReferenceNumber]       NVARCHAR(100)   NULL,
    [ReceiptUrl]            NVARCHAR(200)   NULL,
    [IsRecurring]           BIT             NOT NULL DEFAULT 0,
    [RecurringFrequency]    NVARCHAR(20)    NULL,
    [ApprovalStatus]        NVARCHAR(20)    NOT NULL DEFAULT 'Approved',
    [ApprovedById]          INT             NULL,
    [ApprovedAt]            DATETIME2       NULL,
    [CreatedById]           INT             NULL,
    [CreatedAt]             DATETIME2       NOT NULL DEFAULT GETDATE(),
    CONSTRAINT [PK_Expenses]                    PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Expenses_Branches]           FOREIGN KEY ([BranchId])     REFERENCES [Branches]([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_Expenses_Users_ApprovedBy]   FOREIGN KEY ([ApprovedById]) REFERENCES [Users]([Id])    ON DELETE NO ACTION,
    CONSTRAINT [FK_Expenses_Users_CreatedBy]    FOREIGN KEY ([CreatedById])  REFERENCES [Users]([Id])    ON DELETE NO ACTION,
    CONSTRAINT [CK_Expense_Amount]              CHECK ([Amount] > 0),
    CONSTRAINT [CK_Expense_ApprovalStatus]      CHECK ([ApprovalStatus] IN ('Pending','Approved','Rejected'))
);
