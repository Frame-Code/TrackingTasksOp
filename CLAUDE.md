# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

```bash
# Build
dotnet build TrackingTasksOp.sln

# Run (desde la raíz)
dotnet run --project Web/Web.csproj

# Swagger UI disponible en http://localhost:5266/swagger

# Migraciones EF Core (ejecutar desde la raíz del repo)
# Nota: las migraciones viven en Infrastructure, pero el startup-project es Web
dotnet ef migrations add <NombreMigracion> --project Infrastructure --startup-project Web
dotnet ef database update --project Infrastructure --startup-project Web
dotnet ef database update <MigracionAnterior> --project Infrastructure --startup-project Web  # revertir
```

## Architecture

El proyecto sigue **Arquitectura Hexagonal (Ports & Adapters)** distribuida en **4 proyectos** independientes:

- **Domain** — Entidades puras sin dependencias externas. Contiene:
  - `Domain/Entities/TrackingTasksEntities/` — entidades propias del dominio (`Task`, `TaskTimeDetail`, `Project`, `StatusTask`, `MigrationData`)
  - `Domain/Entities/OpenProjectEntities/` — DTOs que mapean las respuestas de la API de OpenProject (`WorkPackage`, `Project`, `Status`, `Activity`, `User`, etc.)
- **Application** — Referencia solo a `Domain`. Contiene interfaces (ports) y DTOs de request/response. NO contiene implementaciones.
  - `Application/Ports/Repositories/`, `Application/Ports/Services/`, `Application/Ports/UseCases/`
  - `Application/Dto/` (organizado por feature: `Tasks`, `WorkPackages`, `TimeEntry`, `Projects`, `Conversation`)
- **Infrastructure** — Referencia a `Application` + `Domain`. Contiene **todos los adaptadores e implementaciones** (EF Core, HttpClient, servicios externos, etc.).
  - `Infrastructure/Adapters/Repositories/`, `Infrastructure/Adapters/Services/`, `Infrastructure/Adapters/UseCases/`
  - `Infrastructure/DataAccess/` — `TrackingTasksDbContext` y `Configurations/`
  - `Infrastructure/Migrations/` — migraciones EF Core
  - `Infrastructure/Settings/` — clases POCO para `IOptions<T>` (`OpenProjectSettings`, `RedisSettings`, `GeminiSettings`, `OllamaSettings`, `GroqSettings`)
  - `Infrastructure/Extensions/` — métodos de extensión de DI (`ServicesExtensions`, `HttpClientExtensions`, `DbContextExtensions`, `DatabaseExtensions`, `GoogleClientsExtensions`, `TimeExtensions`)
- **Web** — Referencia a los tres anteriores. Es la única capa con dependencia HTTP (`Microsoft.NET.Sdk.Web`). Contiene únicamente:
  - `Web/Controllers/`
  - `Web/Middleware/` — `GlobalExceptionHandler`
  - `Web/Extensions/` — extensiones puramente HTTP (`CorsExtensions`, `InitializeExtensions`)
  - `Web/Program.cs`

### Flujo de dependencias

```
Domain ◄─── Application ◄─── Infrastructure ◄─── Web
```

Las dependencias son estrictamente unidireccionales. `Domain` no referencia a nadie. `Web` solo invoca métodos de extensión definidos en `Infrastructure/Extensions/` y `Web/Extensions/`.

```
Web/Controllers → Application/Ports/UseCases → Infrastructure/Adapters/UseCases
                                                  ↳ Application/Ports/Repositories → Infrastructure/Adapters/Repositories
                                                  ↳ Application/Ports/Services    → Infrastructure/Adapters/Services
```

### Casos de uso actuales

| Interface | Implementación | Descripción |
|---|---|---|
| `IStartTaskCommand` | `StartTaskCommandImpl` | Inicia sesión de tarea, cierra la anterior si existe |
| `IEndTaskSessionCommand` | `EndTaskSessionCommandImpl` | Cierra la sesión activa y sube time entry a OpenProject |
| `IAddTimeEntry` | `AddTimeEntryImpl` | Llama a la API de OpenProject para registrar horas |
| `IListsWorkPackagesCommand` | `ListsWorkPackagesCommandImpl` | Consulta work packages desde OpenProject |
| `ICreateWorkPackageCommand` | `CreateWorkPackageCommandImpl` | Crea un work package en OpenProject |
| `IUpdateWorkPackageCommand` | `UpdateWorkPackageCommandImpl` | Actualiza un work package existente |

### Servicios actuales

| Interface | Implementación | Descripción |
|---|---|---|
| `IStatusOpService` | `StatusOpServiceImpl` | Lee estados desde OpenProject |
| `IProjectOpService` | `ProjectOpServiceImpl` | Lee proyectos desde OpenProject |
| `IActivityOpService` | `ActivityOpServiceImpl` | Lee tipos de actividad desde OpenProject |
| `IUserOpService` | `UserOpServiceImpl` | Lee información de usuario desde OpenProject |
| `IGeminiIntentService` | `GroqIntentService` (default) | Servicio de detección de intenciones; existen impl alternativas: `GeminiIntentService`, `OllamaIntentService`, `GoogleAIStudioIntentService` |
| `IConversationContextService` | `RedisConversationService` | Persiste contexto de conversación del bot en Redis |

