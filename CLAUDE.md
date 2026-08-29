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
  - `Infrastructure/Extensions/` — métodos de extensión de DI (`ServicesExtensions`, `HttpClientExtensions`, `DbContextExtensions`, `DatabaseExtensions`, `GoogleClientsExtensions`, `TimeExtensions`, `DataProtectionExtensions`)
- **Web** — Referencia a los tres anteriores. Es la única capa con dependencia HTTP (`Microsoft.NET.Sdk.Web`). Contiene únicamente:
  - `Web/Controllers/`
  - `Web/Middleware/` — `GlobalExceptionHandler`
  - `Web/Extensions/` — extensiones puramente HTTP (`CorsExtensions`, `InitializeExtensions`, `IdentityExtensions`, `RateLimitingExtensions`, `ForwardedHeadersExtensions`)
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

La autenticación es **multi-usuario y multi-tenant**: cada `ApplicationUser` está asociado a una `OpenProjectInstance` (URL propia). El `HttpClient` nombrado `OpenProjectHttpClient` se registra en `Infrastructure/Extensions/HttpClientExtensions.cs` con un `BaseAddress` placeholder; el `DelegatingHandler` `OpenProjectAuthHandler` (`Infrastructure/Adapters/Http/OpenProjectHttpHandler.cs`) reescribe la URL con la instancia del usuario autenticado y agrega `Authorization: Basic apikey:<key>` desencriptando su `LocalCredential` al vuelo vía `IApiKeyEncryptorService`. Ya no queda Basic Auth estático en `appsettings.json`. Método local (Identity + API key) implementado end-to-end; **OAuth 2.0 contra OpenProject está modelado (`OAuthCredential`) pero el flujo de login/callback aún no está implementado** — ver `AUTH_DESIGN.md`.

### Bot e integración con LLMs

El proyecto tiene un `BotController` (en `Web/Controllers/`) que delega en servicios de IA para detección de intenciones y manejo de conversación:
- **Groq** (default), **Gemini**, **Ollama**, **Google AI Studio** — implementaciones intercambiables de `IGeminiIntentService`
- **Redis** — almacenamiento del contexto conversacional vía `IConversationContextService` → `RedisConversationService`. La configuración está en `RedisSettings`.

### Persistencia local

- **ORM:** Entity Framework Core 8 con **PostgreSQL** (`Npgsql.EntityFrameworkCore.PostgreSQL`). Se migró desde SQL Server; no queda nada específico de ese motor en el código.
- **DbContext:** `TrackingTasksDbContext` en `Infrastructure/DataAccess/`
- **Configuración de entidades:** `Infrastructure/DataAccess/Configurations/`
- Comportamiento por defecto: **NoTracking** + **SplitQuery**
- Conexión: PostgreSQL (`Host=…;Database=…;Username=…;Password=…`). Hay dos bases: `TrackingTasksDb` para desarrollo y `TrackingTasksDbProduccion` para el deploy.
- **Fechas:** `ConfigureConventions` mapea todo `DateTime` a `timestamp without time zone` y le aplica `UnspecifiedDateTimeConverter`. Las dos cosas son necesarias juntas: Npgsql valida el `DateTimeKind` contra el tipo de columna en ambas direcciones, y la app mezcla `DateTime.Now` (StartTime/EndTime, que significan "reloj de pared") con `DateTime.UtcNow` (tokens OAuth, auditoría). Cubierto por `Tests/Infrastructure/DataAccess/DateTimeKindPersistenceTests.cs`.

### Registro de dependencias

Todo el DI se configura mediante **métodos de extensión** que `Web/Program.cs` invoca, en este orden: `AddTrackingDataProtection` → `AddIdentityAndAuth` → `AddHttpClients` → `AddServices` → `AddDbContext` → `ConfigureCors`.

