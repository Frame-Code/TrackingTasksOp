# Reporte de Tareas Diarias en Excel Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Dar al usuario un botón en el dashboard para descargar, en Excel, un reporte de las tareas en las que trabajó cada día dentro de un rango de fechas, con un total general al final.

**Architecture:** Ports & Adapters, siguiendo el patrón ya usado en el resto del repo. Un puerto nuevo `IGenerateDailyTaskReportCommand` en `Application`, implementado en `Infrastructure` (usa `ITaskRepository`/`IProjectRepository`/`IStatusTaskRepository`/`CurrentUser`, todos ya existentes), expuesto por un `ReportController` nuevo en `Web`. El frontend agrega un botón + modal al dashboard existente (`Web/wwwroot/index.html`).

**Tech Stack:** .NET 8 / ASP.NET Core, EF Core 8, ClosedXML (nuevo, MIT) para generar el `.xlsx`, xUnit + Moq para tests, Bootstrap 5 + JS vanilla (ES modules) para el frontend.

## Global Constraints

- Librería de Excel: **ClosedXML** (no EPPlus — licencia comercial fuera de plan gratuito, incompatible con que el producto se distribuya a otras organizaciones).
- Granularidad del reporte: una fila por (Tarea, Día).
- Columnas: Fecha, Proyecto, ID Tarea, Nombre, Estado, Horas.
- Fila final con el total general de horas del rango.
- Solo se reportan tareas del usuario autenticado (`CurrentUser.UserId`).
- Sesiones sin `EndTime` (en curso) se excluyen del cálculo, mismo criterio que `Task.GetTotalHoursWorked()`.
- Una sesión cuenta para el día en que **inició** (`StartTime.Date`).
- UI: sección nueva en el dashboard existente (`index.html`), no página aparte.
- Spec completo: `Docs/superpowers/specs/2026-08-01-daily-task-excel-report-design.md`.

---

### Task 1: Agregar dependencia ClosedXML

**Files:**
- Modify: `Infrastructure/Infrastructure.csproj`

**Interfaces:**
- Produces: paquete NuGet `ClosedXML` disponible para todo el proyecto `Infrastructure` (y transitivamente `Tests`, que ya referencia `Infrastructure.csproj`).

- [ ] **Step 1: Agregar el paquete**

Run: `dotnet add Infrastructure/Infrastructure.csproj package ClosedXML`

Esto agrega una línea `<PackageReference Include="ClosedXML" Version="X.Y.Z" />` dentro del `<ItemGroup>` de paquetes en `Infrastructure/Infrastructure.csproj`, con la última versión estable resuelta por NuGet en el momento de ejecutar el comando (no hardcodear un número de versión a mano).

- [ ] **Step 2: Verificar que restaura y compila**

Run: `dotnet build TrackingTasksOp.sln`
Expected: `Build succeeded.` sin errores nuevos.

- [ ] **Step 3: Commit**

```bash
git add Infrastructure/Infrastructure.csproj
git commit -m "build: add ClosedXML dependency for Excel report generation"
```

---

### Task 2: Puerto del caso de uso + lógica de agrupación por (Tarea, Día) — TDD

**Files:**
- Create: `Application/Ports/UseCases/Reports/IGenerateDailyTaskReportCommand.cs`
- Create: `Infrastructure/Adapters/UseCases/Reports/GenerateDailyTaskReportCommandImpl.cs`
- Test: `Tests/Infrastructure/Adapters/UseCases/Reports/GenerateDailyTaskReportCommandImplTests.cs`

**Interfaces:**
- Consumes: `Domain.Entities.TrackingTasksEntities.Task` (con `TasksTimeDetails`), `Domain.Entities.TrackingTasksEntities.TaskTimeDetail` (`StartTime: DateTime`, `EndTime: DateTime?`).
- Produces:
  - `IGenerateDailyTaskReportCommand.Execute(DateOnly from, DateOnly to) : Task<byte[]>` — puerto consumido por el controlador en Task 5.
  - `internal record DailyTaskReportRow(DateOnly Date, string ProjectName, int WorkPackageId, string TaskName, string StatusName, double Hours)` — consumido por `BuildWorkbook` en Task 3.
  - `internal static List<DailyTaskReportRow> GenerateDailyTaskReportCommandImpl.BuildReportRows(IEnumerable<Task> tasks, DateTime fromDate, DateTime toDateExclusive, IReadOnlyDictionary<int, string> projectNames, IReadOnlyDictionary<int, string> statusNames)` — usado por `Execute` en Task 4.

- [ ] **Step 1: Crear el puerto**

`Application/Ports/UseCases/Reports/IGenerateDailyTaskReportCommand.cs`:

```csharp
namespace Application.Ports.UseCases.Reports;

public interface IGenerateDailyTaskReportCommand
{
    Task<byte[]> Execute(DateOnly from, DateOnly to);
}
```

- [ ] **Step 2: Escribir los tests de `BuildReportRows` (deben fallar)**

`Tests/Infrastructure/Adapters/UseCases/Reports/GenerateDailyTaskReportCommandImplTests.cs`:

