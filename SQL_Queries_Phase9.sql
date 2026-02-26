-- ============================================================
-- Phase 9: Attendance & Salary Module Redesign — SQL Queries
-- Database: RestaurantManagementDB (BILAL\SQLEXPRESS)
-- ============================================================

-- ============================================================
-- 1. ALTER Attendance Table — Add TotalHours & OvertimeHours
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Attendances') AND name = 'TotalHours')
BEGIN
    ALTER TABLE [dbo].[Attendances]
        ADD [TotalHours] DECIMAL(5,2) NOT NULL DEFAULT 0;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Attendances') AND name = 'OvertimeHours')
BEGIN
    ALTER TABLE [dbo].[Attendances]
        ADD [OvertimeHours] DECIMAL(5,2) NOT NULL DEFAULT 0;
END
GO


-- ============================================================
-- 2. ALTER SalaryRecords Table — Add Formula & Workflow Columns
-- ============================================================

-- Overtime
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('SalaryRecords') AND name = 'OvertimeHours')
BEGIN
    ALTER TABLE [dbo].[SalaryRecords]
        ADD [OvertimeHours] DECIMAL(5,2) NOT NULL DEFAULT 0;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('SalaryRecords') AND name = 'OvertimePay')
BEGIN
    ALTER TABLE [dbo].[SalaryRecords]
        ADD [OvertimePay] DECIMAL(10,2) NOT NULL DEFAULT 0;
END
GO

-- Attendance Bonus
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('SalaryRecords') AND name = 'AttendanceBonus')
BEGIN
    ALTER TABLE [dbo].[SalaryRecords]
        ADD [AttendanceBonus] DECIMAL(10,2) NOT NULL DEFAULT 0;
END
GO

-- Gross Salary
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('SalaryRecords') AND name = 'GrossSalary')
BEGIN
    ALTER TABLE [dbo].[SalaryRecords]
        ADD [GrossSalary] DECIMAL(10,2) NOT NULL DEFAULT 0;
END
GO

-- Deduction Breakdown
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('SalaryRecords') AND name = 'AbsenceDeduction')
BEGIN
    ALTER TABLE [dbo].[SalaryRecords]
        ADD [AbsenceDeduction] DECIMAL(10,2) NOT NULL DEFAULT 0;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('SalaryRecords') AND name = 'HalfDayDeduction')
BEGIN
    ALTER TABLE [dbo].[SalaryRecords]
        ADD [HalfDayDeduction] DECIMAL(10,2) NOT NULL DEFAULT 0;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('SalaryRecords') AND name = 'LatePenaltyDeduction')
BEGIN
    ALTER TABLE [dbo].[SalaryRecords]
        ADD [LatePenaltyDeduction] DECIMAL(10,2) NOT NULL DEFAULT 0;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('SalaryRecords') AND name = 'TotalDeductions')
BEGIN
    ALTER TABLE [dbo].[SalaryRecords]
        ADD [TotalDeductions] DECIMAL(10,2) NOT NULL DEFAULT 0;
END
GO

-- Workflow Status
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('SalaryRecords') AND name = 'Status')
BEGIN
    ALTER TABLE [dbo].[SalaryRecords]
        ADD [Status] NVARCHAR(20) NOT NULL DEFAULT 'Draft';
END
GO

-- Finalized By/At
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('SalaryRecords') AND name = 'FinalizedById')
BEGIN
    ALTER TABLE [dbo].[SalaryRecords]
        ADD [FinalizedById] INT NULL;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('SalaryRecords') AND name = 'FinalizedAt')
BEGIN
    ALTER TABLE [dbo].[SalaryRecords]
        ADD [FinalizedAt] DATETIME2 NULL;
END
GO

-- Unlocked By/At
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('SalaryRecords') AND name = 'UnlockedById')
BEGIN
    ALTER TABLE [dbo].[SalaryRecords]
        ADD [UnlockedById] INT NULL;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('SalaryRecords') AND name = 'UnlockedAt')
BEGIN
    ALTER TABLE [dbo].[SalaryRecords]
        ADD [UnlockedAt] DATETIME2 NULL;
END
GO

