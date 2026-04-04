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
dotnet ef migrations add <NombreMigracion> --project Web
dotnet ef database update --project Web
dotnet ef database update <MigracionAnterior> --project Web  # revertir
```

## Architecture

El proyecto sigue **Arquitectura Hexagonal (Ports & Adapters)** en tres capas:

- **Domain** — Entidades puras sin dependencias externas (`Task`, `TaskTimeDetail`, `Project`, `StatusTask`)
- **Application** — Interfaces (ports) para repositorios, servicios y casos de uso; también contiene los DTOs de request/response
- **Web** — Adaptadores (implementaciones), controladores, DbContext, configuración DI y migraciones

### Flujo de dependencias

```
Web/Controllers → Application/Ports/UseCases → Web/Infrastructure/Adapters/UseCases
                                                  ↳ Application/Ports/Repositories → Web/Infrastructure/Adapters/Repositories
                                                  ↳ Application/Ports/Services    → Web/Infrastructure/Adapters/Services
```

### Casos de uso actuales

| Interface | Implementación | Descripción |
|---|---|---|
| `IStartTaskCommand` | `StartTaskCommandImpl` | Inicia sesión de tarea, cierra la anterior si existe |
| `IEndTaskSessionCommand` | `EndTaskSessionCommandImpl` | Cierra la sesión activa y sube time entry a OpenProject |
| `IAddTimeEntry` | `AddTimeEntryImpl` | Llama a la API de OpenProject para registrar horas |
| `IListsWorkPackagesCommand` | `ListsWorkPackagesCommandImpl` | Consulta work packages desde OpenProject |

### Integración con OpenProject

El sistema integra con una instancia local de OpenProject (`http://localhost:8080`) para:
- Obtener work packages, proyectos, actividades y estados
- Publicar time entries

La autenticación es Basic Auth con API key, configurada en `appsettings.json` bajo `OpenProjectConfig`. El `HttpClient` nombrado `"OpenProjectClient"` se registra en `HttpClientExtensions`.

### Persistencia local

- **ORM:** Entity Framework Core 8 con SQL Server
- **DbContext:** `TrackingTasksDbContext` en `Web/Infrastructure/DataAccess/`
- **Configuración de entidades:** `Web/Infrastructure/DataAccess/Configurations/`
- Comportamiento por defecto: **NoTracking** + **SplitQuery**
- Conexión: SQL Server local con Windows Authentication (`TrackingTasksDb`)

### Registro de dependencias

Todo el DI se configura en `Web/Infrastructure/Config/Extensions/`:
- `ServicesExtensions.cs` — casos de uso, servicios y repositorios (todos `Scoped`)
- `DbContextExtensions.cs` — EF Core
- `HttpClientExtensions.cs` — HttpClient con auth para OpenProject

### Manejo de errores

Middleware global en `Web/Infrastructure/Config/Middleware/GlobalExceptionHandler.cs`, devuelve respuestas en formato `ProblemDetails`.

## Entidades de dominio clave

- **Task** — entidad central; `WorkPackageId` es la PK (no identity, viene de OpenProject); tiene método `GetTotalHoursWorked()`
- **TaskTimeDetail** — registra intervalos de tiempo (`StartTime`/`EndTime`) por tarea; `Uploaded` indica si ya fue enviado a OpenProject
- **StatusTask** — estado de la tarea; `IsClosed` controla si acepta nuevas sesiones

## Descripción del proyecto
Software que permite comenzar y terminar sesiones de trabajo a una tarea especifica de manera dinamica, sin tener que crear la entrada de tiempo 
de manera manual en el sistema OpenProject.

- **Problema que resuelve**: Comenzar y terminar una tarea registrada en el open project y acordarse de las horas que se invirtio a partir de la hora que se comenzo o anotar manualmente en cualquier lado la hora en la que se comienza una tarea y al terminarla calcular las horas manualmente y registrar manualmente la entrada de tiempo
- **Como funciona (o pretende funcionar)**: que el usuario pueda elegir una de sus tareas asiganadas del open project y que con un boton le de comenzar session y cuando la finalize en otro boton le de finalizar session y el sistema automaticamente calcula las horas invertidas y registra la entrada de tiempo en open project.