```csharp
using Infrastructure.Adapters.UseCases.Reports;
using Task = Domain.Entities.TrackingTasksEntities.Task;
using TaskTimeDetail = Domain.Entities.TrackingTasksEntities.TaskTimeDetail;

namespace Tests.Infrastructure.Adapters.UseCases.Reports;

public class GenerateDailyTaskReportCommandImplTests
{
    private static Task BuildTask(int workPackageId, string name, int projectId, int statusId, params TaskTimeDetail[] details) => new()
    {
        WorkPackageId = workPackageId,
        UserId = "user-1",
        Name = name,
        ProjectId = projectId,
        StatusTaskId = statusId,
        TasksTimeDetails = details.ToList()
    };

    private static readonly Dictionary<int, string> ProjectNames = new() { [1] = "eProduction", [2] = "Otro Proyecto" };
    private static readonly Dictionary<int, string> StatusNames = new() { [1] = "In Progress", [2] = "Closed" };

    [Fact]
    public void BuildReportRows_MultipleSessionsSameTaskSameDay_SumsHours()
    {
        var task = BuildTask(101, "Tarea A", 1, 1,
            new TaskTimeDetail { StartTime = new DateTime(2026, 8, 3, 8, 0, 0), EndTime = new DateTime(2026, 8, 3, 10, 0, 0) },
            new TaskTimeDetail { StartTime = new DateTime(2026, 8, 3, 11, 0, 0), EndTime = new DateTime(2026, 8, 3, 12, 30, 0) });

        var rows = GenerateDailyTaskReportCommandImpl.BuildReportRows(
            [task], new DateTime(2026, 8, 1), new DateTime(2026, 9, 1), ProjectNames, StatusNames);

        Assert.Single(rows);
        Assert.Equal(new DateOnly(2026, 8, 3), rows[0].Date);
        Assert.Equal(3.5, rows[0].Hours);
        Assert.Equal("eProduction", rows[0].ProjectName);
        Assert.Equal(101, rows[0].WorkPackageId);
        Assert.Equal("In Progress", rows[0].StatusName);
    }

    [Fact]
    public void BuildReportRows_OpenSession_IsExcluded()
    {
        var task = BuildTask(101, "Tarea A", 1, 1,
            new TaskTimeDetail { StartTime = new DateTime(2026, 8, 3, 8, 0, 0), EndTime = null });

        var rows = GenerateDailyTaskReportCommandImpl.BuildReportRows(
            [task], new DateTime(2026, 8, 1), new DateTime(2026, 9, 1), ProjectNames, StatusNames);

        Assert.Empty(rows);
    }

    [Fact]
    public void BuildReportRows_SessionOutsideRange_IsExcluded()
    {
        var task = BuildTask(101, "Tarea A", 1, 1,
            new TaskTimeDetail { StartTime = new DateTime(2026, 7, 15, 8, 0, 0), EndTime = new DateTime(2026, 7, 15, 10, 0, 0) });

        var rows = GenerateDailyTaskReportCommandImpl.BuildReportRows(
            [task], new DateTime(2026, 8, 1), new DateTime(2026, 9, 1), ProjectNames, StatusNames);

        Assert.Empty(rows);
    }

    [Fact]
    public void BuildReportRows_MultipleTasksAndDays_SortsByDateThenProjectThenName()
    {
        var taskB = BuildTask(102, "Tarea B", 2, 2,
            new TaskTimeDetail { StartTime = new DateTime(2026, 8, 2, 9, 0, 0), EndTime = new DateTime(2026, 8, 2, 10, 0, 0) });
        var taskA = BuildTask(101, "Tarea A", 1, 1,
            new TaskTimeDetail { StartTime = new DateTime(2026, 8, 3, 9, 0, 0), EndTime = new DateTime(2026, 8, 3, 10, 0, 0) });

        var rows = GenerateDailyTaskReportCommandImpl.BuildReportRows(
            [taskB, taskA], new DateTime(2026, 8, 1), new DateTime(2026, 9, 1), ProjectNames, StatusNames);

        Assert.Equal(2, rows.Count);
        Assert.Equal(new DateOnly(2026, 8, 2), rows[0].Date);
        Assert.Equal(new DateOnly(2026, 8, 3), rows[1].Date);
    }

    [Fact]
    public void BuildReportRows_UnknownProjectOrStatus_FallsBackToDesconocido()
    {
        var task = BuildTask(101, "Tarea A", 99, 99,
            new TaskTimeDetail { StartTime = new DateTime(2026, 8, 3, 8, 0, 0), EndTime = new DateTime(2026, 8, 3, 9, 0, 0) });

        var rows = GenerateDailyTaskReportCommandImpl.BuildReportRows(
            [task], new DateTime(2026, 8, 1), new DateTime(2026, 9, 1), ProjectNames, StatusNames);

        Assert.Equal("Desconocido", rows[0].ProjectName);
        Assert.Equal("Desconocido", rows[0].StatusName);
    }
}
```

- [ ] **Step 3: Correr los tests y verificar que fallan**