- `Infrastructure/Extensions/ServicesExtensions.cs` — `AddServices()` registra Settings, casos de uso, servicios y repositorios (todos `Scoped`); inicializa el cliente Redis como `Singleton`
- `Infrastructure/Extensions/DbContextExtensions.cs` — `AddDbContext()` registra EF Core
- `Infrastructure/Extensions/HttpClientExtensions.cs` — `AddHttpClients()` registra el `HttpClient` de OpenProject (`OpenProjectAuthHandler` inyecta Basic Auth por usuario) y el de Groq (con Bearer)
- `Infrastructure/Extensions/DataProtectionExtensions.cs` — `AddTrackingDataProtection()` persiste el key ring en la tabla `DataProtectionKeys` de la propia base, para que viaje en el mismo backup que las API keys que descifra; opcionalmente lo cifra con un `.pfx` (ver `Docs/DataProtection.md`)
- `Infrastructure/Extensions/DatabaseExtensions.cs` — utilidades de inicialización/migración
- `Infrastructure/Extensions/GoogleClientsExtensions.cs` — opcional, inyecta clientes de Google Cloud (actualmente comentado)
- `Web/Extensions/CorsExtensions.cs` — `ConfigureCors()` (vive en Web porque es puramente HTTP)
- `Web/Extensions/IdentityExtensions.cs` — `AddIdentityAndAuth()` configura ASP.NET Core Identity (`ApplicationUser`, políticas de password/lockout desde `IdentitySettings`), el `ClaimsPrincipalFactory` y la cookie de auth (`CookieSettings`). Vive en Web, no en Infrastructure, junto con Cors por el mismo criterio ("puramente HTTP": esquema de cookie/auth pipeline).
- `Web/Extensions/InitializeExtensions.cs` — `InitializeAsync()` corre migraciones y arma el pipeline HTTP en el arranque

### Manejo de errores

Middleware global en `Web/Middleware/GlobalExceptionHandler.cs`, devuelve respuestas en formato `ProblemDetails`.

## Entidades de dominio clave

- **Task** — entidad central; PK compuesta `(UserId, WorkPackageId)` (`WorkPackageId` no es identity, viene de OpenProject; `UserId` la aísla por usuario/tenant); también tiene `OpenProjectInstanceId` para scoping multi-tenant en sus relaciones con `Project`/`StatusTask`; tiene método `GetTotalHoursWorked()`.
- **OpenProjectInstance** — representa la instancia/organización de OpenProject a la que pertenece un usuario/tarea (soporte multi-tenant, ver `AUTH_DESIGN.md`).
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

- `AUTH_DESIGN.md` — diseño y estado real del sistema de autenticación (Identity local + OAuth contra OpenProject), modelo de datos, flujos, middleware de invalidación de API key, y qué falta (OAuth) del roadmap original en 4 fases.
- `Docs/DataProtection.md` — cómo y dónde se cifran las API keys de OpenProject (Data Protection API), por qué el key ring vive en la base y no en disco, el certificado opcional que lo envuelve, y cómo respaldarlo.
- `Docs/Cuenta.md` — la vista `/settings.html`, que concentra toda la configuración (cuenta, seguridad, notificaciones, apariencia, tareas, OpenProject, asistente IA). Incluye guía para usuarios finales del 2FA con TOTP, qué pasa si se pierde el teléfono, el reset de 2FA por SQL, y las decisiones de diseño del avatar y del segundo factor. El dashboard ya no tiene sidebar: sus acciones viven en una barra superior.
- `Docs/OpenProjectEntities.md` — mapeo de las respuestas JSON de la API de OpenProject a las entidades de `Domain/Entities/OpenProjectEntities/`.
- `Docs/Bot.md` — diseño del bot conversacional (intents, adapters de LLM, acciones).
- `Docs/OpenProjectDockerBackup.md` — backup/restore de la instancia de OpenProject (servidor de testing) contra la que corre esta app en desarrollo, incluyendo migración completa servidor → máquina local.
