# Mitsubishi PLC To MSSQL Logger

A powerful .NET 8 WPF desktop application for real-time data acquisition, logging, and database integration with Mitsubishi PLCs using the MC Protocol.

## Overview

Mitsubishi PLC To MSSQL Logger is a professional-grade Windows application designed for industrial automation environments. It provides seamless communication with Mitsubishi PLCs, real-time data monitoring, flexible SQL database logging, and advanced conditional data processing capabilities.

## Screenshots

> Add your screenshots here

## Key Features

### PLC Communication
- **Real-time Data Reading**: Continuous monitoring of PLC registers with configurable intervals
- **Bit-Level Addressing**: Read individual bits from registers (e.g., `D3115.1`)
- **Write Capability**: Write values to PLC registers with thread-safe operations
- **Connection Management**: Automatic timeout handling and retry logic
- **Thread-Safe Operations**: Semaphore-based locking prevents simultaneous read/write conflicts

### Data Logging
- **Multiple Log Modes**:
  - **Disabled**: No logging
  - **Interval**: Log at fixed time intervals
  - **OnChange**: Log only when values change
  - **Both**: Combine interval and change-based logging

- **Conditional Logging**: Execute SQL queries based on complex conditions
  - Support for AND/OR operators
  - Cross-register value comparisons
  - Numeric and string comparison support
  - Example: `Reg_T2 == '1' AND Value > 50`

### Database Integration
- **SQL Server Support**: Native integration with Microsoft SQL Server
- **Flexible SQL Operations**:
  - INSERT new records
  - UPDATE existing records
  - UPSERT (INSERT or UPDATE)
  - Custom SQL queries per register

- **Smart Parameters**: Access register values across queries
  - `@FieldName` - Register field name
  - `@RegisterAddress` - PLC address
  - `@Value` - Current value
  - `@Timestamp` - Read timestamp
  - `@Description` - Register description
  - `@Unit` - Unit of measurement
  - `@Reg_{FieldName}` - Access other register values

### Configuration Management
- **JSON-Based Configuration**: Easy-to-edit register mappings
- **Visual Configuration Editor**: Built-in GUI for managing registers
- **Hot Reload**: Update configuration without restarting the application
- **Multiple Data Types**: INT, UINT, FLOAT, BOOL, STRING

### User Interface
- **Real-Time Data Grid**: Live updating values with timestamps
- **Connection Status Indicator**: Visual feedback (green=connected, red=disconnected)
- **Configuration Editor**: Dedicated window for register management
- **Status Messages**: Comprehensive logging and error reporting

## Supported PLC Models

### Mitsubishi PLC
- **Protocol**: MC Protocol (Mitsubishi Communication Protocol)
- **Connection**: TCP/IP
- **Supported Models**:
  - MELSEC iQ-R Series
  - MELSEC iQ-F Series
  - MELSEC Q Series
  - MELSEC L Series
  - Any Mitsubishi PLC supporting MC Protocol over Ethernet

### Default Configuration
- **IP Address**: 192.168.3.39
- **Port**: 9005
- **Device Type**: D Registers (Data Registers)
- **Communication Method**: Binary MC Protocol

## Technology Stack

- **.NET 8.0** - Latest LTS framework
- **WPF** - Windows Presentation Foundation
- **MVVM Architecture** - Clean separation of concerns
- **CommunityToolkit.Mvvm** - Modern MVVM helpers
- **Microsoft.Data.SqlClient** - SQL Server connectivity
- **Newtonsoft.Json** - Configuration serialization

## Installation

### Prerequisites
- Windows 10/11 (64-bit)
- .NET 8.0 Runtime or SDK
- SQL Server (Express, Standard, or Enterprise) - Optional, for database logging
- Network connectivity to your Mitsubishi PLC

