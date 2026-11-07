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

## Uso de IA durante el desarrollo

Durante el desarrollo utilicé herramientas de IA para:
- Generar la estructura inicial de endpoints, validadores y comentarios técnicos (ahorró tiempo de boilerplate).
- Revisar la traducción de LINQ a SQL en la consulta de agregación (`GetAggregationAsync`).
- Redactar el diagrama de arquitectura y los comentarios técnicos del código.

Decisiones que **no acepté** de la IA:
- No usé el código de la IA sin revisarlo: cada línea fue verificada y adaptada al dominio del desafío.
- No activé la integración real con OpenAI por defecto, para evitar costos y dependencia de API key; se dejó un mock realista con esqueleto (`OpenAILLMService`) listo para activar con `LLM__Provider=openai`.

## Pendientes / Qué dejé afuera

- **Autenticación y autorización:** No hay JWT ni roles. Cualquiera puede ingestar logs. Se resolvería con ASP.NET Core Identity o Azure AD B2C.
- **Rate limiting:** No hay límite de peticiones por cliente. Se agregaría Middleware de rate limiting.
- **Pruebas con Testcontainers:** Los tests de integración usan InMemory Database. En producción se usarían tests contra SQL Server real en contenedor.
- **Secretos en Key Vault:** Las cadenas de conexión y API keys van en `appsettings.json` / `.env`; en producción se moverían a Azure Key Vault o AWS Secrets Manager.
- **Índices compuestos:** Solo hay índices simples en `ServiceName`, `Level` y `Timestamp`. Para agregaciones masivas se agregaría un índice compuesto `(ServiceName, Timestamp)`.
- **Salud de BD más profunda:** `/api/health` solo hace `CanConnectAsync`. Se podrían agregar verificaciones de disco, memoria y latencia de consulta.

## Nota sobre scripts de migración y la entrega por PIA

El PDF original pide "Scripts de migración y seed con datos de ejemplo" en una carpeta `db/`. Sin embargo, el validador del portal de PIA rechaza archivos `.sql` durante la subida del ZIP. Por esta razón, los archivos `.sql` de la carpeta `db/` se excluyeron del archivo ZIP de entrega.

Los scripts de migración **no se perdieron**: están disponibles en el código fuente como migraciones de Entity Framework Core (`src/SmartLogAnalyzer.Infrastructure/Data/Migrations/`) y en el repositorio de GitHub (`https://github.com/adrian-silva-uy/smartlog-analyzer.git`). Así se cumple el requisito funcional sin violar las restricciones de subida del portal.

## Tests

```bash
dotnet test
```