### Integración con OpenProject

El sistema integra con una instancia de OpenProject (por defecto `http://localhost:8080`) para:
- Obtener work packages, proyectos, actividades, estados y usuarios
- Crear y actualizar work packages
- Publicar time entries

La autenticación actual es Basic Auth con API key, configurada en `appsettings.json` bajo `OpenProjectSettings`. El `HttpClient` nombrado se registra en `Infrastructure/Extensions/HttpClientExtensions.cs`. **Nota:** la implementación de autenticación está siendo rediseñada — ver `AUTH_DESIGN.md` para el plan de migración a auth multi-usuario (Identity local + OAuth).

### Bot e integración con LLMs

El proyecto tiene un `BotController` (en `Web/Controllers/`) que delega en servicios de IA para detección de intenciones y manejo de conversación:
- **Groq** (default), **Gemini**, **Ollama**, **Google AI Studio** — implementaciones intercambiables de `IGeminiIntentService`
- **Redis** — almacenamiento del contexto conversacional vía `IConversationContextService` → `RedisConversationService`. La configuración está en `RedisSettings`.

### Persistencia local

- **ORM:** Entity Framework Core 8 con SQL Server
- **DbContext:** `TrackingTasksDbContext` en `Infrastructure/DataAccess/`
- **Configuración de entidades:** `Infrastructure/DataAccess/Configurations/`
- Comportamiento por defecto: **NoTracking** + **SplitQuery**
- Conexión: SQL Server local con Windows Authentication (`TrackingTasksDb`)

### Registro de dependencias

Todo el DI se configura mediante **métodos de extensión expuestos por `Infrastructure`** que `Web/Program.cs` invoca:

- `Infrastructure/Extensions/ServicesExtensions.cs` — `AddServices()` registra Settings, casos de uso, servicios y repositorios (todos `Scoped`); inicializa el cliente Redis como `Singleton`
- `Infrastructure/Extensions/DbContextExtensions.cs` — `AddDbContext()` registra EF Core
- `Infrastructure/Extensions/HttpClientExtensions.cs` — `AddHttpClients()` registra el `HttpClient` de OpenProject (con Basic Auth) y el de Groq (con Bearer)
- `Infrastructure/Extensions/DatabaseExtensions.cs` — utilidades de inicialización/migración
- `Infrastructure/Extensions/GoogleClientsExtensions.cs` — opcional, inyecta clientes de Google Cloud (actualmente comentado)
- `Web/Extensions/CorsExtensions.cs` — `ConfigureCors()` (esta sí vive en Web porque es puramente HTTP)
- `Web/Extensions/InitializeExtensions.cs` — `InitializeAsync()` corre setup en el arranque

### Manejo de errores

Middleware global en `Web/Middleware/GlobalExceptionHandler.cs`, devuelve respuestas en formato `ProblemDetails`.

## Entidades de dominio clave

- **Task** — entidad central; `WorkPackageId` es la PK actualmente (no identity, viene de OpenProject); tiene método `GetTotalHoursWorked()`. **Nota:** ver `AUTH_DESIGN.md` — al introducir multi-usuario, la PK pasará a ser compuesta `(UserId, WorkPackageId)`.
- **TaskTimeDetail** — registra intervalos de tiempo (`StartTime`/`EndTime`) por tarea; `Uploaded` indica si ya fue enviado a OpenProject
- **StatusTask** — estado de la tarea; `IsClosed` controla si acepta nuevas sesiones
- **Project** — proyecto local (cache de OpenProject)
- **MigrationData** — entidad de soporte para migraciones de datos

## Descripción del proyecto

Software que permite comenzar y terminar sesiones de trabajo a una tarea específica de manera dinámica, sin tener que crear la entrada de tiempo de manera manual en el sistema OpenProject.

- **Problema que resuelve**: Comenzar y terminar una tarea registrada en OpenProject y acordarse de las horas que se invirtieron a partir de la hora que se comenzó, o anotar manualmente en cualquier lado la hora en la que se comienza una tarea y al terminarla calcular las horas manualmente y registrar manualmente la entrada de tiempo.
- **Como funciona (o pretende funcionar)**: que el usuario pueda elegir una de sus tareas asignadas del OpenProject y que con un botón le dé comenzar sesión y cuando la finalice en otro botón le dé finalizar sesión, y el sistema automáticamente calcule las horas invertidas y registre la entrada de tiempo en OpenProject.
- **Distribución**: el producto está pensado para ser distribuible a cualquier organización con su propia instancia de OpenProject.

## Documentos relacionados

- `AUTH_DESIGN.md` — diseño detallado del sistema de autenticación (Identity local + OAuth contra OpenProject), modelo de datos, flujos, middleware de invalidación de API key, y roadmap de implementación en 4 fases.