-- Foreign Keys for Finalized/Unlocked
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_SalaryRecords_FinalizedBy')
BEGIN
    ALTER TABLE [dbo].[SalaryRecords]
        ADD CONSTRAINT [FK_SalaryRecords_FinalizedBy]
        FOREIGN KEY ([FinalizedById]) REFERENCES [dbo].[Users]([Id])
        ON DELETE NO ACTION;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_SalaryRecords_UnlockedBy')
BEGIN
    ALTER TABLE [dbo].[SalaryRecords]
        ADD CONSTRAINT [FK_SalaryRecords_UnlockedBy]
        FOREIGN KEY ([UnlockedById]) REFERENCES [dbo].[Users]([Id])
        ON DELETE NO ACTION;
END
GO

-- Check Constraint for Status
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_SalaryRecord_Status')
BEGIN
    ALTER TABLE [dbo].[SalaryRecords]
        ADD CONSTRAINT [CK_SalaryRecord_Status]
        CHECK ([Status] IN ('Draft', 'Finalized'));
END
GO


-- ============================================================
-- 3. CREATE SalaryAdjustments Table
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'SalaryAdjustments')
BEGIN
    CREATE TABLE [dbo].[SalaryAdjustments] (
        [Id]              INT IDENTITY(1,1) NOT NULL,
        [SalaryRecordId]  INT NOT NULL,
        [Type]            NVARCHAR(20) NOT NULL,        -- 'Bonus' or 'Deduction'
        [Amount]          DECIMAL(10,2) NOT NULL,
        [Reason]          NVARCHAR(500) NULL,
        [CreatedById]     INT NULL,
        [CreatedAt]       DATETIME2 NOT NULL DEFAULT GETDATE(),

        CONSTRAINT [PK_SalaryAdjustments] PRIMARY KEY ([Id]),

        CONSTRAINT [FK_SalaryAdjustments_SalaryRecord]
            FOREIGN KEY ([SalaryRecordId])
            REFERENCES [dbo].[SalaryRecords]([Id])
            ON DELETE CASCADE,

        CONSTRAINT [FK_SalaryAdjustments_CreatedBy]
            FOREIGN KEY ([CreatedById])
            REFERENCES [dbo].[Users]([Id])
            ON DELETE NO ACTION,

        CONSTRAINT [CK_SalaryAdjustment_Type]
            CHECK ([Type] IN ('Bonus', 'Deduction')),

        CONSTRAINT [CK_SalaryAdjustment_Amount]
            CHECK ([Amount] > 0)
    );
END
GO


-- ============================================================
-- 4. USEFUL QUERIES
-- ============================================================

-- 4a. Monthly Attendance Summary per Staff
SELECT
    s.Id AS StaffId,
    u.Name AS StaffName,
    YEAR(a.Date) AS [Year],
    MONTH(a.Date) AS [Month],
    COUNT(*) AS TotalRecords,
    SUM(CASE WHEN a.Status = 'Present' THEN 1 ELSE 0 END) AS DaysPresent,
    SUM(CASE WHEN a.Status = 'Absent' THEN 1 ELSE 0 END) AS DaysAbsent,
    SUM(CASE WHEN a.Status = 'Late' THEN 1 ELSE 0 END) AS DaysLate,
    SUM(CASE WHEN a.Status = 'Half-Day' THEN 1 ELSE 0 END) AS DaysHalfDay,
    SUM(a.TotalHours) AS TotalHoursWorked,
    SUM(a.OvertimeHours) AS TotalOvertimeHours
FROM Attendances a
INNER JOIN Staff s ON a.StaffId = s.Id
INNER JOIN Users u ON s.UserId = u.Id
WHERE a.Date >= '2025-01-01' AND a.Date < '2025-02-01'
GROUP BY s.Id, u.Name, YEAR(a.Date), MONTH(a.Date)
ORDER BY u.Name;


