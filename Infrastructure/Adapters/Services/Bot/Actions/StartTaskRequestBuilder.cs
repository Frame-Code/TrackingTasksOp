using Application.Dto.Conversation;
using Application.Dto.Tasks;
using Application.Dto.WorkPackages;
using Application.Ports.Services;
using Application.Ports.UseCases.WorkPackages;

namespace Infrastructure.Adapters.Services.Bot.Actions;

/// <summary>
/// Resultado de resolver los parámetros del LLM: o bien un <see cref="StarTaskRequest"/>
/// listo para ejecutar, o bien un <see cref="PendingMessage"/> pidiendo datos adicionales.
/// </summary>
internal sealed record BuildResult(StarTaskRequest? Request, string? PendingMessage, int ResolvedWpId);

/// <summary>
/// Resuelve los parámetros que manda el LLM (nombres de proyecto/estado/usuarios, fechas,
/// campos personalizados) a un <see cref="StarTaskRequest"/> listo para ejecutar.
/// Lo comparten "create_task" y "start_task" para no duplicar la resolución.
/// </summary>
internal static class StartTaskRequestBuilder
{
    public static async Task<BuildResult> BuildAsync(
        GroqAction action,
        int? contextWpId,
        ConversationContext? conversationContext,
        IStatusOpService statusOpService,
        IOpenProjectEntityResolver entityResolver,
        ICreateWorkPackageCommand createWorkPackageCommand)
    {
        var p = action.Params;
        int wpId = GroqActionParams.GetInt(p, "workPackageId", "id", "wpId");
        if (wpId == 0 && contextWpId.HasValue) wpId = contextWpId.Value;

        var pendingDraft = conversationContext?.PendingStartTaskDraft;

        int? projId = GroqActionParams.GetNullableInt(p, "projectId");
        string pName = GroqActionParams.GetStr(p, "projectName", "project");
        if (!projId.HasValue && !string.IsNullOrEmpty(pName))
        {
            projId = await entityResolver.ResolveProjectId(pName);
        }
        if (!projId.HasValue && pendingDraft != null)
        {
            projId = pendingDraft.ProjectId;
        }

        // ── Padre (subtarea) ──────────────────────────────────────────────────────────
        // "parentId" es la excepción legítima a la regla "nunca un ID": la gente sí dice
        // "crea una subtarea dentro de la #412". Si en cambio la nombró por asunto, se
        // resuelve buscando, y si hay varias candidatas se pregunta en vez de adivinar.
        int? parentId = GroqActionParams.GetNullableInt(p, "parentId", "parentWorkPackageId");
        string parentName = GroqActionParams.GetStr(p, "parentName", "parent");

        if (!parentId.HasValue && !string.IsNullOrEmpty(parentName))
        {
            var candidates = await entityResolver.FindWorkPackagesBySubject(parentName);

            if (candidates.Count == 0)
                throw new Exception($"No encontré ninguna tarea llamada '{parentName}' para usarla como tarea padre. " +
                                    "Verifica el nombre o indícame su número (ej. \"dentro de la #412\").");

            if (candidates.Count > 1)
            {
                var options = string.Join("\n", candidates.Select(c => $"- **#{c.Id}** {c.Subject}"));
                return new BuildResult(null,
                    $"🤔 Hay varias tareas que coinciden con «{parentName}». ¿De cuál debe depender la subtarea?\n\n{options}\n\nIndícame el número y la creo.",
                    wpId);
            }

            parentId = candidates[0].Id;
        }

        if (!parentId.HasValue && pendingDraft != null) parentId = pendingDraft.ParentId;

        // El proyecto sale del padre: una subtarea vive donde vive su padre. Por eso
        // "crea una subtarea dentro de la #412 llamada Acta de firma" alcanza, sin volver a preguntar.
        if (!projId.HasValue && parentId is > 0)
        {
            projId = await entityResolver.GetProjectIdOfWorkPackage(parentId.Value);
            if (!projId.HasValue)
                throw new Exception($"No pude leer la tarea #{parentId} en OpenProject, así que no sé en qué proyecto crear la subtarea. " +
                                    "Verifica el número o indícame el proyecto.");
        }

        // El borrador solo aplica si es de la MISMA tarea/proyecto que se está resolviendo ahora;
        // si el usuario cambió de proyecto, se descarta (mismo criterio que PendingCustomFields).
        var draft = pendingDraft != null && projId == pendingDraft.ProjectId ? pendingDraft : null;

        int? statId = GroqActionParams.GetNullableInt(p, "statusId");
        string statusToResolve = GroqActionParams.GetStr(p, "statusName", "status");
        if (!statId.HasValue && !string.IsNullOrEmpty(statusToResolve))
        {
            statId = await entityResolver.ResolveStatusId(statusToResolve);
        }
        if (!statId.HasValue && draft != null)
        {
            statId = draft.StatusId;
        }

        // Si no hay estado, intentar usar "En curso" o el primero disponible como fallback
        if (!statId.HasValue && string.IsNullOrEmpty(statusToResolve))
        {
            var allStatuses = await statusOpService.Lists();
            statId = allStatuses.FirstOrDefault(s => s.Name.Contains("nuevo", StringComparison.OrdinalIgnoreCase))?.Id
                     ?? allStatuses.FirstOrDefault()?.Id;
        }

        string taskName = GroqActionParams.GetStr(p, "name", "subject", "title");
        if (string.IsNullOrEmpty(taskName) && draft != null) taskName = draft.Name;
        if (string.IsNullOrEmpty(taskName)) throw new Exception("El parámetro 'name' (nombre de la tarea) es obligatorio.");
        if (!projId.HasValue) throw new Exception($"No pude encontrar el proyecto '{pName}'. Por favor, verifica el nombre o usa 'listar proyectos'.");
        if (!statId.HasValue) throw new Exception($"No pude encontrar el estado '{statusToResolve}'. Por favor, verifica el nombre o usa 'listar estados'.");

        string assigneeName = GroqActionParams.GetStr(p, "assigneeName", "assignee");
        string responsibleName = GroqActionParams.GetStr(p, "responsibleName", "responsible");

        int? assigneeId = !string.IsNullOrEmpty(assigneeName)
            ? await entityResolver.ResolveUserId(assigneeName, projId)
            : draft?.AssigneeId;
        int? responsibleId = !string.IsNullOrEmpty(responsibleName)
            ? await entityResolver.ResolveUserId(responsibleName, projId)
            : draft?.ResponsibleId;

        if (!string.IsNullOrEmpty(assigneeName) && !assigneeId.HasValue)
            throw new Exception($"No pude encontrar al usuario '{assigneeName}' para asignarlo. Verifica el nombre o usa 'listar usuarios del proyecto'.");

        if (!string.IsNullOrEmpty(responsibleName) && !responsibleId.HasValue)
            throw new Exception($"No pude encontrar al usuario '{responsibleName}' como responsable. Verifica el nombre o usa 'listar usuarios del proyecto'.");

        string description = GroqActionParams.GetStr(p, "description");
        if (string.IsNullOrEmpty(description) && draft != null) description = draft.Description ?? "";

        DateOnly? startDate = GroqActionParams.GetDate(p, "startDate") ?? draft?.StartDate;
        DateOnly? dueDate = GroqActionParams.GetDate(p, "dueDate") ?? draft?.DueDate;

        // Estimación de trabajo. Opcional: si no viene, la tarea se crea sin estimar.
        double? estimatedHours = GroqActionParams.GetNullableDouble(p, "estimatedHours", "estimatedTime", "workHours")
                                 ?? draft?.EstimatedHours;

        // Borrador que se persiste en Redis si falta algún dato: así el usuario solo responde
        // lo que falta, sin repetir el resto en el siguiente turno.
        var draftToPersist = new PendingStartTaskDraft
        {
            ProjectId = projId.Value,
            StatusId = statId.Value,
            Name = taskName,
            Description = description,
            AssigneeId = assigneeId,
            ResponsibleId = responsibleId,
            StartDate = startDate,
            DueDate = dueDate,
            EstimatedHours = estimatedHours,
            ParentId = parentId
        };

        // El tipo de work package (DESARROLLO, ERROR, SOPORTE TECNICO...) determina qué campos
        // personalizados son obligatorios, así que se resuelve ANTES de consultarlos.
        // Solo aplica al crear: si se hace seguimiento de un WP existente, su tipo ya está fijado.
        int? typeId = null;
        if (wpId <= 0)
        {
            typeId = GroqActionParams.GetNullableInt(p, "typeId") ?? draft?.TypeId;
            string typeName = GroqActionParams.GetStr(p, "typeName", "type", "workPackageType");

            if (!typeId.HasValue || !string.IsNullOrEmpty(typeName))
            {
                var availableTypes = await createWorkPackageCommand.GetTypesAsync(projId.Value);

                if (!string.IsNullOrEmpty(typeName))
                    typeId = MatchType(availableTypes, typeName)?.Id ?? typeId;

                // Preguntamos con los tipos reales del proyecto en vez de asumir uno. Asumir era
                // lo que hacía que toda tarea terminara como el primer tipo de la lista.
                if (!typeId.HasValue && availableTypes.Count > 0)
                {
                    if (conversationContext != null)
                    {
                        conversationContext.PendingTaskProjectId = projId.Value;
                        conversationContext.PendingStartTaskDraft = draftToPersist;
                    }

                    var intro = string.IsNullOrEmpty(typeName)
                        ? "🤔 ¿De qué **tipo** es esta tarea?"
                        : $"🤔 No encontré el tipo «{typeName}» en este proyecto. ¿Cuál de estos es?";

                    var options = string.Join("\n", availableTypes.Select(t => $"- **{t.Name}**"));
                    return new BuildResult(null, $"{intro}\n\n{options}\n\nDime cuál y creo la tarea.", wpId);
                }
            }

            draftToPersist.TypeId = typeId;
        }

        // Si vamos a crear un work package nuevo (no a iniciar seguimiento de uno existente),
        // verificamos si el tipo de tarea del proyecto tiene campos personalizados obligatorios.
        Dictionary<string, int>? customFieldOptionIds = null;
        Dictionary<string, string>? customFieldTextValues = null;
        if (wpId <= 0)
        {
            var requiredFields = await createWorkPackageCommand.GetRequiredCustomFieldsAsync(projId.Value, typeId);
            if (requiredFields.Count > 0)
            {
                // Combina lo que ya se había resuelto en un turno anterior (persistido en el
                // ConversationContext, y por lo tanto en Redis) con lo nuevo de este turno, para
                // no depender de que el LLM recuerde y reenvíe TODOS los valores en cada mensaje.
                var mergedFields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                if (conversationContext?.PendingTaskProjectId == projId.Value && conversationContext.PendingCustomFields != null)
                    foreach (var (k, v) in conversationContext.PendingCustomFields) mergedFields[k] = v;

                var providedCustomFields = GroqActionParams.GetDict(p, "customFields");
                if (providedCustomFields != null)
                    foreach (var (k, v) in providedCustomFields) mergedFields[k] = v;

                var resolvedOptions = new Dictionary<string, int>();
                var resolvedTexts = new Dictionary<string, string>();
                var resolvedRaw = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                var missing = new List<RequiredCustomField>();

                foreach (var field in requiredFields)
                {
                    var providedValue = mergedFields
                        .FirstOrDefault(kv => kv.Key.Equals(field.Name, StringComparison.OrdinalIgnoreCase)).Value;

                    if (field.IsOptionType)
                    {
                        var matchedOption = !string.IsNullOrEmpty(providedValue)
                            ? field.AllowedValues.FirstOrDefault(o =>
                                o.Value.Equals(providedValue, StringComparison.OrdinalIgnoreCase) ||
                                o.Value.Contains(providedValue, StringComparison.OrdinalIgnoreCase) ||
                                providedValue.Contains(o.Value, StringComparison.OrdinalIgnoreCase))
                            : null;

                        if (matchedOption != null)
                        {
                            resolvedOptions[field.Key] = matchedOption.Id;
                            resolvedRaw[field.Name] = matchedOption.Value;
                        }
                        else missing.Add(field);
                    }
                    else
                    {
                        if (!string.IsNullOrEmpty(providedValue))
                        {
                            resolvedTexts[field.Key] = providedValue;
                            resolvedRaw[field.Name] = providedValue;
                        }
                        else missing.Add(field);
                    }
                }

                if (missing.Count > 0)
                {
                    if (conversationContext != null)
                    {
                        conversationContext.PendingTaskProjectId = projId.Value;
                        conversationContext.PendingCustomFields = resolvedRaw;
                        conversationContext.PendingStartTaskDraft = draftToPersist;
                    }

                    var msg = "🤔 Para crear esta tarea necesito que me indiques los siguientes datos:\n\n";
                    foreach (var f in missing)
                    {
                        if (f.IsOptionType)
                            msg += $"- **{f.Name}**: {string.Join(", ", f.AllowedValues.Select(o => o.Value))}\n";
                        else
                            msg += $"- **{f.Name}**: (texto libre)\n";
                    }
                    msg += "\nIndícame estos valores y vuelvo a crear la tarea.";
                    return new BuildResult(null, msg, wpId);
                }

                if (conversationContext != null)
                {
                    conversationContext.PendingTaskProjectId = null;
                    conversationContext.PendingCustomFields = null;
                    conversationContext.PendingStartTaskDraft = null;
                }

                if (resolvedOptions.Count > 0) customFieldOptionIds = resolvedOptions;
                if (resolvedTexts.Count > 0) customFieldTextValues = resolvedTexts;
            }
        }

        var startReq = new StarTaskRequest
        {
            ProjectId = projId.Value,
            StatusId = statId.Value,
            TypeId = typeId,
            ParentId = parentId,
            Name = taskName,
            WorkPackageId = wpId,
            Description = description,
            ActivityId = GroqActionParams.GetNullableInt(p, "activityId"),
            Comment = GroqActionParams.GetStr(p, "comment"),
            AssigneeId = assigneeId,
            ResponsibleId = responsibleId,
            StartDate = startDate,
            DueDate = dueDate,
            EstimatedHours = estimatedHours,
            CustomFieldOptionIds = customFieldOptionIds,
            CustomFieldTextValues = customFieldTextValues
        };

        return new BuildResult(startReq, null, wpId);
    }

    /// <summary>
    /// Empareja lo que dijo el usuario ("error", "soporte") con los tipos del proyecto
    /// ("ERROR", "SOPORTE TECNICO"). Va de más estricto a más laxo para que un nombre exacto
    /// nunca pierda contra una coincidencia parcial de otro tipo.
    /// </summary>
    internal static WorkPackageType? MatchType(List<WorkPackageType> types, string name)
        => types.FirstOrDefault(t => t.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
           ?? types.FirstOrDefault(t => t.Name.Contains(name, StringComparison.OrdinalIgnoreCase))
           ?? types.FirstOrDefault(t => name.Contains(t.Name, StringComparison.OrdinalIgnoreCase));
}
