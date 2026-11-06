# ARCHITECTURE.md — SmartLog Analyzer

## 1. Diagrama de arquitectura (Mermaid)

```mermaid
C4Container
  title SmartLog Analyzer — Contenedores y flujo de datos

  persona(dev, "Desarrollador / Operador", "Consulta logs y análisis")

  container_db(db, "Base de datos", "SQL Server 2022 Express", "Almacena logs y resultados de análisis de IA")
  container_api(api, "API REST", ".NET 10 (ASP.NET Core minimal APIs)", "Expone endpoints /api/logs, /api/analysis, /api/health")
  container_llm(llm, "Servicio LLM", "Mock realista (por defecto) o OpenAI", "Resume logs, categoriza problemas, sugiere acciones")

  dev --> api: HTTP (JSON)
  api --> db: "SQL (EF Core, SQL Server provider)"
  api --> llm: HTTP (prompt con conjunto de logs)

  container_nginx(proxy, "Reverse proxy (opcional)", "Nginx", "Terminación TLS y enrutamiento")
  dev -.-> proxy: HTTPS
  proxy --> api: HTTP

  DB_DATA((Volumen Docker smartlog_db_data))
  db -.-> DB_DATA: persistencia
```

### Flujo de datos principal

1. **Ingesta**: `POST /api/logs` recibe un `IngestLogRequest` → se valida (FluentValidation) → se persiste `LogEntry` en SQL Server → se devuelve `201 Created` con el `LogResponse`.
2. **Consulta**: `GET /api/logs` con filtros (`serviceName`, `level`, `from`, `to`, `search`) + paginación → `PagedResult<LogResponse>`.
3. **Agregación**: `GET /api/logs/aggregation?groupBy=service|level&from=…&to=…` → conteo agrupado (consulta de agregación requerida).
4. **Análisis IA**: `POST /api/logs/analyze` con lista de `logIds` → recupera logs → llama a `ILLMService` (mock por defecto) → valida formato de respuesta → persiste `Analysis` + unión `AnalysisLogs` → devuelve `AnalysisResponse`.
5. **Obtención**: `GET /api/analysis/{id}` → devuelve el análisis persistido.
6. **Health**: `GET /api/health` → verifica `CanConnectAsync` a SQL Server.

## 2. Decisiones técnicas (con alternativas descartadas)

Para cada decisión se documenta la alternativa descartada y el porqué.