Run: `dotnet test Tests/Tests.csproj --filter "FullyQualifiedName~GenerateDailyTaskReportCommandImplTests"`
Expected: FAIL — `GenerateDailyTaskReportCommandImpl` no existe todavía (error de compilación).

- [ ] **Step 4: Implementación mínima**

`Infrastructure/Adapters/UseCases/Reports/GenerateDailyTaskReportCommandImpl.cs`:

```csharp
using Application.Ports.Auth;
using Application.Ports.Repositories;
using Application.Ports.UseCases.Reports;
using Task = Domain.Entities.TrackingTasksEntities.Task;

namespace Infrastructure.Adapters.UseCases.Reports;

internal record DailyTaskReportRow(DateOnly Date, string ProjectName, int WorkPackageId, string TaskName, string StatusName, double Hours);

public class GenerateDailyTaskReportCommandImpl(
    ITaskRepository taskRepository,
    IProjectRepository projectRepository,
    IStatusTaskRepository statusTaskRepository,
    CurrentUser currentUser) : IGenerateDailyTaskReportCommand
{
    public async System.Threading.Tasks.Task<byte[]> Execute(DateOnly from, DateOnly to)
    {
        throw new NotImplementedException("Se completa en la Task 4 del plan.");
    }

    internal static List<DailyTaskReportRow> BuildReportRows(
        IEnumerable<Task> tasks,
        DateTime fromDate,
        DateTime toDateExclusive,
        IReadOnlyDictionary<int, string> projectNames,
        IReadOnlyDictionary<int, string> statusNames)
    {
        var rows = new List<DailyTaskReportRow>();

        foreach (var task in tasks)
        {
            var sessionsInRange = task.TasksTimeDetails
                .Where(d => d.EndTime != null && d.StartTime >= fromDate && d.StartTime < toDateExclusive);

            var byDay = sessionsInRange.GroupBy(d => d.StartTime.Date);

            foreach (var dayGroup in byDay)
            {
                var hours = Math.Round(dayGroup.Sum(d => (d.EndTime!.Value - d.StartTime).TotalHours), 2);
                if (hours <= 0) continue;

                rows.Add(new DailyTaskReportRow(
                    DateOnly.FromDateTime(dayGroup.Key),
                    projectNames.GetValueOrDefault(task.ProjectId, "Desconocido"),
                    task.WorkPackageId,
                    task.Name,
                    statusNames.GetValueOrDefault(task.StatusTaskId, "Desconocido"),
                    hours));
            }
        }

        return rows
            .OrderBy(r => r.Date)
            .ThenBy(r => r.ProjectName)
            .ThenBy(r => r.TaskName)
            .ToList();
    }
}
```

- [ ] **Step 5: Correr los tests y verificar que pasan**

Run: `dotnet test Tests/Tests.csproj --filter "FullyQualifiedName~GenerateDailyTaskReportCommandImplTests"`
Expected: `Passed! - Failed: 0, Passed: 5, Skipped: 0, Total: 5`

- [ ] **Step 6: Commit**

```bash
git add Application/Ports/UseCases/Reports/IGenerateDailyTaskReportCommand.cs Infrastructure/Adapters/UseCases/Reports/GenerateDailyTaskReportCommandImpl.cs Tests/Infrastructure/Adapters/UseCases/Reports/GenerateDailyTaskReportCommandImplTests.cs
git commit -m "feat: add daily task report port and row-grouping logic"
```

---

### Task 3: Generación del workbook Excel con ClosedXML — TDD

**Files:**
- Modify: `Infrastructure/Adapters/UseCases/Reports/GenerateDailyTaskReportCommandImpl.cs`
- Test: `Tests/Infrastructure/Adapters/UseCases/Reports/GenerateDailyTaskReportCommandImplTests.cs`

**Interfaces:**
- Consumes: `DailyTaskReportRow` (de Task 2).
- Produces: `internal static byte[] GenerateDailyTaskReportCommandImpl.BuildWorkbook(List<DailyTaskReportRow> rows)` — consumido por `Execute` en Task 4.

- [ ] **Step 1: Agregar los tests de `BuildWorkbook` (deben fallar)**

Agregar al final de la clase `GenerateDailyTaskReportCommandImplTests` (antes del `}` de cierre):

