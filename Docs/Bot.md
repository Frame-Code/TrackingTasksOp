# Bot Conversacional (Asistente de Tareas)

## 1. Arquitectura general

El bot procesa cada mensaje del usuario en capas, en este orden:

1. **Normalización** (`HeuristicIntentInterceptor.Normalize`): quita tildes, pasa a minúsculas y recorta espacios.
2. **Interceptor heurístico** (`HeuristicIntentInterceptor.TryInterceptAsync`): atajos para preguntas muy comunes que se resuelven SIN llamar a la IA (más rápido y sin costo de tokens):
   - `"proyectos"`, `"listar proyectos"`, `"mis proyectos"`, `"que proyectos"` → lista de proyectos.
   - `"tareas"`, `"mis tareas"`, `"tareas pendientes"`, `"que tengo pendiente"` (solo si **no** incluye además "estado", "status" o "proyecto") → lista de tareas pendientes agrupadas por proyecto.
   - `"estados"`, `"listar estados"`, `"ver estados"` → lista de estados disponibles.
3. **Groq (LLM)** (`GroqApiClient` + `GroqIntentService`): si no hubo atajo, el prompt se envía a Groq junto con el **system prompt** (reglas + acciones disponibles) y el historial reciente de la conversación (últimos 6 mensajes).
4. **Ejecución de acciones** (`BotActionExecutor`): si la respuesta de la IA contiene uno o más bloques JSON `{ "action": "...", "params": {...} }`, cada uno se ejecuta contra OpenProject/BD. El resultado real (texto con datos reales) reemplaza/acompaña la respuesta del modelo.

**Endpoint:** `POST /api/v1/Bot/chat/{sessionId}` con body `{ "Prompt": "texto del usuario" }`.
`sessionId` identifica la conversación (se guarda en `localStorage` en el front, ver `bot.html`). El historial se persiste en Redis (`IConversationContextService`) con TTL configurable (`RedisSettings.ConversationTtlMinutes`, por defecto 60 min).

---

## 2. Acciones disponibles (`action` + `params`)

| Acción | Params (nombres aceptados) | Obligatorios | Notas |
|---|---|---|---|
| `start_task` | `projectName`/`project`, `statusName`/`status`, `name`/`subject`/`title`, `description`, `assigneeName`/`assignee`, `responsibleName`/`responsible`, `startDate`, `dueDate`, `customFields` (objeto), `workPackageId` | `name`. `projectName` y `statusName` son obligatorios salvo que se resuelvan por defecto (estado "nuevo" o el primero disponible si no se indica ninguno) | Fechas en `yyyy-MM-dd`. Si el tipo de tarea del proyecto exige campos personalizados (ej. "Area", "Modulo") y no vienen en `customFields`, el bot responde pidiéndolos en vez de fallar. |
| `list_projects` | — | — | Solo lectura, sin confirmación. |
| `list_project_users` | `projectName`/`project` | `projectName` | Solo lectura. Si el proyecto no existe, error explicando que se use `listar proyectos`. |
| `list_tasks` | `statusName`/`status` (opcional) | — | Si se da un estado no reconocido, responde con la lista de estados válidos. |
| `list_statuses` | `projectName`/`project` (opcional) | — | Solo lectura. |
| `end_task_session` | `workPackageId`/`id`/`wpId`, `comment`, `activityId`/`activity`, `newStatusName`/`newStatus`/`newStatusId` | `workPackageId` (o que exista un `contextWpId` de una acción previa en la misma respuesta) | Finaliza el seguimiento y registra tiempo en OpenProject. |
| `update_task_status` | `workPackageId`/`id`/`wpId`, `statusName`/`status`/`statusId` | `workPackageId`, `statusName` | Si la transición no está permitida por el workflow, devuelve los estados intermedios válidos (regla 9 del system prompt). |
| `assign_user_to_task` | `workPackageId`/`id`/`wpId`, `assigneeName`/`assignee`, `responsibleName`/`responsible` | `workPackageId` | Al menos uno de los dos roles. |
| `update_progress` | `workPackageId`/`id`/`wpId`, `progress`/`percentageDone`/`percentage` | `workPackageId`, `progress` (0-100) | Falla si `progress` está fuera de rango. |
| `update_task_dates` | `workPackageId`/`id`/`wpId`, `startDate`, `dueDate` | `workPackageId` + al menos una fecha | Fechas en `yyyy-MM-dd`. |
| `pause_task` | `workPackageId`/`id`/`wpId`, `statusName`/`status` (opcional, default "On hold") | `workPackageId` | Guarda el tiempo localmente, no lo sube a OpenProject. |
| `resume_task` | `workPackageId`/`id`/`wpId`, `statusName`/`status` (opcional, default "In progress") | `workPackageId` | Reanuda el cronómetro. |

