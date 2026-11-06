-- ===============================================================================
-- SmartLog Analyzer - Script de migración inicial (SQL Server)
-- Generado a partir del modelo de Entity Framework Core (InitialCreate).
-- Se aplica automáticamente por la aplicación al arrancar (SeedHostedService).
-- Este archivo se conserva en db/ para auditoría y despliegues manuales.
-- ===============================================================================

IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Analysis]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[Analysis] (
        [Id] uniqueidentifier NOT NULL,
        [Title] nvarchar(max) NOT NULL,
        [Summary] nvarchar(max) NOT NULL,
        [ProblemCategories] nvarchar(max) NOT NULL,
        [ActionSuggestions] nvarchar(max) NOT NULL,
        [LogCount] int NOT NULL,
        [PeriodFrom] datetimeoffset NULL,
        [PeriodTo] datetimeoffset NULL,
        [Status] int NOT NULL,
        [ErrorMessage] nvarchar(max) NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_Analysis] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[LogEntries]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[LogEntries] (
        [Id] uniqueidentifier NOT NULL,
        [ServiceName] nvarchar(100) NOT NULL,
        [Level] int NOT NULL,
        [Message] nvarchar(4000) NOT NULL,
        [Timestamp] datetimeoffset NOT NULL,
        [Source] nvarchar(max) NULL,
        [TraceId] nvarchar(max) NULL,
        [Metadata] nvarchar(max) NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_LogEntries] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[AnalysisLogs]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[AnalysisLogs] (
        [AnalysisId] uniqueidentifier NOT NULL,
        [LogEntryId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_AnalysisLogs] PRIMARY KEY ([AnalysisId], [LogEntryId])
    );
END;

-- Índices para consultas frecuentes de listado y agregación.
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = N'IX_LogEntries_ServiceName' AND object_id = OBJECT_ID(N'[dbo].[LogEntries]'))
    CREATE INDEX [IX_LogEntries_ServiceName] ON [dbo].[LogEntries] ([ServiceName]);

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = N'IX_LogEntries_Level' AND object_id = OBJECT_ID(N'[dbo].[LogEntries]'))
    CREATE INDEX [IX_LogEntries_Level] ON [dbo].[LogEntries] ([Level]);

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = N'IX_LogEntries_Timestamp' AND object_id = OBJECT_ID(N'[dbo].[LogEntries]'))
    CREATE INDEX [IX_LogEntries_Timestamp] ON [dbo].[LogEntries] ([Timestamp]);

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = N'IX_LogEntries_CreatedAt' AND object_id = OBJECT_ID(N'[dbo].[LogEntries]'))
    CREATE INDEX [IX_LogEntries_CreatedAt] ON [dbo].[LogEntries] ([CreatedAt]);

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = N'IX_Analysis_CreatedAt' AND object_id = OBJECT_ID(N'[dbo].[Analysis]'))
    CREATE INDEX [IX_Analysis_CreatedAt] ON [dbo].[Analysis] ([CreatedAt]);

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = N'IX_Analysis_Status' AND object_id = OBJECT_ID(N'[dbo].[Analysis]'))
    CREATE INDEX [IX_Analysis_Status] ON [dbo].[Analysis] ([Status]);

-- Claves foráneas de la unión muchos-a-muchos Analysis-Log.
ALTER TABLE [dbo].[AnalysisLogs]
    ADD CONSTRAINT [FK_AnalysisLogs_Analysis_AnalysisId]
    FOREIGN KEY ([AnalysisId]) REFERENCES [dbo].[Analysis] ([Id]) ON DELETE CASCADE;

ALTER TABLE [dbo].[AnalysisLogs]
    ADD CONSTRAINT [FK_AnalysisLogs_LogEntries_LogEntryId]
    FOREIGN KEY ([LogEntryId]) REFERENCES [dbo].[LogEntries] ([Id]) ON DELETE CASCADE;

-- ===============================================================================
-- Seed de datos de ejemplo (120 logs de 3 servicios)
-- Se ejecuta por SeedData.InitializeAsync si la tabla LogEntries está vacía.
-- Se conserva aquí para reproducir entornos sin correr la aplicación.
-- ===============================================================================

/*
Ejemplo de inserciones (representativo; la aplicación ejecuta el seed completo en C#):

INSERT INTO [dbo].[LogEntries] ([Id], [ServiceName], [Level], [Message], [Timestamp], [Source], [TraceId], [Metadata], [CreatedAt])
VALUES
('8a3f1c2e-4b5d-4e6f-8a9b-0c1d2e3f4a5b', 'payments', 3, 'Excepción al procesar transacción: tiempo de respuesta superado.', '2026-11-01T10:15:30.123-03:00', 'k8s-pod-payments-1', 'trace-1001', '{"requestId":"req-001","version":"1.0.0"}', SYSDATETIMEOFFSET()),
('b2c4d6e8-1a3b-5c7d-9e0f-1a2b3c4d5e6f', 'inventory', 1, 'Operación normal en inventory completada correctamente.', '2026-11-01T10:16:02.456-03:00', 'k8s-pod-inventory-2', 'trace-1002', '{"requestId":"req-002","version":"1.0.0"}', SYSDATETIMEOFFSET());
*/

-- Niveles (enum LogLevel): 0=Debug, 1=Information, 2=Warning, 3=Error, 4=Critical, 5=Fatal
