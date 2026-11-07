# SmartLog Analyzer

Prototipo funcional que recibe logs de microservicios, los almacena y usa IA para detectar patrones y generar insights.

## Estructura

```
smartlog-analyzer/
├── src/
│   ├── SmartLogAnalyzer.Core/
│   ├── SmartLogAnalyzer.Infrastructure/
│   └── SmartLogAnalyzer.Api/
├── tests/
│   ├── SmartLogAnalyzer.UnitTests/
│   └── SmartLogAnalyzer.IntegrationTests/
├── db/
├── Dockerfile
├── docker-compose.yml
├── .env.example
├── ARCHITECTURE.md
└── README.md
```

## Requisitos

- .NET 10 SDK
- Docker y Docker Compose (para levantar con un solo comando)
- SQL Server 2022 Express (o SQL Server en contenedor)

## Cómo levantar

```bash
cd src/SmartLogAnalyzer.Api
dotnet restore
dotnet run
```

La aplicación queda en `http://localhost:5000` (o el puerto configurado en `launchSettings.json`).

Para usar Docker:

```bash
docker-compose up --build
```

## Endpoints

| Método | Ruta | Descripción |
|--------|------|-------------|
| POST | /api/logs | Ingestar un log |
| GET | /api/logs | Listar logs con filtros y paginación |
| GET | /api/logs/aggregation | Agregación de logs por servicio/nivel |
| POST | /api/logs/analyze | Analizar logs con IA |
| GET | /api/analysis/{id} | Obtener resultado de un análisis |
| GET | /api/health | Verificar estado del servicio |

## Ejemplo de uso

Ingresar un log:

```json
POST /api/logs
{
  "serviceName": "payments",
  "level": "Error",
  "message": "Fallo al procesar pago",
  "timestamp": "2026-11-04T10:00:00Z",
  "source": "api-gateway",
  "traceId": "trace-abc",
  "metadata": "{}"
}
```

Listar logs con filtros:

```
GET /api/logs?serviceName=payments&level=Error&pageSize=10
```

Agregación:

```
GET /api/logs/aggregation?groupBy=service
GET /api/logs/aggregation?groupBy=level
```

Análisis con IA:

```json
POST /api/logs/analyze
{
  "logIds": ["guid-1", "guid-2"]
}
```

## Variables de entorno

Copiar `.env.example` a `.env` y ajustar los valores.

## Base de datos

Se utiliza SQL Server. El seed de datos se aplica automáticamente al arrancar si la base de datos está vacía.

## Tests

```bash
dotnet test
```