```csharp
    [Fact]
    public void BuildWorkbook_WritesHeadersRowsAndTotal()
    {
        var rows = new List<DailyTaskReportRow>
        {
            new(new DateOnly(2026, 8, 3), "eProduction", 101, "Tarea A", "In Progress", 3.5),
            new(new DateOnly(2026, 8, 4), "eProduction", 102, "Tarea B", "Closed", 2.0)
        };

        var bytes = GenerateDailyTaskReportCommandImpl.BuildWorkbook(rows);

        using var ms = new MemoryStream(bytes);
        using var workbook = new ClosedXML.Excel.XLWorkbook(ms);
        var ws = workbook.Worksheet(1);

        Assert.Equal("Fecha", ws.Cell(1, 1).GetString());
        Assert.Equal("Proyecto", ws.Cell(1, 2).GetString());
        Assert.Equal("ID Tarea", ws.Cell(1, 3).GetString());
        Assert.Equal("Nombre", ws.Cell(1, 4).GetString());
        Assert.Equal("Estado", ws.Cell(1, 5).GetString());
        Assert.Equal("Horas", ws.Cell(1, 6).GetString());

        Assert.Equal("2026-08-03", ws.Cell(2, 1).GetString());
        Assert.Equal("eProduction", ws.Cell(2, 2).GetString());
        Assert.Equal(101, ws.Cell(2, 3).GetValue<int>());
        Assert.Equal("Tarea A", ws.Cell(2, 4).GetString());
        Assert.Equal("In Progress", ws.Cell(2, 5).GetString());
        Assert.Equal(3.5, ws.Cell(2, 6).GetValue<double>());

        Assert.Equal("Total", ws.Cell(4, 5).GetString());
        Assert.Equal(5.5, ws.Cell(4, 6).GetValue<double>());
    }

    [Fact]
    public void BuildWorkbook_NoRows_WritesHeadersOnlyWithZeroTotal()
    {
        var bytes = GenerateDailyTaskReportCommandImpl.BuildWorkbook([]);

        using var ms = new MemoryStream(bytes);
        using var workbook = new ClosedXML.Excel.XLWorkbook(ms);
        var ws = workbook.Worksheet(1);

        Assert.Equal("Fecha", ws.Cell(1, 1).GetString());
        Assert.Equal("Total", ws.Cell(2, 5).GetString());
        Assert.Equal(0d, ws.Cell(2, 6).GetValue<double>());
    }
```

- [ ] **Step 2: Correr los tests y verificar que fallan**

Run: `dotnet test Tests/Tests.csproj --filter "FullyQualifiedName~GenerateDailyTaskReportCommandImplTests"`
Expected: FAIL — `BuildWorkbook` no existe todavía (error de compilación).

- [ ] **Step 3: Implementar `BuildWorkbook`**

Agregar dentro de `GenerateDailyTaskReportCommandImpl` (junto a `BuildReportRows`), y agregar `using ClosedXML.Excel;` al inicio del archivo:

```csharp
using ClosedXML.Excel;
```

```csharp
    internal static byte[] BuildWorkbook(List<DailyTaskReportRow> rows)
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Reporte");

        string[] headers = ["Fecha", "Proyecto", "ID Tarea", "Nombre", "Estado", "Horas"];
        for (var i = 0; i < headers.Length; i++)
        {
            var cell = ws.Cell(1, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
        }

        var row = 2;
        foreach (var r in rows)
        {
            ws.Cell(row, 1).Value = r.Date.ToString("yyyy-MM-dd");
            ws.Cell(row, 2).Value = r.ProjectName;
            ws.Cell(row, 3).Value = r.WorkPackageId;
            ws.Cell(row, 4).Value = r.TaskName;
            ws.Cell(row, 5).Value = r.StatusName;
            ws.Cell(row, 6).Value = r.Hours;
            row++;
        }

        ws.Cell(row, 5).Value = "Total";
        ws.Cell(row, 5).Style.Font.Bold = true;
        ws.Cell(row, 6).Value = rows.Sum(r => r.Hours);
        ws.Cell(row, 6).Style.Font.Bold = true;

        ws.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
    }
```

- [ ] **Step 4: Correr los tests y verificar que pasan**

Run: `dotnet test Tests/Tests.csproj --filter "FullyQualifiedName~GenerateDailyTaskReportCommandImplTests"`
Expected: `Passed! - Failed: 0, Passed: 7, Skipped: 0, Total: 7`

- [ ] **Step 5: Commit**

```bash
git add Infrastructure/Adapters/UseCases/Reports/GenerateDailyTaskReportCommandImpl.cs Tests/Infrastructure/Adapters/UseCases/Reports/GenerateDailyTaskReportCommandImplTests.cs
git commit -m "feat: generate xlsx workbook for daily task report"
```

---

### Task 4: Orquestación de `Execute()` + registro en DI — TDD

**Files:**
- Modify: `Infrastructure/Adapters/UseCases/Reports/GenerateDailyTaskReportCommandImpl.cs`
- Modify: `Infrastructure/Extensions/ServicesExtensions.cs`
- Test: `Tests/Infrastructure/Adapters/UseCases/Reports/GenerateDailyTaskReportCommandImplTests.cs`

**Interfaces:**
- Consumes: `ITaskRepository.GetAllAsync(Expression<Func<Task,bool>>?, bool)`, `IProjectRepository.GetByIdAsync(int, bool)`, `IStatusTaskRepository.GetByIdAsync(int, bool)`, `CurrentUser.UserId`.
- Produces: `IGenerateDailyTaskReportCommand` registrado en DI, listo para inyectar en el controlador (Task 5).

- [ ] **Step 1: Agregar los tests de `Execute` (deben fallar)**

Agregar al inicio de `GenerateDailyTaskReportCommandImplTests` (junto a los demás `using`) y al final de la clase:

```csharp
using System.Linq.Expressions;
using Application.Ports.Auth;
using Application.Ports.Repositories;
using Moq;
```