### Download .NET 8.0
If you don't have .NET 8.0 installed:
1. Download from [Microsoft .NET Downloads](https://dotnet.microsoft.com/download/dotnet/8.0)
2. Install the SDK (for development) or Runtime (for running only)

### Build from Source
```bash
# Clone the repository
git clone https://github.com/bonggiatek/Mitsubishi-PLC-To-MSSQL-Logger.git
cd Mitsubishi-PLC-To-MSSQL-Logger

# Restore dependencies
dotnet restore

# Build the project
dotnet build --configuration Release

# Run the application
dotnet run
```

### Create Executable
```bash
# Publish as self-contained executable
dotnet publish -c Release -r win-x64 --self-contained true

# Find executable in:
# bin/Release/net8.0-windows/win-x64/publish/PLCDataLogger.exe
```

## Quick Start

### 1. PLC Configuration

Configure your Mitsubishi PLC to enable MC Protocol communication:

1. **Set PLC IP Address**: Configure your PLC's Ethernet module with a static IP
2. **Enable MC Protocol**: In GX Works or your PLC configuration software
3. **Set Port Number**: Default is 9005, or use your custom port
4. **Configure Open Settings**: Enable communication with external devices

### 2. Database Setup (Optional)

If you want to use database logging:

```sql
-- Run the provided SQL script to create the database
-- Located at: Configurations/CreateDatabase.sql

-- Or manually create:
CREATE DATABASE PLCData;
GO

USE PLCData;
GO

CREATE TABLE RegisterLogs (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    FieldName NVARCHAR(100) NOT NULL,
    RegisterAddress NVARCHAR(50) NOT NULL,
    Value NVARCHAR(500),
    Timestamp DATETIME NOT NULL,
    Description NVARCHAR(500),
    Unit NVARCHAR(50)
);
GO

-- Create indices for better performance
CREATE INDEX IX_RegisterLogs_FieldName ON RegisterLogs(FieldName);
CREATE INDEX IX_RegisterLogs_Timestamp ON RegisterLogs(Timestamp);
GO
```

### 3. Register Configuration

Edit `Configurations/registers.json` to define your PLC registers:

```json
{
  "registers": [
    {
      "FieldName": "Temperature",
      "RegisterAddress": "D100",
      "Description": "Tank Temperature",
      "DataType": 0,
      "Length": 1,
      "Unit": "°C",
      "Sql": {
        "ConnectionString": "Server=localhost;Database=PLCData;Integrated Security=true;TrustServerCertificate=true;",
        "TableName": "dbo.RegisterLogs",
        "LogMode": 2,
        "IntervalSeconds": 5,
        "UseCustomQuery": false,
        "CustomQuery": "",
        "LogCondition": ""
      }
    },
    {
      "FieldName": "PumpStatus",
      "RegisterAddress": "D3115.1",
      "Description": "Pump Running Status",
      "DataType": 3,
      "Length": 1,
      "Unit": "",
      "Sql": {
        "ConnectionString": "Server=localhost;Database=PLCData;Integrated Security=true;TrustServerCertificate=true;",
        "TableName": "dbo.RegisterLogs",
        "LogMode": 1,
        "IntervalSeconds": 0,
        "UseCustomQuery": true,
        "CustomQuery": "INSERT INTO dbo.RegisterLogs (FieldName, RegisterAddress, Value, Timestamp, Description, Unit) VALUES (@FieldName, @RegisterAddress, @Value, GETDATE(), @Description, @Unit)",
        "LogCondition": "Reg_Temperature > 50"
      }
    }
  ]
}
```

### 4. Run the Application

1. Launch `PLCDataLogger.exe`
2. Enter your PLC IP address and port
3. Click **Connect**
4. Monitor real-time data in the grid
5. Data will automatically log to your database based on configuration

## Configuration Reference

### Data Types

| DataType | Value | Description | Example |
|----------|-------|-------------|---------|
| INT | 0 | Signed 16-bit integer | -32768 to 32767 |
| UINT | 1 | Unsigned 16-bit integer | 0 to 65535 |
| FLOAT | 2 | 32-bit floating point | 1.23, -45.67 |
| BOOL | 3 | Boolean (bit) | true/false, 0/1 |
| STRING | 4 | Text string | "Hello" |

### Log Modes

| LogMode | Value | Behavior |
|---------|-------|----------|
| Disabled | 0 | No logging |
| OnChange | 1 | Log only when value changes |
| Interval | 2 | Log at fixed intervals |
| Both | 3 | Log on both change and interval |

### Register Address Format

- **Word Address**: `D100`, `D200`, `D1000`
- **Bit Address**: `D3115.0`, `D3115.1`, `D3115.15` (bit 0-15)
- **Length**: Number of consecutive registers to read (for multi-word data)

### SQL Configuration Properties

```json
{
  "ConnectionString": "SQL Server connection string",
  "TableName": "Target table name (e.g., dbo.RegisterLogs)",
  "LogMode": 0-3,
  "IntervalSeconds": "Interval in seconds (for Interval or Both modes)",
  "UseCustomQuery": true/false,
  "CustomQuery": "Your custom SQL statement",
  "LogCondition": "Optional condition (e.g., Reg_T1 == '1')"
}
```

### Predefined SQL Templates

The application includes built-in SQL templates accessible via the Configuration Editor:

1. **Standard INSERT**: Basic insert with all parameters
2. **INSERT with Server Time**: Use SQL Server's current time
3. **INSERT with NULL Check**: Only insert if value is not null
4. **UPDATE Latest Record**: Update the most recent record for a field
5. **UPDATE by FieldName**: Update specific field records
6. **UPSERT Pattern**: Insert if not exists, update if exists

## Usage Examples

### Example 1: Monitor Temperature Every 5 Seconds

```json
{
  "FieldName": "TankTemp",
  "RegisterAddress": "D200",
  "Description": "Main Tank Temperature",
  "DataType": 2,
  "Length": 2,
  "Unit": "°C",
  "Sql": {
    "ConnectionString": "Server=localhost;Database=PLCData;Integrated Security=true;",
    "TableName": "dbo.RegisterLogs",
    "LogMode": 2,
    "IntervalSeconds": 5,
    "UseCustomQuery": false,
    "CustomQuery": "",
    "LogCondition": ""
  }
}
```

### Example 2: Log Only When Status Changes

```json
{
  "FieldName": "MachineStatus",
  "RegisterAddress": "D150",
  "Description": "Production Line Status",
  "DataType": 0,
  "Length": 1,
  "Unit": "",
  "Sql": {
    "ConnectionString": "Server=localhost;Database=PLCData;Integrated Security=true;",
    "TableName": "dbo.RegisterLogs",
    "LogMode": 1,
    "IntervalSeconds": 0,
    "UseCustomQuery": false,
    "CustomQuery": "",
    "LogCondition": ""
  }
}
```

### Example 3: Conditional Logging with Custom Query

```json
{
  "FieldName": "AlarmCode",
  "RegisterAddress": "D500",
  "Description": "Active Alarm Code",
  "DataType": 0,
  "Length": 1,
  "Unit": "",
  "Sql": {
    "ConnectionString": "Server=localhost;Database=PLCData;Integrated Security=true;",
    "TableName": "dbo.AlarmHistory",
    "LogMode": 1,
    "IntervalSeconds": 0,
    "UseCustomQuery": true,
    "CustomQuery": "INSERT INTO dbo.AlarmHistory (AlarmCode, Timestamp, Severity, Description) VALUES (@Value, GETDATE(), 'HIGH', @Description)",
    "LogCondition": "Value != 0"
  }
}
```

### Example 4: Multi-Register Condition

```json
{
  "FieldName": "ProductionCount",
  "RegisterAddress": "D300",
  "Description": "Total Production Count",
  "DataType": 1,
  "Length": 1,
  "Unit": "pcs",
  "Sql": {
    "ConnectionString": "Server=localhost;Database=PLCData;Integrated Security=true;",
    "TableName": "dbo.Production",
    "LogMode": 3,
    "IntervalSeconds": 60,
    "UseCustomQuery": false,
    "CustomQuery": "",
    "LogCondition": "Reg_MachineStatus == 1 AND Value > 0"
  }
}
```

## Logs and Troubleshooting

### Application Logs

Logs are automatically saved to:
```
PLCDataLogger/Logs/PLCLog_YYYYMMDD.txt
```

Log entries include:
- **Timestamp**: When the event occurred
- **Level**: INFO, WARNING, ERROR
- **Message**: Detailed description

### Common Issues

#### Cannot Connect to PLC
**Symptoms**: Connection status remains red, "Failed to connect" error

**Solutions**:
1. Verify PLC IP address: `ping 192.168.3.39`
2. Check PLC port configuration (default: 9005)
3. Ensure PLC MC Protocol is enabled
4. Verify network connectivity and firewall settings
5. Check PLC is powered on and Ethernet module is active

#### No Data Updating
**Symptoms**: Connected but values don't change

**Solutions**:
1. Verify register addresses match your PLC configuration
2. Check PLC program is running
3. Review application logs for read errors
4. Ensure register addresses exist in your PLC

#### Database Logging Not Working
**Symptoms**: Connected to PLC but no database records

**Solutions**:
1. Verify SQL Server connection string
2. Check database and table exist
3. Ensure SQL Server allows connections
4. Review LogMode settings (must not be 0/Disabled)
5. Check LogCondition if specified
6. Review logs for SQL errors

#### Configuration Changes Not Applied
**Solutions**:
1. Save configuration in the editor
2. Restart the application or reload configuration
3. Check JSON syntax in `registers.json`
4. Review logs for configuration load errors

## Project Structure

```
PLCDataLogger/
├── Models/
│   ├── PLCRegisterData.cs              # Observable UI data model
│   ├── RegisterMapping.cs              # Configuration data model
│   └── (Other models)
├── Services/
│   ├── PLCService.cs                   # MC Protocol implementation
│   ├── DatabaseService.cs              # SQL Server operations
│   ├── ConfigurationService.cs         # JSON configuration loader
│   ├── LoggingService.cs               # File/console logging
│   ├── RegisterLogger.cs               # Per-register logging logic
│   └── ConditionEvaluator.cs           # Condition evaluation engine
├── ViewModels/
│   ├── MainViewModel.cs                # Main window logic (MVVM)
│   └── ConfigurationEditorViewModel.cs # Configuration editor logic
├── Views/
│   ├── ConfigurationEditorWindow.xaml  # Configuration UI
│   └── ConfigurationEditorWindow.xaml.cs
├── Converters/
│   ├── EnumToVisibilityConverter.cs    # UI converters
│   ├── NullToBoolConverter.cs
│   └── InverseBooleanConverter.cs
├── Helpers/
│   └── SqlQueryTemplates.cs            # SQL template library
├── Configurations/
│   ├── registers.json                  # Register configuration
│   └── CreateDatabase.sql              # Database setup script
├── Logs/                               # Auto-generated log files
├── MainWindow.xaml                     # Main UI layout
├── App.xaml                            # Application resources
└── PLCDataLogger.csproj                # Project file
```

## Advanced Features

### MC Protocol Details
- **Binary Protocol**: Efficient binary communication
- **Batch Operations**: Read/write multiple registers in one request
- **Error Handling**: Response validation and error code checking
- **Thread Safety**: Semaphore-based synchronization

### Security Considerations
- Connection strings stored in JSON (consider encryption for production)
- No authentication required for PLC connection (MC Protocol default)
- SQL Server authentication supported (Windows or SQL)
- File access should be restricted in production environments

## Contributing

Contributions are welcome! Please follow these guidelines:

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/YourFeature`)
3. Commit your changes (`git commit -m 'Add some feature'`)
4. Push to the branch (`git push origin feature/YourFeature`)
5. Open a Pull Request

## License

This project is licensed under the MIT License - see the LICENSE file for details.

## Acknowledgments

- Built with ❤️ for industrial automation professionals
- Uses the MC Protocol for Mitsubishi PLC communication
- Powered by .NET 8.0 and WPF

## Support

For issues, questions, or feature requests:
- **GitHub Issues**: [Create an issue](https://github.com/yourusername/Mitsubishi-PLC-To-MSSQL-Logger/issues)
- **Logs**: Check `Logs/PLCLog_YYYYMMDD.txt` for detailed error information
- **Configuration**: Review `Configurations/registers.json` for setup

---

**Made with .NET 8.0 | WPF | MC Protocol**