-- 4b. Salary Records with Formula Breakdown
SELECT
    sr.Id,
    u.Name AS StaffName,
    b.Name AS Branch,
    sr.Year, sr.Month,
    sr.BaseSalary,
    sr.OvertimeHours,
    sr.OvertimePay,
    sr.AttendanceBonus,
    sr.GrossSalary,
    sr.AbsenceDeduction,
    sr.HalfDayDeduction,
    sr.LatePenaltyDeduction,
    sr.TotalDeductions,
    sr.FinalSalary AS NetSalary,
    sr.Status AS WorkflowStatus,
    sr.PaymentStatus,
    sr.PaidDate
FROM SalaryRecords sr
INNER JOIN Staff s ON sr.StaffId = s.Id
INNER JOIN Users u ON s.UserId = u.Id
INNER JOIN Branches b ON sr.BranchId = b.Id
WHERE sr.Year = 2025 AND sr.Month = 1
ORDER BY u.Name;


-- 4c. Salary Adjustments for a Record
SELECT
    sa.Id,
    sa.Type,
    sa.Amount,
    sa.Reason,
    uc.Name AS CreatedByName,
    sa.CreatedAt
FROM SalaryAdjustments sa
LEFT JOIN Users uc ON sa.CreatedById = uc.Id
WHERE sa.SalaryRecordId = @RecordId
ORDER BY sa.CreatedAt;


-- 4d. Finance Summary (Total Payroll for a Month)
SELECT
    sr.Year,
    sr.Month,
    COUNT(*) AS TotalStaff,
    SUM(sr.BaseSalary) AS TotalBaseSalary,
    SUM(sr.OvertimePay) AS TotalOvertimePay,
    SUM(sr.AttendanceBonus) AS TotalAttendanceBonus,
    SUM(sr.GrossSalary) AS TotalGross,
    SUM(sr.TotalDeductions) AS TotalDeductions,
    SUM(sr.FinalSalary) AS TotalNetPayable,
    SUM(CASE WHEN sr.PaymentStatus = 'Paid' THEN sr.FinalSalary ELSE 0 END) AS TotalPaid,
    SUM(CASE WHEN sr.PaymentStatus = 'Pending' THEN sr.FinalSalary ELSE 0 END) AS TotalPending,
    SUM(CASE WHEN sr.Status = 'Draft' THEN 1 ELSE 0 END) AS DraftCount,
    SUM(CASE WHEN sr.Status = 'Finalized' THEN 1 ELSE 0 END) AS FinalizedCount
FROM SalaryRecords sr
WHERE sr.Year = 2025 AND sr.Month = 1
GROUP BY sr.Year, sr.Month;


-- 4e. Attendance Daily Rate Calculation (verify formula)
SELECT
    u.Name,
    ss.BaseSalary,
    sr.TotalWorkingDays,
    CAST(ss.BaseSalary / NULLIF(sr.TotalWorkingDays, 0) AS DECIMAL(10,2)) AS DailyRate,
    CAST(ss.BaseSalary / NULLIF(sr.TotalWorkingDays, 0) / 8 AS DECIMAL(10,2)) AS HourlyRate,
    sr.OvertimeHours,
    CAST(sr.OvertimeHours * (ss.BaseSalary / NULLIF(sr.TotalWorkingDays, 0) / 8) * 1.5 AS DECIMAL(10,2)) AS CalcOvertimePay,
    sr.OvertimePay AS StoredOvertimePay
FROM SalaryRecords sr
INNER JOIN Staff s ON sr.StaffId = s.Id
INNER JOIN Users u ON s.UserId = u.Id
INNER JOIN StaffSalaries ss ON s.Id = ss.StaffId AND ss.IsActive = 1
WHERE sr.Year = 2025 AND sr.Month = 1
ORDER BY u.Name;


-- 4f. Staff with Perfect Attendance (eligible for 5% bonus)
SELECT
    u.Name,
    sr.DaysAbsent,
    sr.DaysLate,
    sr.AttendanceBonus,
    CASE WHEN sr.DaysAbsent = 0 AND sr.DaysLate <= 2 THEN 'Eligible' ELSE 'Not Eligible' END AS BonusEligibility
FROM SalaryRecords sr
INNER JOIN Staff s ON sr.StaffId = s.Id
INNER JOIN Users u ON s.UserId = u.Id
WHERE sr.Year = 2025 AND sr.Month = 1
ORDER BY BonusEligibility DESC, u.Name;