**Resolución de nombres:** `projectName`, `statusName`, `assigneeName`, `responsibleName` se resuelven contra OpenProject por **coincidencia exacta primero, luego por substring** (`Contains`, sin distinguir mayúsculas/minúsculas). Si no hay match, la acción falla con un mensaje indicando que se use `listar proyectos` / `listar estados` / `listar usuarios del proyecto`.

---

## 3. Cómo escribir prompts para que el bot NO falle

### 3.1 Reglas de oro

1. **Sé explícito con nombres exactos** de proyectos, estados y usuarios. Si no estás seguro, primero pide:
   - *"Lista los proyectos"* / *"¿Qué estados hay disponibles?"* / *"¿Quiénes son los usuarios del proyecto X?"*
   - Estas consultas son de solo lectura y el bot las responde de inmediato, sin pedir confirmación.

2. **Para crear una tarea nueva** (`start_task` sin `workPackageId`), indica como mínimo:
   - Nombre de la tarea (obligatorio).
   - Proyecto (si no lo das, el bot probablemente no podrá resolver `projectId` y fallará).
   - Si quieres evitar la pregunta de confirmación, da también: estado, fechas, descripción y a quién asignarla.
   - Ejemplo completo:
     > "Crea una tarea 'Revisar PR #45' en el proyecto eProduction, estado New, asígnala a Stin Sanchez, con fecha de inicio hoy y fin el viernes."
   - Ejemplo mínimo (el bot preguntará por los valores por defecto antes de crear):
     > "Crea una tarea 'Revisar PR #45' en eProduction"
     >
     > → El bot responderá explicando qué valores por defecto usará (fecha de hoy, sin asignar, etc.) y pedirá confirmación ("sí", "dale", "confirmo", "adelante", "procede").

3. **Si el proyecto tiene campos personalizados obligatorios** (ej. "Area", "Modulo" en eProduction), el bot los pedirá automáticamente la primera vez que intentes crear una tarea ahí. Responde con los valores exactos que te ofrezca (son opciones predefinidas, no texto libre).

4. **Para acciones sobre tareas existentes** (`resume_task`, `pause_task`, `end_task_session`, `update_task_status`, `assign_user_to_task`, `update_progress`, `update_task_dates`), **siempre incluye el número de tarea** (`#1134`, `tarea 1134`, `ID 1134`, `work package 1134`). El bot lo toma literalmente y ejecuta sin pedir confirmación adicional — si el ID no existe, OpenProject devolverá el error correspondiente.
   - Ejemplos válidos: *"Pausa la tarea #1134"*, *"Cambia el estado de la tarea 1134 a Developed"*, *"Asigna la tarea #1134 a Juan"*.

5. **Fechas**: usa expresiones naturales en español ("hoy", "mañana", "el viernes", "del 10 al 15 de junio") — el bot las convierte a `yyyy-MM-dd` usando la fecha actual del servidor como referencia. Si prefieres, puedes dar la fecha ya en formato `yyyy-MM-dd`.

6. **Progreso (`update_progress`)**: da un número entre 0 y 100 (ej. *"pon el progreso de la tarea #1134 en 50%"*). Valores fuera de ese rango fallan explícitamente.

7. **Filtrar tareas por estado** (`list_tasks`): usa el nombre real del estado (ej. *"muéstrame mis tareas en estado In progress"*). No uses "todos"/"all" como filtro — simplemente no menciones ningún estado si quieres ver todas.

8. **Cambios de estado (workflow)**: OpenProject restringe a qué estados se puede pasar desde el estado actual. Si pides un cambio no permitido, el bot te dirá los estados intermedios válidos; puedes pedirle que aplique uno de esos como primer paso.

9. **Una acción por mensaje (recomendado)**: aunque el bot puede generar varios bloques JSON en una sola respuesta y encadenarlos (p. ej. crear una tarea y luego finalizarla usando el ID recién creado), para evitar ambigüedades es más confiable pedir una acción a la vez y confirmar el resultado antes de continuar.

### 3.2 Errores comunes y cómo evitarlos

