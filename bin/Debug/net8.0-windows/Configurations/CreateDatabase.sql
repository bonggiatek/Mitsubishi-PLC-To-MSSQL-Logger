-- SQL Server Database Setup Script for PLC Data Logger
-- Run this script to create the database and table for logging PLC register values

-- Step 1: Create the database (if it doesn't exist)
IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = 'PLCData')
BEGIN
    CREATE DATABASE PLCData;
    PRINT 'Database PLCData created successfully.';
END
ELSE
BEGIN
    PRINT 'Database PLCData already exists.';
END
GO

-- Step 2: Switch to the PLCData database
USE PLCData;
GO

-- Step 3: Create the RegisterLogs table (if it doesn't exist)
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'RegisterLogs')
BEGIN
    CREATE TABLE RegisterLogs (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        FieldName NVARCHAR(100) NOT NULL,
        RegisterAddress NVARCHAR(50) NOT NULL,
        Value NVARCHAR(100),
        Timestamp DATETIME2 NOT NULL,
        Description NVARCHAR(500),
        Unit NVARCHAR(50),
        INDEX IX_Timestamp (Timestamp DESC),
        INDEX IX_FieldName_Timestamp (FieldName, Timestamp DESC)
    );
    PRINT 'Table RegisterLogs created successfully.';
END
ELSE
BEGIN
    PRINT 'Table RegisterLogs already exists.';
END
GO

-- Step 4: Verify the table structure
SELECT
    COLUMN_NAME,
    DATA_TYPE,
    CHARACTER_MAXIMUM_LENGTH,
    IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'RegisterLogs'
ORDER BY ORDINAL_POSITION;
GO

PRINT 'Database setup complete!';
PRINT '';
PRINT 'You can now configure your registers.json file with:';
PRINT '  "connectionString": "Server=localhost;Database=PLCData;Integrated Security=true;TrustServerCertificate=true;"';
PRINT '';
PRINT 'Sample query to view logged data:';
PRINT '  SELECT TOP 100 * FROM RegisterLogs ORDER BY Timestamp DESC';
GO
