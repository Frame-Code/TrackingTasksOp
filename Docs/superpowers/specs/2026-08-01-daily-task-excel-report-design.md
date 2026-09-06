# Reporte de tareas diarias en Excel — Diseño

**Fecha:** 2026-08-01
**Estado:** Aprobado (pendiente de plan de implementación)

## 1. Contexto y problema

El usuario necesita, al cierre de mes, poder sacar un reporte de las tareas en las que trabajó día a día durante su jornada laboral, filtrado por un rango de fechas (`between`), descargable en Excel. Hoy no existe ningún mecanismo de reporting: no hay controlador de reportes, ni librería de generación de Excel en el proyecto.

Los datos ya existen en el modelo local:
- `Task` (`Domain/Entities/TrackingTasksEntities/Task.cs`) — tarea local (cache de OpenProject), con `UserId`, `ProjectId` → `Project`, `StatusTaskId` → `StatusTask`, y su colección `TasksTimeDetails`.
- `TaskTimeDetail` (`Domain/Entities/TrackingTasksEntities/TaskTimeDetail.cs`) — cada sesión de trabajo iniciada/finalizada (`StartTime`/`EndTime`), con `UserId` propio y `GetHoursWorked()` (`null` si la sesión sigue abierta).

## 2. Alcance

Incluye:
- Un nuevo endpoint que genera y devuelve un `.xlsx` con el detalle de tareas trabajadas por día, para el usuario autenticado, en un rango de fechas dado.
- Un botón + selector de fechas nuevo en el dashboard existente (`Web/wwwroot/index.html`) para disparar la descarga.

No incluye (fuera de alcance de este spec):
- Reportes de otros usuarios (solo el usuario autenticado ve sus propias tareas — mismo criterio que el resto de los endpoints existentes vía `ICurrentUser`).
- Reportes en otros formatos (PDF, CSV) — se puede agregar después si hace falta, no se diseña ahora.
- Programar el envío automático del reporte (ej. por email a fin de mes) — no fue pedido.

## 3. Decisión técnica: librería de Excel

**ClosedXML** (NuGet, licencia MIT).

Se descartó **EPPlus**: desde la v5 su licencia es comercial fuera de un plan "NonCommercial" limitado, y el `CLAUDE.md` del proyecto indica que el producto está pensado para distribuirse a otras organizaciones — adoptar EPPlus arrastraría un problema de licenciamiento a cada instalación del producto.

Se descartó escribir el `.xlsx` a mano (formato OOXML/zip) por ser sobre-ingeniería para un reporte simple de una sola hoja.

## 4. Arquitectura (Ports & Adapters, siguiendo el patrón existente)

```
Web/Controllers/ReportController.cs
  └─ Application/Ports/UseCases/Reports/IGenerateDailyTaskReportCommand.cs
       └─ Infrastructure/Adapters/UseCases/Reports/GenerateDailyTaskReportCommandImpl.cs
            ├─ Application/Ports/Repositories/ITaskRepository.cs (ya existe)
            └─ Application/Ports/Auth/CurrentUser.cs (ya existe)
```

### 4.1. Puerto nuevo

`Application/Ports/UseCases/Reports/IGenerateDailyTaskReportCommand.cs`:

```csharp
public interface IGenerateDailyTaskReportCommand
{
    Task<byte[]> Execute(DateOnly from, DateOnly to);
}
```

Devuelve directamente los bytes del `.xlsx` en memoria (no hace falta persistir el archivo en disco ni en blob storage — se genera y se descarga al vuelo).

### 4.2. Implementación

`Infrastructure/Adapters/UseCases/Reports/GenerateDailyTaskReportCommandImpl.cs`:

1. Lee `currentUser.UserId` (lanza excepción si no hay usuario autenticado — no debería pasar porque el endpoint exige auth, pero es la guarda que ya usan otros casos de uso).
2. Consulta `taskRepository.GetAllAsync(t => t.UserId == userId && t.TasksTimeDetails.Any(d => d.EndTime != null && d.StartTime.Date >= from.ToDateTime(TimeOnly.MinValue) && d.StartTime.Date <= to.ToDateTime(TimeOnly.MinValue)))`.
3. En memoria, por cada `Task` devuelta: agrupa sus `TasksTimeDetails` por `StartTime.Date` (día en que **empezó** cada sesión — una sesión que cruza medianoche cuenta para el día en que arrancó, caso borde poco común y aceptado como comportamiento simple), filtrando `EndTime != null` (una sesión abierta/en curso no tiene horas computables, mismo criterio que ya usa `Task.GetTotalHoursWorked()`) y filtrando el rango `[from, to]` sobre ese día.
4. Por cada combinación (Tarea, Día) con horas > 0, arma una fila: Fecha, Proyecto (`task.Project.Name`), ID Tarea (`task.WorkPackageId`), Nombre (`task.Name`), Estado (`task.StatusTask.Name`), Horas (decimal, suma de `GetHoursWorked().TotalHours` de las sesiones de ese día).
5. Ordena las filas por Fecha ascendente y, dentro del mismo día, por Proyecto/Nombre.
6. Arma el workbook con ClosedXML: una hoja ("Reporte"), fila de encabezados en negrita, una fila por combinación (Tarea, Día), y una fila final "Total" con la suma de todas las horas del rango.
7. Si no hay filas, el archivo sale igual con headers y sin filas de detalle (no es un error).
8. Devuelve el workbook serializado a `byte[]` (`using var ms = new MemoryStream(); workbook.SaveAs(ms); return ms.ToArray();`).