```csharp
    private class FakeCurrentUser(string? userId) : CurrentUser
    {
        public override string? UserId => userId;
        public override bool IsAuthenticated => userId != null;
        public override string? OpenProjectInstanceUrl => "http://localhost:8080";
        public override int? OpenProjectInstanceId => 1;
        public override int? OpenProjectUserId => 1;
    }

    [Fact]
    public async System.Threading.Tasks.Task Execute_FromAfterTo_ThrowsValidationException()
    {
        var repoMock = new Mock<ITaskRepository>();
        var projectRepoMock = new Mock<IProjectRepository>();
        var statusRepoMock = new Mock<IStatusTaskRepository>();
        var command = new GenerateDailyTaskReportCommandImpl(
            repoMock.Object, projectRepoMock.Object, statusRepoMock.Object, new FakeCurrentUser("user-1"));

        await Assert.ThrowsAsync<System.ComponentModel.DataAnnotations.ValidationException>(
            () => command.Execute(new DateOnly(2026, 8, 10), new DateOnly(2026, 8, 1)));
    }

    [Fact]
    public async System.Threading.Tasks.Task Execute_NoAuthenticatedUser_ThrowsUnauthorizedAccessException()
    {
        var repoMock = new Mock<ITaskRepository>();
        var projectRepoMock = new Mock<IProjectRepository>();
        var statusRepoMock = new Mock<IStatusTaskRepository>();
        var command = new GenerateDailyTaskReportCommandImpl(
            repoMock.Object, projectRepoMock.Object, statusRepoMock.Object, new FakeCurrentUser(null));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => command.Execute(new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 10)));
    }

    [Fact]
    public async System.Threading.Tasks.Task Execute_HappyPath_ReturnsWorkbookWithResolvedNames()
    {
        var task = BuildTask(101, "Tarea A", 1, 1,
            new TaskTimeDetail { StartTime = new DateTime(2026, 8, 3, 8, 0, 0), EndTime = new DateTime(2026, 8, 3, 10, 0, 0) });

        var repoMock = new Mock<ITaskRepository>();
        repoMock.Setup(r => r.GetAllAsync(It.IsAny<Expression<Func<Task, bool>>>(), It.IsAny<bool>()))
            .ReturnsAsync([task]);

        var projectRepoMock = new Mock<IProjectRepository>();
        projectRepoMock.Setup(r => r.GetByIdAsync(1, It.IsAny<bool>()))
            .ReturnsAsync(new Domain.Entities.TrackingTasksEntities.Project { Id = 1, Name = "eProduction" });

        var statusRepoMock = new Mock<IStatusTaskRepository>();
        statusRepoMock.Setup(r => r.GetByIdAsync(1, It.IsAny<bool>()))
            .ReturnsAsync(new Domain.Entities.TrackingTasksEntities.StatusTask { Id = 1, Name = "In Progress" });

        var command = new GenerateDailyTaskReportCommandImpl(
            repoMock.Object, projectRepoMock.Object, statusRepoMock.Object, new FakeCurrentUser("user-1"));

        var bytes = await command.Execute(new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31));

        Assert.NotEmpty(bytes);
        using var ms = new MemoryStream(bytes);
        using var workbook = new ClosedXML.Excel.XLWorkbook(ms);
        var ws = workbook.Worksheet(1);
        Assert.Equal("eProduction", ws.Cell(2, 2).GetString());
        Assert.Equal("Tarea A", ws.Cell(2, 4).GetString());
        Assert.Equal("In Progress", ws.Cell(2, 5).GetString());

        repoMock.Verify(r => r.GetAllAsync(It.IsAny<Expression<Func<Task, bool>>>(), It.IsAny<bool>()), Times.Once);
    }
```

- [ ] **Step 2: Correr los tests y verificar que fallan**

Run: `dotnet test Tests/Tests.csproj --filter "FullyQualifiedName~GenerateDailyTaskReportCommandImplTests"`
Expected: FAIL — `Execute` lanza `NotImplementedException` en vez del comportamiento esperado.

- [ ] **Step 3: Implementar `Execute`**

Reemplazar el cuerpo de `Execute` en `Infrastructure/Adapters/UseCases/Reports/GenerateDailyTaskReportCommandImpl.cs` (agregar `using System.ComponentModel.DataAnnotations;` al inicio del archivo):

```csharp
using System.ComponentModel.DataAnnotations;
```