| Síntoma | Causa típica | Cómo evitarlo |
|---|---|---|
| *"No pude encontrar el proyecto 'X'"* | El nombre no coincide ni exacto ni como substring con ningún proyecto de OpenProject | Usa *"listar proyectos"* y copia el nombre tal cual aparece |
| *"No pude encontrar el estado 'X'"* | Nombre de estado inexistente o mal escrito | Usa *"ver estados"* (o *"ver estados del proyecto X"*) |
| Bot pide datos de "Area"/"Modulo" repetidamente | No se enviaron `customFields` con valores que coincidan (ni exacto ni substring) con las opciones permitidas | Copia exactamente una de las opciones que el bot lista |
| *"Se requiere un ID de tarea válido (workPackageId)"* | Se pidió una acción sobre una tarea existente sin indicar número de tarea, y no hay una tarea creada previamente en la misma conversación | Indica explícitamente `#<número>` |
| *"El progreso debe estar entre 0 y 100"* | Se dio un porcentaje fuera de rango | Usa un valor 0-100 |
| *"No se pudo cambiar el estado... Desde el estado actual puedes cambiar a: ..."* | Transición de estado no permitida por el workflow de OpenProject | Pide uno de los estados intermedios sugeridos |
| Tarea creada pero "no aparece en Mis tareas" | Se creó sin `assigneeName`/`responsibleName` | Indica a quién asignarla, o pide reasignarla después con `assign_user_to_task` |
| *"⚠️ API Key de Groq no configurada"* | Falta `Groq:ApiKey` en `appsettings.Development.json` / variable de entorno | Configurar la API key de Groq (ver sección de configuración del proyecto) |

### 3.3 Ejemplos de prompts recomendados

```
Listar proyectos
¿Qué tengo pendiente hoy?
Ver estados disponibles
Muéstrame los usuarios del proyecto eProduction
Crea una tarea "Corregir bug de login" en eProduction, estado New, asígnala a Stin Sanchez, descripción "El login falla con OAuth", fecha de inicio hoy y fin el viernes
Reanuda el seguimiento de la tarea #1134
Pausa la tarea #1134
Finaliza la sesión de la tarea #1134 con el comentario "Avance del día" y cámbiala a estado Developed
Cambia el estado de la tarea #1134 a Testing
Asigna la tarea #1134 a Juan Pérez como responsable
Pon el progreso de la tarea #1134 en 75%
Cambia las fechas de la tarea #1134: inicio hoy, fin el próximo lunes
Muéstrame mis tareas en estado In progress
```

---

## 4. Para desarrolladores: ajustar el system prompt

El system prompt completo vive en `Infrastructure/Adapters/Services/Bot/GroqApiClient.cs` (método `BuildSystemPrompt`). Resumen de las reglas que el modelo debe seguir:

1. Generar comandos JSON cuando aplique, con texto explicativo opcional antes/después.
2. Si falta información crítica (ej. nombre del proyecto), preguntar en vez de inventar.
3. Usar **nombres** (no IDs) para proyectos/estados/usuarios — el backend resuelve los IDs.
4. Puede usar `list_project_users` para descubrir a quién asignar antes de preguntar.
5. Antes de `start_task` (tarea nueva) con datos no críticos faltantes, primero confirmar con el usuario qué valores por defecto se usarán; generar el JSON solo tras confirmación explícita.
6. Si el usuario da un número de tarea (`#1134`, etc.), usarlo literalmente como `workPackageId` sin pedir confirmación.
7. No incluir `statusName` en `list_tasks` si no se pidió filtro (nunca usar "Todos"/"All").
8. Para acciones de solo lectura (`list_projects`, `list_project_users`, `list_tasks`, `list_statuses`): responder ÚNICAMENTE con el JSON, sin texto ni datos inventados — el sistema agrega el resultado real después de ejecutar.
9. Si una transición de estado falla por restricciones de workflow, ofrecer los estados intermedios sugeridos por el error.

Al modificar el system prompt, mantener la ESTRUCTURA DE COMANDO JSON (`{ "action": "...", "params": {...} }`) y la lista de `ACCIONES DISPONIBLES` sincronizada con los `ActionName` reales en `Infrastructure/Adapters/Services/Bot/Actions/*ActionHandler.cs` — si se agrega/renombra una acción o parámetro ahí, debe reflejarse también en el prompt para que el modelo lo use correctamente.
