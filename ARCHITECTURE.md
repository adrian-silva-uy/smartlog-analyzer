# ARCHITECTURE.md

## Diagrama de Arquitectura

```
┌─────────────────────────────────────────────────────────────────────┐
│                      SmartLog Analyzer                              │
└─────────────────────────────────────────────────────────────────────┘
                                    │
                                    ▼
┌─────────────────┐      POST /api/logs      ┌──────────────────────┐
│  Cliente / API  │────────────────────────▶│   API Layer (ASP.NET)  │
│  (Cliente/Curl) │                           │  - Ingestión de logs   │
└─────────────────┘                           │  - Listado con filtros │
                                              │  - Agregación          │
                                              │  - Análisis con IA     │
                                              └──────────────────────┘
                                                        │
                    ┌──────────────────────────────────────────────────┐
                    │                                                   │
                    ▼                                                   ▼
        ┌─────────────────────┐                               ┌──────────────────────┐
        │   Base de Datos     │                               │   Servicio de IA /   │
        │   SQL Server        │                               │   LLM (Mock / Real)  │
        │   - LogEntries      │                               │   - Resumen          │
        │   - Analyses        │                               │   - Categorías       │
        │   - AnalysisLogs    │                               │   - Sugerencias      │
        └─────────────────────┘                               └──────────────────────┘
```

## Flujo de datos

1. **Ingesta**: El cliente envía un log vía POST /api/logs. La API valida los datos con FluentValidation y los persiste en la tabla LogEntries de SQL Server.
2. **Listado**: GET /api/logs permite filtrar por servicio, nivel, rango de fechas y búsqueda textual, con paginación.
3. **Agregación**: GET /api/logs/aggregation agrupa logs por servicio o nivel dentro de un rango de fechas.
4. **Análisis IA**: POST /api/logs/analyze recibe una lista de IDs de logs, los recupera, los envía al LLM (mock o real), valida la respuesta y persiste el resultado en la tabla Analysis con sus relaciones en AnalysisLogs.
5. **Consulta de análisis**: GET /api/analysis/{id} devuelve el resultado persistido de un análisis.

## Decisiones técnicas

### 1. Arquitectura en capas separadas (Core / Infrastructure / Api)
- **Alternativa descartada**: Clean Architecture completa con capas de Application/UseCases.
- **Por qué**: Para un prototipo de 6 endpoints, una Clean Architecture completa añade complejidad innecesaria. La separación en tres capas (Core: dominio/DTOs/interfaces, Infrastructure: persistencia/LLM, Api: endpoints/middleware) ofrece separación de responsabilidades sin sobre-ingeniería.

### 2. Base de datos: SQL Server con Entity Framework Core
- **Alternativa descartada**: MongoDB, PostgreSQL, SQLite.
- **Por qué**: SQL Server es el estándar empresarial y EF Core lo integra nativamente. Los índices en ServiceName, Level y Timestamp optimizan las consultas de listado y agregación.

### 3. LLM: Mock realista por defecto, con opción de proveedor real
- **Alternativa descartada**: Usar una API de LLM real (OpenAI) directamente.
- **Por qué**: Evitar costos y dependencia de API key durante el desarrollo. El mock permite desarrollar y probar sin credenciales. Se incluye un esqueleto de OpenAILLMService listo para activar cuando se necesite.

### 4. Validación con FluentValidation
- **Alternativa descartada**: Data Annotations.
- **Por qué**: FluentValidation permite separar las reglas de negocio del modelo de datos y es más flexible para validaciones complejas.

### 5. Respuestas de error con ProblemDetails (RFC 7807)
- **Alternativa descartada**: Respuestas ad-hoc por endpoint.
- **Por qué**: Proporciona contratos de error consistentes y estandarizados.

## Compensaciones (trade-offs) conocidas

- El endpoint POST /api/logs/analyze llama al repositorio una vez por cada logId para verificar existencia. En producción se unificaría en un solo método GetByIdsAsync.
- El seed corre en un IHostedService al arrancar; si la base de datos ya tiene datos, no se vuelve a sembrar.

## Estructura del repositorio

```
smartlog-analyzer/
├── src/
│   ├── SmartLogAnalyzer.Core/        # Dominio, DTOs, interfaces, validadores
│   ├── SmartLogAnalyzer.Infrastructure/  # EF Core, repositorios, LLM, seed, DI
│   └── SmartLogAnalyzer.Api/         # Endpoints, middleware, Program.cs, Swagger
├── tests/
│   ├── SmartLogAnalyzer.UnitTests/   # Pruebas unitarias
│   └── SmartLogAnalyzer.IntegrationTests/  # Pruebas de integración
├── db/                               # Migración SQL y seed
├── Dockerfile
├── docker-compose.yml
├── .env.example
├── README.md
└── ARCHITECTURE.md
```