```csharp
    public async System.Threading.Tasks.Task<byte[]> Execute(DateOnly from, DateOnly to)
    {
        if (from > to)
            throw new ValidationException("La fecha 'from' no puede ser posterior a 'to'.");

        var userId = currentUser.UserId;
        if (string.IsNullOrEmpty(userId))
            throw new UnauthorizedAccessException("Usuario no autenticado.");

        var fromDate = from.ToDateTime(TimeOnly.MinValue);
        var toDateExclusive = to.ToDateTime(TimeOnly.MinValue).AddDays(1);

        var tasks = (await taskRepository.GetAllAsync(t =>
            t.UserId == userId &&
            t.TasksTimeDetails.Any(d => d.EndTime != null && d.StartTime >= fromDate && d.StartTime < toDateExclusive)))
            .ToList();

        var projectNames = new Dictionary<int, string>();
        foreach (var projectId in tasks.Select(t => t.ProjectId).Distinct())
        {
            var project = await projectRepository.GetByIdAsync(projectId);
            projectNames[projectId] = project?.Name ?? "Desconocido";
        }

        var statusNames = new Dictionary<int, string>();
        foreach (var statusId in tasks.Select(t => t.StatusTaskId).Distinct())
        {
            var status = await statusTaskRepository.GetByIdAsync(statusId);
            statusNames[statusId] = status?.Name ?? "Desconocido";
        }

        var rows = BuildReportRows(tasks, fromDate, toDateExclusive, projectNames, statusNames);
        return BuildWorkbook(rows);
    }
```

- [ ] **Step 4: Correr los tests y verificar que pasan**

Run: `dotnet test Tests/Tests.csproj --filter "FullyQualifiedName~GenerateDailyTaskReportCommandImplTests"`
Expected: `Passed! - Failed: 0, Passed: 10, Skipped: 0, Total: 10`

- [ ] **Step 5: Registrar en DI**

En `Infrastructure/Extensions/ServicesExtensions.cs`, agregar el `using` y la línea de registro junto al resto de casos de uso:

```csharp
using Application.Ports.UseCases.Reports;
using Infrastructure.Adapters.UseCases.Reports;
```

Dentro de `AddServices`, en la sección `//Use cases`, después de `collection.AddScoped<ILoginLocalUserCommand, LoginLocalUserCommandImpl>();`:

```csharp
        collection.AddScoped<IGenerateDailyTaskReportCommand, GenerateDailyTaskReportCommandImpl>();
```

- [ ] **Step 6: Verificar que compila todo el solution**

Run: `dotnet build TrackingTasksOp.sln`
Expected: `Build succeeded.` sin errores.

- [ ] **Step 7: Correr toda la suite de tests unitarios**

Run: `dotnet test Tests/Tests.csproj --filter "FullyQualifiedName!~Tests.Integration"`
Expected: todos los tests pasan (el conteo total sube en 10 respecto al que había antes de este plan).

- [ ] **Step 8: Commit**

```bash
git add Infrastructure/Adapters/UseCases/Reports/GenerateDailyTaskReportCommandImpl.cs Infrastructure/Extensions/ServicesExtensions.cs Tests/Infrastructure/Adapters/UseCases/Reports/GenerateDailyTaskReportCommandImplTests.cs
git commit -m "feat: implement daily task report use case and register in DI"
```

---

### Task 5: Endpoint `ReportController`

**Files:**
- Create: `Web/Controllers/ReportController.cs`

**Interfaces:**
- Consumes: `IGenerateDailyTaskReportCommand.Execute(DateOnly, DateOnly)` (de Task 4).
- Produces: `GET /api/v1/report/daily-tasks?from=yyyy-MM-dd&to=yyyy-MM-dd` — consumido por el frontend en Task 7.

No lleva test dedicado: ningún otro controlador del proyecto (`TaskController`, `WorkPackageController`, etc.) tiene tests de controlador — solo se testean los casos de uso que invocan. Este controlador es un simple pass-through, igual que los demás, así que se sigue el mismo patrón. La verificación es manual (Step 3).

- [ ] **Step 1: Crear el controlador**

`Web/Controllers/ReportController.cs`:

```csharp
using Application.Ports.UseCases.Reports;
using Microsoft.AspNetCore.Mvc;

namespace Web.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class ReportController(IGenerateDailyTaskReportCommand generateDailyTaskReportCommand) : ControllerBase
{
    [HttpGet("daily-tasks")]
    public async Task<IActionResult> DailyTasks([FromQuery] DateOnly from, [FromQuery] DateOnly to)
    {
        var bytes = await generateDailyTaskReportCommand.Execute(from, to);
        var fileName = $"Reporte_Tareas_{from:yyyy-MM-dd}_{to:yyyy-MM-dd}.xlsx";
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }
}
```

No hace falta `[Authorize]` explícito: `Web/Program.cs:15` ya registra un `AuthorizeFilter()` global para todos los controladores.

- [ ] **Step 2: Verificar que compila**

Run: `dotnet build TrackingTasksOp.sln`
Expected: `Build succeeded.`

- [ ] **Step 3: Verificación manual del endpoint**

Run: `dotnet run --project Web/Web.csproj`

Con la app corriendo y una sesión iniciada (login vía `/auth.html`), abrir en el navegador o con `curl` (reusando la cookie de sesión del navegador):

```
GET http://localhost:5266/api/v1/report/daily-tasks?from=2026-08-01&to=2026-08-31
```

Expected: descarga un archivo `.xlsx` válido (o, si no hay sesiones activas en ese rango para el usuario logueado, un `.xlsx` con solo encabezados y total en 0 — se puede abrir con Excel/LibreOffice para confirmar). Sin sesión iniciada, debe responder `401`.