| Decisión | Alternativa descartada | Justificación |
|---|---|---|
| **Lenguaje / Framework: .NET 10 + ASP.NET Core minimal APIs** | .NET 8 LTS, ASP.NET Core con controllers, Java/Spring, Node.js | .NET 10 es el framework solicitado por el desafío. Minimal APIs reducen código repetitivo y son ideales para una API REST ligera. Los controllers añaden complejidad innecesaria para 6 endpoints. |
| **Base de datos: SQL Server (EF Core, proveedor nativo)** | PostgreSQL, MySQL, MongoDB, SQLite | SQL Server es el estándar empresarial donde opera Abstracta, tiene excelente soporte en Docker (imagen oficial `mssql/server`) y EF Core lo integra nativamente. MongoDB/PostgreSQL requerirían proveedores de terceros y no aportan ventaja para datos estructurados de logs. |
| **ORM: Entity Framework Core** | Dapper, ADO.NET puro, NHibernate | EF Core ofrece migrations, change tracking y LINQ, acelerando el desarrollo del prototipo. Dapper sería más rápido pero requeriría escribir SQL manual (menos mantenible para este alcance). |
| **Patrón: Capas separadas (Core / Infrastructure / Api)** | Clean Architecture completa, Capa única, Microservicios | Separación por responsabilidades sin sobre-ingeniería. Clean Architecture completa añadiría abstracciones excesivas para un prototipo de ~6 endpoints. Una sola capa acoplaría presentación y persistencia. |
| **LLM por defecto: Mock realista detrás de interfaz `ILLMService`** | OpenAI real con API key, modelo local, Anthropic | El desafío dice explícitamente: "No necesitás gastar dinero". El mock permite desarrollar y probar sin API key, con variables de entorno (`MOCK_LLM_DELAY_MS`, `MOCK_LLM_MALFORMED`, `MOCK_LLM_TIMEOUT`) para simular casos borde. `OpenAILLMService` está como esqueleto documentado para activar con `LLM__Provider=openai`. |
| **Validación: FluentValidation** | Data Annotations, validación manual en endpoints | FluentValidation separa reglas de negocio del endpoint, es testeable unitariamente y se integra con ASP.NET Core para devolver 400 automáticamente. Data Annotations acoplan la validación al DTO. |
| **Manejo de errores: ProblemDetails (RFC 7807) + ExceptionMiddleware centralizado** | Respuestas ad-hoc por endpoint, middleware genérico de .NET | ProblemDetails brinda contratos de error consistentes y estandarizados. El middleware centralizado evita repetir try/catch en cada endpoint y no filtra stack traces al cliente. |
| **Logging: Serilog estructurado** | `ILogger` de Microsoft.Extensions.Logging, console nativo | En un analizador de logs es incoherente no usar logging estructurado. Serilog ofrece enriquecimiento (machine name, thread id), sinks múltiples y formato JSON listo para producción. |
| **Documentación de API: Swagger/OpenAPI (Swashbuckle)** | Sin documentación, Postman collection externa | Swagger genera documentación interactiva automática desde los endpoints, facilitando pruebas y consumo. Es un extra justificado por el PDF. |
| **Despliegue: Docker + docker-compose** | Instalación directa en host, Kubernetes, Azure/AWS | `docker compose up` levanta app + DB con un solo comando (requerido). K8s es overkill para un prototipo. |
| **Seed de datos: ejecución automática al arrancar (`SeedHostedService`)** | Scripts SQL manuales, no tener seed | Garantiza que la app esté lista tras `docker compose up` sin pasos manuales (cumple el requisito de despliegue con un solo comando). Los scripts SQL también se guardan en `db/` para auditoría. |

### Motivación de no aplicar ciertos "extras opcionales"

- **Nginx reverse proxy**: descartado por ahora. Se agregaría solo si se requiere terminación TLS o enrutamiento externo; no es necesario para el prototipo interno.
- **Pipeline de CI**: se puede agregar con GitHub Actions (`dotnet test`, `docker build`). No crítico para la primera entrega.
- **Paginación / logging estructurado**: sí incluidas (paginación en `GET /api/logs`; Serilog estructurado).

## 3. Compensaciones (trade-offs) conocidas

- `POST /api/logs/analyze` llama al repositorio una vez por `logId` para verificar existencia (`ExistsAsync`), luego `GetByIdsAsync` trae todas las entidades. Es aceptable para un prototipo; en producción se unificaría en un solo método `GetByIdsAsync` que devuelva las entidades y se validaría contra los ids solicitados.
- El seed corre en un `IHostedService` al arrancar; si la base ya tiene datos, no se vuelve a sembrar (`AnyAsync`).
- El mock de LLM no persiste llamadas ni tiene caché; cada análisis genera una respuesta nueva basada en los logs recibidos.

## 4. Estructura del repositorio

```
smartlog-analyzer/
├── src/
│   ├── SmartLogAnalyzer.Core/        # Dominio, DTOs, interfaces, validadores
│   ├── SmartLogAnalyzer.Infrastructure/  # EF Core, repositorios, LLM, seed, DI
│   └── SmartLogAnalyzer.Api/         # Endpoints, middleware, Program.cs, Swagger
├── tests/
│   ├── SmartLogAnalyzer.UnitTests/   # Pruebas unitarias (InMemory)
│   └── SmartLogAnalyzer.IntegrationTests/  # Pruebas de integración (WebApplicationFactory)
├── db/                               # Migración SQL y seed
├── Dockerfile
├── docker-compose.yml
├── .env.example
├── .gitignore
├── README.md
└── SmartLogAnalyzer.slnx
```
