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
- Docker Desktop con Docker Compose V2 (para levantar la solución completa)
- SQL Server 2022 Express o una instancia local de SQL Server (solo para ejecución sin Docker)

## Ejecución con Docker

Desde la raíz del repositorio:

```powershell
docker compose up
```

No es necesario crear `.env` para la primera ejecución: Compose utiliza una contraseña local de demostración que cumple la política de complejidad de SQL Server. Para personalizarla, copiar `.env.example` como `.env` y modificar sus valores.

Compose construye la imagen de la API, inicia SQL Server, espera a que el healthcheck de la base de datos sea correcto y recién entonces inicia la API. La aplicación aplica las migraciones de Entity Framework Core y ejecuta el seed al arrancar.

Comandos útiles:

```powershell
# Ejecutar en segundo plano
docker compose up -d

# Ver el estado de los servicios
docker compose ps

# Ver los logs de todos los servicios
docker compose logs -f

# Detener los servicios conservando los datos
docker compose down

# Detener y eliminar también el volumen de SQL Server
docker compose down -v
```

`docker compose down -v` elimina permanentemente los datos persistidos del volumen `smartlog_db_data`.

### Puertos

| Servicio | Puerto del host | Uso |
|---|---:|---|
| API | 8080 | Acceso HTTP a SmartLog Analyzer (`http://localhost:8080`) |
| SQL Server | 1433 | Acceso a SQL Server desde el host |

Swagger está habilitado únicamente en el entorno `Development`, por lo que no se expone al ejecutar el Compose con la configuración predeterminada `Production`.

### Red Docker

Compose crea una red bridge para los servicios. La API se conecta a SQL Server mediante el nombre DNS interno del servicio, `db:1433`; no utiliza `localhost` para la comunicación entre contenedores.

### Variables de entorno

Las variables se leen desde `.env` si existe. `.env.example` es una plantilla y no contiene credenciales reales.

| Variable | Obligatoria | Valor predeterminado | Descripción |
|---|---|---|---|
| `DB_PASSWORD` | No | `LocalDev_2026!Change` | Contraseña local de la cuenta `sa` de SQL Server. Reemplazar en entornos reales. |
| `LLM__Provider` | No | `mock` | Proveedor de IA: `mock` u `openai`. |
| `LLM__ApiKey` | No | Vacío | Clave del proveedor real; no es necesaria con `mock`. |
| `LLM__BaseUrl` | No | `https://api.openai.com/v1` | URL base del proveedor de IA. |
| `ASPNETCORE_ENVIRONMENT` | No | `Production` | Entorno de ejecución de ASP.NET Core. |

Para configurar valores propios:

```powershell
Copy-Item .env.example .env
docker compose up
```

El archivo `.env` está excluido de Git y de la imagen mediante `.gitignore` y `.dockerignore`.

## Ejecución local sin Docker

Se necesita una instancia accesible de SQL Server y configurar la cadena `ConnectionStrings:DefaultConnection` en `src/SmartLogAnalyzer.Api/appsettings.Development.json` o mediante variables de entorno. Por ejemplo, en PowerShell:

```powershell
$env:ConnectionStrings__DefaultConnection = "Server=localhost,1433;Database=SmartLogAnalyzer;User Id=sa;Password=LocalDev_2026!Change;TrustServerCertificate=True"
$env:ASPNETCORE_ENVIRONMENT = "Development"
dotnet run --project src/SmartLogAnalyzer.Api
```

La aplicación queda disponible en el puerto indicado por `launchSettings.json` (habitualmente `http://localhost:5000`). En `Development`, Swagger queda disponible en la raíz `/`.

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

Consultar la tabla de variables de la sección [Ejecución con Docker](#variables-de-entorno). Para Docker, la copia de `.env.example` es opcional porque Compose define valores locales predeterminados.

## Base de datos, migraciones y seed

Se utiliza SQL Server. Al arrancar, `SeedHostedService` espera a que la conexión esté disponible, ejecuta `Database.MigrateAsync()` para aplicar las migraciones de Entity Framework Core y llama a `SeedData.InitializeAsync()`. El seed agrega datos de ejemplo únicamente cuando la tabla de logs está vacía, por lo que reiniciar la API no duplica los registros.

El flujo es el siguiente:

1. SQL Server arranca en el servicio `db`.
2. Docker Compose espera el healthcheck de SQL Server.
3. La API inicia después de que `db` está saludable.
4. Entity Framework Core crea o actualiza el esquema mediante las migraciones.
5. El seed inserta los datos de ejemplo si todavía no existen.

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
dotnet test SmartLogAnalyzer.slnx --verbosity normal```