- [ ] **Step 4: Commit**

```bash
git add Web/Controllers/ReportController.cs
git commit -m "feat: add daily task report endpoint"
```

---

### Task 6: Frontend — botón y modal en el dashboard

**Files:**
- Modify: `Web/wwwroot/index.html`

**Interfaces:**
- Produces: elementos DOM `#reportBtn`, `#reportModal`, `#reportFromDate`, `#reportToDate`, `#reportError`, `#confirmReportBtn` — consumidos por `app.js` en Task 8.

- [ ] **Step 1: Agregar el botón en el navbar**

En `Web/wwwroot/index.html`, dentro del `<div class="d-flex align-items-center gap-2">` que contiene `navUserEmail` y `logoutBtn` (línea 28), agregar el botón de reporte antes de `logoutBtn`:

```html
                <div class="d-flex align-items-center gap-2">
                    <span id="navUserEmail" class="text-muted small d-none d-md-inline"></span>
                    <button id="reportBtn" class="btn btn-sm btn-outline-secondary" title="Descargar reporte de tareas">
                        <i class="bi bi-file-earmark-excel me-1"></i>
                        <span class="d-none d-md-inline">Reporte</span>
                    </button>
                    <button id="logoutBtn" class="btn btn-sm btn-outline-secondary" title="Cerrar sesión">
                        <i class="bi bi-box-arrow-right me-1"></i>
                        <span class="d-none d-md-inline">Salir</span>
                    </button>
                </div>
```

- [ ] **Step 2: Agregar el modal**

Después del cierre del `<!-- ── Modal: Editar fechas ── -->` (después de la línea `</div>` que cierra ese modal, línea 219 original) y antes del comentario `<!-- AI Bot Floating Button -->`, agregar:

```html
    <!-- ── Modal: Reporte de tareas diarias ── -->
    <div class="modal fade" id="reportModal" tabindex="-1">
        <div class="modal-dialog modal-sm">
            <div class="modal-content">
                <div class="modal-header border-0 pb-0">
                    <h5 class="modal-title">
                        <i class="bi bi-file-earmark-excel me-2 text-success"></i>Reporte de tareas
                    </h5>
                    <button type="button" class="btn-close" data-bs-dismiss="modal"></button>
                </div>
                <div class="modal-body">
                    <p class="text-muted small">Descarga en Excel las tareas trabajadas por día en el rango seleccionado.</p>
                    <div id="reportError" class="alert alert-danger py-2 small d-none"></div>
                    <div class="mb-3">
                        <label class="form-label fw-medium">Desde</label>
                        <input type="date" id="reportFromDate" class="form-control">
                    </div>
                    <div>
                        <label class="form-label fw-medium">Hasta</label>
                        <input type="date" id="reportToDate" class="form-control">
                    </div>
                </div>
                <div class="modal-footer border-0 pt-0">
                    <button type="button" class="btn btn-secondary" data-bs-dismiss="modal">Cancelar</button>
                    <button type="button" id="confirmReportBtn" class="btn btn-success px-4">
                        <i class="bi bi-download me-1"></i>Descargar
                    </button>
                </div>
            </div>
        </div>
    </div>

```

- [ ] **Step 3: Verificación manual**

Run: `dotnet run --project Web/Web.csproj`

Abrir `http://localhost:5266/index.html`, iniciar sesión, y confirmar que el botón "Reporte" aparece en el navbar. Como todavía no hay JS conectado (eso es Task 8), no se espera que el botón haga nada al hacer clic — solo verificar que la página carga sin errores de consola por el HTML nuevo (no hay tags mal cerrados, etc.).

- [ ] **Step 4: Commit**

```bash
git add Web/wwwroot/index.html
git commit -m "feat: add report button and modal markup to dashboard"
```

---

### Task 7: Frontend — descarga del reporte en `api.js`

**Files:**
- Modify: `Web/wwwroot/js/api.js`

**Interfaces:**
- Consumes: `GET /api/v1/report/daily-tasks?from=&to=` (de Task 5).
- Produces: `downloadDailyTaskReport(from: string, to: string): Promise<void>` — consumido por `app.js` en Task 8.

- [ ] **Step 1: Agregar la función**

Al final de `Web/wwwroot/js/api.js` (después de `postCancelSession`):

```javascript

export async function downloadDailyTaskReport(from, to) {
    const res = await fetch(`${API}/report/daily-tasks?from=${from}&to=${to}`, { credentials: 'include' });

    if (res.status === 401) {
        sessionStorage.removeItem('currentUser');
        window.location.replace('/auth.html');
        return;
    }

    if (!res.ok) {
        let msg = `Error ${res.status}`;
        try {
            const body = await res.json();
            msg = body.title || body.message || body.detail || msg;
        } catch (_) { /* ignorar errores de parseo */ }
        throw new Error(msg);
    }

    const blob = await res.blob();
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `Reporte_Tareas_${from}_${to}.xlsx`;
    document.body.appendChild(a);
    a.click();
    a.remove();
    URL.revokeObjectURL(url);
}
```

