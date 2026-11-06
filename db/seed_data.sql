-- ===============================================================================
-- SmartLog Analyzer - Script de seed de datos de ejemplo
-- Pobla la tabla LogEntries con 120 registros de 3 microservicios
-- (payments, inventory, notifications) en un rango de 48 horas.
-- payments tiene un sesgo del 25% de logs de Error para demostrar agregaciones.
-- ===============================================================================

-- Ejecutar solo si la tabla está vacía.
IF EXISTS (SELECT TOP 1 1 FROM [dbo].[LogEntries])
BEGIN
    PRINT 'LogEntries ya contiene datos. Seed omitido.';
    RETURN;
END;

-- Nota: este script es un esqueleto ilustrativo. El seed completo (120 filas con
-- identificadores GUID únicos y distribución aleatoria) se ejecuta desde C# mediante
-- SmartLogAnalyzer.Infrastructure.Data.SeedData.InitializeAsync(), que usa
-- Entity Framework Core y una semilla fija (Random(42)) para ser reproducible.

-- Ejemplo de una fila representativa por servicio:
INSERT INTO [dbo].[LogEntries] (
    [Id], [ServiceName], [Level], [Message], [Timestamp], [Source], [TraceId], [Metadata], [CreatedAt]
) VALUES (
    '11111111-1111-1111-1111-111111111111',
    'payments',
    3,
    'Excepción al procesar transacción en payments: tiempo de respuesta superado.',
    DATEADD(minute, -120, SYSDATETIMEOFFSET()),
    'k8s-pod-payments-1',
    'trace-0001',
    N'{"requestId":"req-0001","version":"1.0.0"}',
    SYSDATETIMEOFFSET()
);

INSERT INTO [dbo].[LogEntries] (
    [Id], [ServiceName], [Level], [Message], [Timestamp], [Source], [TraceId], [Metadata], [CreatedAt]
) VALUES (
    '22222222-2222-2222-2222-222222222222',
    'inventory',
    1,
    'Operación normal en inventory completada correctamente.',
    DATEADD(minute, -90, SYSDATETIMEOFFSET()),
    'k8s-pod-inventory-2',
    'trace-0002',
    N'{"requestId":"req-0002","version":"1.0.0"}',
    SYSDATETIMEOFFSET()
);

INSERT INTO [dbo].[LogEntries] (
    [Id], [ServiceName], [Level], [Message], [Timestamp], [Source], [TraceId], [Metadata], [CreatedAt]
) VALUES (
    '33333333-3333-3333-3333-333333333333',
    'notifications',
    2,
    'Cola de notificaciones por encima del umbral de advertencia.',
    DATEADD(minute, -60, SYSDATETIMEOFFSET()),
    'k8s-pod-notifications-3',
    'trace-0003',
    N'{"requestId":"req-0003","version":"1.0.0"}',
    SYSDATETIMEOFFSET()
);

PRINT 'Seed representativo insertado. Ejecutar la aplicación para poblar los 120 registros completos.';