### 4.3. Controlador

`Web/Controllers/ReportController.cs`:

```csharp
[ApiController]
[Route("api/v1/[controller]")]
public class ReportController(IGenerateDailyTaskReportCommand generateDailyTaskReportCommand) : ControllerBase
{
    [HttpGet("daily-tasks")]
    public async Task<IActionResult> DailyTasks([FromQuery] DateOnly from, [FromQuery] DateOnly to)
    {
        if (from > to) return BadRequest("La fecha 'from' no puede ser posterior a 'to'.");

        var bytes = await generateDailyTaskReportCommand.Execute(from, to);
        var fileName = $"Reporte_Tareas_{from:yyyy-MM-dd}_{to:yyyy-MM-dd}.xlsx";
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }
}
```

No hace falta `[Authorize]` explícito: `Web/Program.cs:15` ya registra un `AuthorizeFilter()` global (`AddControllers(opt => opt.Filters.Add(new AuthorizeFilter()))`), así que todo controlador nuevo queda protegido por defecto — el mismo mecanismo que ya protege `TaskController`. Solo los endpoints públicos (login, etc.) necesitan `[AllowAnonymous]` explícito, que no aplica acá.

### 4.4. Registro de DI

`Infrastructure/Extensions/ServicesExtensions.cs`: agregar `collection.AddScoped<IGenerateDailyTaskReportCommand, GenerateDailyTaskReportCommandImpl>();` junto al resto de casos de uso.

## 5. Frontend

### 5.1. UI

Nueva sección pequeña en `Web/wwwroot/index.html`, dentro del dashboard existente (no una página aparte):

- Dos `<input type="date">` (Desde / Hasta).
- Un botón "Descargar reporte".
- Un mensaje de error inline si falta alguna fecha o si "Desde" > "Hasta" (validación en cliente, antes de pegarle al backend).

### 5.2. `api.js`

Nueva función, ej. `downloadDailyTaskReport(from, to)`. No puede reusar `apiFetch()` tal cual porque esa función asume respuesta JSON; se necesita:

```js
export async function downloadDailyTaskReport(from, to) {
    const res = await fetch(`${API}/report/daily-tasks?from=${from}&to=${to}`, { credentials: 'include' });
    if (res.status === 401) {
        sessionStorage.removeItem('currentUser');
        window.location.replace('/auth.html');
        return;
    }
    if (!res.ok) throw new Error(`Error ${res.status}`);
    const blob = await res.blob();
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `Reporte_Tareas_${from}_${to}.xlsx`;
    a.click();
    URL.revokeObjectURL(url);
}
```

### 5.3. `app.js`

Handler del botón: valida fechas, llama a `downloadDailyTaskReport`, y muestra error inline (mismo patrón visual que otros errores del dashboard) si falla.

## 6. Testing

- **Unit** (`Tests/Infrastructure/Adapters/UseCases/Reports/GenerateDailyTaskReportCommandImplTests.cs`):
  - Agrupa correctamente horas de varias sesiones de la misma tarea el mismo día.
  - Excluye sesiones sin `EndTime` (en curso).
  - Excluye sesiones fuera del rango `[from, to]`.
  - Fila de total suma correctamente todas las horas del rango.
  - Rango sin datos → workbook válido, sin filas de detalle.
  - Se puede verificar el contenido abriendo el workbook resultante con `ClosedXML.Excel.XLWorkbook` desde un `MemoryStream` y leyendo celdas.
- **Controller**: test liviano de que `from > to` devuelve `BadRequest`, y que una llamada válida devuelve `FileContentResult` con el content-type esperado.

## 7. Decisiones ya validadas con el usuario (no re-abrir)

- Granularidad: una fila por tarea x día.
- Columnas: Fecha, Proyecto, ID Tarea, Nombre, Estado, Horas.
- Ubicación UI: sección nueva en el dashboard existente, no página aparte.
- Se incluye fila de total general al final.