No reusa `apiFetch()` porque esa función asume que la respuesta es JSON (llama a `res.json()` internamente); acá la respuesta es binaria (`blob`).

- [ ] **Step 2: Verificación manual**

Abrir la consola del navegador en `http://localhost:5266/index.html` con sesión iniciada, y ejecutar:

```javascript
const { downloadDailyTaskReport } = await import('/js/api.js');
await downloadDailyTaskReport('2026-08-01', '2026-08-31');
```

Expected: se dispara la descarga de `Reporte_Tareas_2026-08-01_2026-08-31.xlsx` en el navegador, sin errores en consola.

- [ ] **Step 3: Commit**

```bash
git add Web/wwwroot/js/api.js
git commit -m "feat: add frontend function to download daily task report"
```

---

### Task 8: Frontend — conectar el botón, el modal y validación de fechas

**Files:**
- Modify: `Web/wwwroot/js/app.js`

**Interfaces:**
- Consumes: `downloadDailyTaskReport(from, to)` (de Task 7), `showToast(html, type)` (ya existente en `ui.js`), elementos DOM de Task 6.

- [ ] **Step 1: Agregar el import**

En `Web/wwwroot/js/app.js`, modificar el import existente de `api.js` (línea 4-7) para incluir la nueva función:

```javascript
import { fetchProjects, fetchWorkPackages, fetchActivities, fetchTask,
         postStartSession, postEndSession, fetchStatuses,
         patchWorkPackageStatus, patchWorkPackageProgress,
         patchWorkPackageDates, postCancelSession,
         downloadDailyTaskReport } from './api.js';
```

- [ ] **Step 2: Agregar las funciones del modal**

Después de la sección `// ── Modal: Editar fechas ──` (después de `bindConfirmDatesButton`, antes de `bindLoadButton`), agregar:

```javascript
// ── Modal: Reporte de tareas diarias ──────────────────────────────────────────

function openReportModal() {
    document.getElementById('reportFromDate').value = '';
    document.getElementById('reportToDate').value = '';
    document.getElementById('reportError').classList.add('d-none');

    const confirmBtn = document.getElementById('confirmReportBtn');
    confirmBtn.disabled = false;
    confirmBtn.innerHTML = '<i class="bi bi-download me-1"></i>Descargar';

    new bootstrap.Modal(document.getElementById('reportModal')).show();
}

function bindReportButton() {
    document.getElementById('reportBtn').addEventListener('click', openReportModal);
}

function bindConfirmReportButton() {
    document.getElementById('confirmReportBtn').addEventListener('click', async () => {
        const from = document.getElementById('reportFromDate').value;
        const to = document.getElementById('reportToDate').value;
        const errorBox = document.getElementById('reportError');
        errorBox.classList.add('d-none');

        if (!from || !to) {
            errorBox.textContent = 'Debes indicar ambas fechas.';
            errorBox.classList.remove('d-none');
            return;
        }
        if (from > to) {
            errorBox.textContent = 'La fecha "Desde" no puede ser posterior a "Hasta".';
            errorBox.classList.remove('d-none');
            return;
        }

        const btn = document.getElementById('confirmReportBtn');
        btn.disabled = true;
        btn.innerHTML = '<span class="spinner-border spinner-border-sm me-2"></span>Generando...';

        try {
            await downloadDailyTaskReport(from, to);
            bootstrap.Modal.getInstance(document.getElementById('reportModal'))?.hide();
            showToast('Reporte descargado correctamente.', 'success');
        } catch (e) {
            errorBox.textContent = `Error al generar el reporte: ${e.message}`;
            errorBox.classList.remove('d-none');
        } finally {
            btn.disabled = false;
            btn.innerHTML = '<i class="bi bi-download me-1"></i>Descargar';
        }
    });
}
```

- [ ] **Step 3: Registrar los event listeners en Init**

En la sección `// ── Init ──` al final del archivo, agregar junto a los demás `bind*()`:

```javascript
bindReportButton();
bindConfirmReportButton();
```

- [ ] **Step 4: Verificación manual end-to-end**

Run: `dotnet run --project Web/Web.csproj`

Con la app corriendo, iniciar sesión en `http://localhost:5266/index.html` y probar:

1. Click en "Reporte" → se abre el modal con los dos campos de fecha vacíos.
2. Click en "Descargar" sin llenar fechas → aparece el error "Debes indicar ambas fechas." sin cerrar el modal.
3. Llenar "Desde" con una fecha posterior a "Hasta" → aparece el error de validación de rango.
4. Llenar un rango válido con sesiones de trabajo conocidas (o un rango sin datos) → se descarga el `.xlsx`, el modal se cierra y aparece el toast de éxito. Abrir el archivo descargado y confirmar que las columnas y el total coinciden con lo esperado.
5. Repetir el paso 4 sin sesión iniciada (logout primero) → debe redirigir a `/auth.html` en vez de descargar un archivo corrupto.

- [ ] **Step 5: Commit**

```bash
git add Web/wwwroot/js/app.js
git commit -m "feat: wire up daily task report button and validation"
```
