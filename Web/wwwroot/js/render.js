// Renderizado del DOM. Solo genera HTML y actualiza elementos;
// no realiza llamadas API ni maneja eventos.

import { escHtml, statusClass, typeClass, formatDuration, formatDateTime } from './helpers.js';
import { store, getActiveSession, getPausedIds } from './state.js';

// ── Navbar ────────────────────────────────────────────────────────────────────

export function updateNavbar() {
    const session = getActiveSession();
    const badge = document.getElementById('activeSessionBadge');
    if (session) {
        document.getElementById('activeSessionName').textContent = session.subject;
        badge.classList.remove('d-none');
    } else {
        badge.classList.add('d-none');
    }
}

// ── Project select ────────────────────────────────────────────────────────────

export function renderProjectSelect() {
    const sel = document.getElementById('projectSelect');
    sel.innerHTML =
        '<option value="">Todos los proyectos</option>' +
        store.projects
            .map(p => `<option value="${p.id}">${escHtml(p.name)}</option>`)
            .join('');
}

// ── Status filters ────────────────────────────────────────────────────────────

export function renderStatusFilters() {
    const section = document.getElementById('statusFilterSection');
    const pillsEl = document.getElementById('statusFilterPills');

    const uniqueStatuses = [...new Map(
        store.workPackages.map(wp => {
            const title = wp._links?.status?.title || 'Sin estado';
            return [title, title];
        })
    ).values()].sort();

    if (!uniqueStatuses.length) {
        section.classList.add('d-none');
        return;
    }

    section.classList.remove('d-none');
    pillsEl.innerHTML = uniqueStatuses.map(title => {
        const isActive = store.activeStatusFilters.has(title);
        // Solo se marca el filtro aplicado (check). El "+" del estado inactivo sugería
        // "agregar" algo, que no es lo que hace el botón.
        return `
            <button class="btn btn-sm status-filter-pill ${statusClass(title)}${isActive ? ' is-active' : ''}"
                    data-status="${escHtml(title)}"
                    aria-pressed="${isActive}"
                    title="${isActive ? 'Filtro aplicado — clic para quitarlo' : 'Clic para filtrar por este estado'}">
                ${isActive ? '<i class="bi bi-check2 me-1" aria-hidden="true"></i>' : ''}${escHtml(title)}
            </button>`;
    }).join('');
}

// ── Work package cards ────────────────────────────────────────────────────────

export function renderCards() {
    const grid       = document.getElementById('wpGrid');
    const empty      = document.getElementById('emptyState');
    const countBadge = document.getElementById('wpCount');

    if (!store.workPackages.length) {
        grid.innerHTML = '';
        empty.classList.remove('d-none');
        countBadge.classList.add('d-none');
        renderPagination(0, 0);
        return;
    }

    // Búsqueda y filtros de estado se combinan (AND). Antes la búsqueda anulaba los
    // filtros pero las pills seguían encendidas, así que la pantalla mostraba un filtro
    // que no estaba surtiendo efecto.
    const q = store.searchQuery.trim().toLowerCase();
    const filtered = store.workPackages.filter(wp => {
        const matchesQuery = !q
            || wp.subject?.toLowerCase().includes(q)
            || String(wp.id).includes(q);
        if (!matchesQuery) return false;

        // Sin filtros seleccionados no se filtra por estado: es "todos", no "ninguno".
        if (store.activeStatusFilters.size === 0) return true;
        return store.activeStatusFilters.has(wp._links?.status?.title || 'Sin estado');
    });

    // Paginación
    const total     = filtered.length;
    const pageSize  = store.pageSize;
    const pageCount = Math.max(1, Math.ceil(total / pageSize));
    if (store.currentPage > pageCount) store.currentPage = pageCount;
    const start   = (store.currentPage - 1) * pageSize;
    const visible = filtered.slice(start, start + pageSize);

    const session = getActiveSession();
    empty.classList.add('d-none');

    if (q) {
        countBadge.textContent = total
            ? `${total} resultado${total !== 1 ? 's' : ''}`
            : 'Sin resultados';
    } else {
        countBadge.textContent =
            `${filtered.length} de ${store.workPackages.length} tarea${store.workPackages.length !== 1 ? 's' : ''}`;
    }
    countBadge.classList.remove('d-none');

    if (!visible.length) {
        grid.innerHTML = `
            <div class="col-12 text-center py-4 text-muted">
                <i class="bi bi-${q ? 'search' : 'funnel'} display-6 d-block mb-2 opacity-25"></i>
                <p class="mb-0">${q
                    ? `Sin resultados para <strong>"${escHtml(q)}"</strong>`
                    : 'Ninguna tarea coincide con los filtros seleccionados.'}</p>
            </div>`;
        renderPagination(0, 0);
        return;
    }

    grid.innerHTML = visible.map(wp => buildCard(wp, session)).join('');
    renderPagination(total, pageCount);
}

export function renderPagination(total, pageCount) {
    const el = document.getElementById('pagination');
    if (!el) return;

    if (pageCount <= 1) {
        el.classList.add('d-none');
        el.innerHTML = '';
        return;
    }

    el.classList.remove('d-none');
    const current    = store.currentPage;
    const maxVisible = 7;
    let rangeStart = Math.max(1, current - Math.floor(maxVisible / 2));
    let rangeEnd   = Math.min(pageCount, rangeStart + maxVisible - 1);
    if (rangeEnd - rangeStart < maxVisible - 1)
        rangeStart = Math.max(1, rangeEnd - maxVisible + 1);

    let html = '<nav aria-label="Paginación"><ul class="pagination mb-0">';

    html += `<li class="page-item${current === 1 ? ' disabled' : ''}">
        <button class="page-link" data-page="${current - 1}" aria-label="Anterior">
            <i class="bi bi-chevron-left"></i>
        </button></li>`;

    if (rangeStart > 1) {
        html += `<li class="page-item"><button class="page-link" data-page="1">1</button></li>`;
        if (rangeStart > 2)
            html += `<li class="page-item disabled"><span class="page-link">…</span></li>`;
    }

    for (let i = rangeStart; i <= rangeEnd; i++) {
        html += `<li class="page-item${i === current ? ' active' : ''}">
            <button class="page-link" data-page="${i}">${i}</button></li>`;
    }

    if (rangeEnd < pageCount) {
        if (rangeEnd < pageCount - 1)
            html += `<li class="page-item disabled"><span class="page-link">…</span></li>`;
        html += `<li class="page-item"><button class="page-link" data-page="${pageCount}">${pageCount}</button></li>`;
    }

    html += `<li class="page-item${current === pageCount ? ' disabled' : ''}">
        <button class="page-link" data-page="${current + 1}" aria-label="Siguiente">
            <i class="bi bi-chevron-right"></i>
        </button></li>`;

    html += '</ul></nav>';
    el.innerHTML = html;
}

function buildStatusDropdown(wp, statusTitle) {
    if (!store.statuses.length) {
        return `<span class="badge flex-shrink-0 ${statusClass(statusTitle)}">${escHtml(statusTitle)}</span>`;
    }

    const items = store.statuses.map(s => {
        const isCurrent = s.name === statusTitle;
        return `
            <li>
                <button class="dropdown-item btn-set-status d-flex align-items-center gap-2"
                        data-wp-id="${wp.id}"
                        data-status-id="${s.id}"
                        data-status-name="${escHtml(s.name)}"
                        ${isCurrent ? 'disabled' : ''}>
                    <span class="status-dot ${statusClass(s.name)}"></span>
                    <span class="flex-grow-1">${escHtml(s.name)}</span>
                    ${isCurrent ? '<i class="bi bi-check text-success"></i>' : ''}
                </button>
            </li>`;
    }).join('');

    return `
        <div class="dropdown flex-shrink-0">
            <button class="badge ${statusClass(statusTitle)} border-0 dropdown-toggle status-badge-btn"
                    type="button"
                    data-bs-toggle="dropdown"
                    data-bs-auto-close="true"
                    aria-expanded="false"
                    data-wp-id="${wp.id}"
                    title="Cambiar estado">
                ${escHtml(statusTitle)}
            </button>
            <ul class="dropdown-menu dropdown-menu-end status-dropdown-menu shadow-sm">
                <li><h6 class="dropdown-header py-1">Cambiar estado</h6></li>
                <li><hr class="dropdown-divider my-1"></li>
                ${items}
            </ul>
        </div>`;
}

/**
 * Chip de persona con la etiqueta del rol visible. Asignado y responsable son papeles
 * distintos en OpenProject y suelen ser personas distintas: dos íconos de persona sin
 * texto obligarían a recordar cuál es cuál (Nielsen: reconocer antes que recordar).
 * "Sin asignar" se muestra explícitamente en vez de omitir el dato, porque un hueco
 * en blanco no distingue "nadie" de "no cargó".
 */
function buildPersonChip(icon, label, name) {
    const value = name || 'Sin asignar';
    const muted = name ? '' : ' fst-italic opacity-75';
    return `
        <span class="d-inline-flex align-items-center gap-1 text-truncate" title="${label}: ${escHtml(value)}">
            <i class="bi ${icon}" aria-hidden="true"></i>
            <span class="opacity-75">${label}:</span>
            <span class="${muted}">${escHtml(value)}</span>
        </span>`;
}

function buildCard(wp, session) {
    const isActive     = session?.workPackageId === wp.id;
    const isPaused     = !isActive && getPausedIds().has(wp.id);
    const hasOther     = session && !isActive && !isPaused;
    const statusTitle  = wp._links?.status?.title  || 'Sin estado';
    const typeTitle    = wp._links?.type?.title    || '';
    const projectTitle = wp._links?.project?.title || '';
    const assignee     = wp._links?.assignee?.title || '';
    const responsible  = wp._links?.responsible?.title || '';
    const pct          = wp.percentageDone ?? 0;

    const cardExtraClass = isActive  ? 'wp-card--active'
                         : hasOther  ? 'wp-card--disabled'
                         : '';

    // ── Timer ──────────────────────────────────────────────────────────────────
    const timerHtml = isActive
        ? `<div class="d-flex flex-column align-items-center py-2 my-1 rounded bg-body-secondary">
               <span class="card-timer">00:00:00</span>
               <small class="text-muted mt-1" style="font-size:.7rem;letter-spacing:.05em">TIEMPO EN SESIÓN</small>
           </div>`
        : '';

    // ── Fechas (display en una sola línea: inicio – fin) ──────────────────────
    const startTxt = wp.startDate ? escHtml(wp.startDate) : '–';
    const dueTxt   = wp.dueDate   ? escHtml(wp.dueDate)   : '–';
    const datesDisplay = `
        <span class="d-inline-flex align-items-center gap-1">
            <i class="bi bi-calendar3" aria-hidden="true"></i>${startTxt} — ${dueTxt}
        </span>`;

    // ── Botones de acción ──────────────────────────────────────────────────────
    const actionBtns = isActive
        ? `<button class="btn btn-outline-secondary btn-sm btn-cancel" data-id="${wp.id}" title="Cancelar sesión sin guardar" aria-label="Cancelar sesión sin guardar">
               <i class="bi bi-x-circle"></i>
           </button>
           <button class="btn btn-warning btn-sm btn-pause" data-id="${wp.id}" title="Pausar sesión" aria-label="Pausar sesión">
               <i class="bi bi-pause-circle-fill me-1"></i>Pausar
           </button>
           <button class="btn btn-danger btn-sm btn-end" data-id="${wp.id}">
               <i class="bi bi-stop-circle-fill me-1"></i>Finalizar
           </button>`
        : isPaused
            ? `<button class="btn btn-primary btn-sm btn-resume" data-id="${wp.id}">
                   <i class="bi bi-play-circle-fill me-1"></i>Continuar
               </button>`
            : `<button class="btn btn-success btn-sm btn-start" data-id="${wp.id}">
                   <i class="bi bi-play-circle me-1"></i>Iniciar
               </button>`;

    // La tarjeta se lee en tres niveles, de mayor a menor peso visual:
    //   1. QUÉ ES  → tipo + #id (eyebrow) y el asunto como único texto grande.
    //   2. CONTEXTO → proyecto, personas y fechas, en micro-texto atenuado que
    //                 se puede saltar de un vistazo.
    //   3. ESTADO Y ACCIÓN → progreso y botones, anclados abajo en todas las
    //                 tarjetas para que la fila de acciones sea escaneable.
    // Antes todo compartía tamaño y color, así que nada guiaba la mirada.
    return `
        <div class="col-12 col-md-6 col-xl-4">
            <div class="card wp-card h-100 ${cardExtraClass}" data-wp-id="${wp.id}">
                <div class="card-body d-flex flex-column p-3">

                    <!-- ① Identidad -->
                    <div class="d-flex justify-content-between align-items-center gap-2 mb-2">
                        <div class="d-flex align-items-center gap-2 text-truncate">
                            ${typeTitle
                                ? `<span class="badge wp-type-badge ${typeClass(typeTitle)}" title="Tipo de paquete de trabajo">${escHtml(typeTitle)}</span>`
                                : ''}
                            <span class="wp-id">#${wp.id}</span>
                        </div>
                        ${buildStatusDropdown(wp, statusTitle)}
                    </div>

                    <h6 class="wp-title mb-2" title="${escHtml(wp.subject)}">${escHtml(wp.subject)}</h6>

                    <!-- ② Contexto -->
                    <div class="wp-meta mb-3">
                        <div class="d-flex align-items-center gap-2 text-truncate">
                            ${projectTitle
                                ? `<span class="d-inline-flex align-items-center gap-1 text-truncate">
                                       <i class="bi bi-folder2" aria-hidden="true"></i>${escHtml(projectTitle)}
                                   </span>
                                   <span class="wp-meta-sep" aria-hidden="true">·</span>`
                                : ''}
                            ${datesDisplay}
                            <button class="btn btn-link btn-sm p-0 wp-meta-edit btn-dates"
                                    data-id="${wp.id}"
                                    data-start="${escHtml(wp.startDate || '')}"
                                    data-due="${escHtml(wp.dueDate || '')}"
                                    title="Editar fechas" aria-label="Editar fechas de la tarea ${wp.id}">
                                <i class="bi bi-pencil-square" aria-hidden="true"></i>
                            </button>
                        </div>
                        <div class="d-flex flex-wrap column-gap-3 row-gap-1 mt-1">
                            ${buildPersonChip('bi-person', 'Asignado', assignee)}
                            ${buildPersonChip('bi-person-badge', 'Responsable', responsible)}
                        </div>
                    </div>

                    ${timerHtml}

                    <!-- ③ Progreso + acciones, siempre al pie -->
                    <div class="mt-auto">
                        <div class="d-flex justify-content-between align-items-center mb-1">
                            <small class="wp-meta">Progreso</small>
                            <small class="fw-semibold wp-pct-display">${pct}%</small>
                        </div>
                        <input type="range" class="form-range wp-progress-input"
                               min="0" max="100" step="5"
                               value="${pct}"
                               data-wp-id="${wp.id}"
                               aria-label="Progreso de la tarea ${wp.id}"
                               style="background: linear-gradient(to right, #0d6efd ${pct}%, rgba(255,255,255,0.15) ${pct}%)">

                        <div class="d-flex justify-content-between align-items-center gap-2 pt-2 mt-2 border-top border-subtle">
                            <div class="d-flex gap-1">
                                <button class="btn btn-sm wp-icon-btn btn-log-time"
                                        data-id="${wp.id}" title="Registrar tiempo a mano (sin cronómetro)" aria-label="Registrar tiempo a mano en la tarea ${wp.id}">
                                    <i class="bi bi-stopwatch" aria-hidden="true"></i>
                                </button>
                                <button class="btn btn-sm wp-icon-btn btn-history"
                                        data-id="${wp.id}" title="Ver historial de sesiones" aria-label="Ver historial de sesiones de la tarea ${wp.id}">
                                    <i class="bi bi-clock-history" aria-hidden="true"></i>
                                </button>
                            </div>
                            <div class="d-flex gap-2">
                                ${actionBtns}
                            </div>
                        </div>
                    </div>

                </div>
            </div>
        </div>`;
}

// ── History modal ─────────────────────────────────────────────────────────────

export function renderHistoryLoading(taskName) {
    document.getElementById('historyTaskName').textContent = taskName;
    document.getElementById('historyBody').innerHTML =
        '<div class="text-center py-4"><div class="spinner-border text-primary"></div></div>';
}

export function renderHistoryContent(task) {
    const bodyEl = document.getElementById('historyBody');

    if (!task?.tasksTimeDetails?.length) {
        bodyEl.innerHTML = emptyHistoryHtml();
        return;
    }

    const details = [...task.tasksTimeDetails].sort(
        (a, b) => new Date(b.startTime) - new Date(a.startTime)
    );

    let totalSecs = 0;
    const rows = details.map(d => {
        const start   = new Date(d.startTime);
        const end     = d.endTime ? new Date(d.endTime) : null;
        const durSecs = end ? (end - start) / 1000 : null;
        if (durSecs) totalSecs += durSecs;

        return `
            <tr${!d.endTime ? ' class="session-active-row"' : ''}>
                <td class="text-nowrap">${formatDateTime(start)}</td>
                <td class="text-nowrap">
                    ${end ? formatDateTime(end) : '<span class="badge bg-success">Activa</span>'}
                </td>
                <td class="font-monospace text-nowrap">
                    ${durSecs != null
                        ? formatDuration(durSecs)
                        : '<span class="text-success">En progreso</span>'}
                </td>
                <td class="text-center">
                    ${d.uploaded
                        ? '<i class="bi bi-cloud-check-fill text-success" title="Registrado en OpenProject"></i>'
                        : '<i class="bi bi-cloud-slash text-muted" title="Pendiente de subir"></i>'}
                </td>
            </tr>`;
    }).join('');

    // Sesiones cerradas que siguen sin registrarse en OpenProject. Se cuentan aparte para
    // poder ofrecer la acción de subirlas: antes el tiempo quedaba visible pero sin salida.
    const pending = details.filter(d => d.endTime && !d.uploaded);
    const pendingSecs = pending.reduce(
        (acc, d) => acc + (new Date(d.endTime) - new Date(d.startTime)) / 1000, 0);

    // Visibilidad del estado del sistema + control del usuario (Nielsen): se dice cuánto
    // falta por subir y se da el botón para hacerlo, en vez de solo marcar el ícono de nube.
    const pendingHtml = pending.length
        ? `<div class="alert alert-warning d-flex flex-wrap align-items-center justify-content-between gap-2 py-2 mb-3">
               <span>
                   <i class="bi bi-cloud-slash me-1"></i>
                   <strong>${pending.length} sesión${pending.length !== 1 ? 'es' : ''}</strong>
                   sin registrar en OpenProject
                   (<span class="font-monospace">${formatDuration(pendingSecs)}</span>)
               </span>
               <button class="btn btn-warning btn-sm btn-upload-pending" data-id="${task.workPackageId}">
                   <i class="bi bi-cloud-arrow-up me-1"></i>Subir pendientes
               </button>
           </div>`
        : '';

    bodyEl.innerHTML = `
        ${pendingHtml}
        <div class="d-flex gap-2 flex-wrap mb-3">
            <span class="badge rounded-pill bg-body-secondary text-body border fs-6 fw-normal px-3 py-2">
                <i class="bi bi-list-check me-1"></i>
                ${details.length} sesión${details.length !== 1 ? 'es' : ''}
            </span>
            <span class="badge rounded-pill bg-body-secondary text-body border fs-6 fw-normal px-3 py-2">
                <i class="bi bi-clock me-1"></i>
                Total: <strong class="font-monospace ms-1">${formatDuration(totalSecs)}</strong>
            </span>
        </div>
        <div class="table-responsive">
            <table class="table table-sm table-hover align-middle mb-0">
                <thead>
                    <tr>
                        <th>Inicio</th><th>Fin</th><th>Duración</th>
                        <th class="text-center" title="Registrado en OpenProject">
                            <i class="bi bi-cloud"></i>
                        </th>
                    </tr>
                </thead>
                <tbody>${rows}</tbody>
            </table>
        </div>`;
}

export function renderHistoryError(message) {
    document.getElementById('historyBody').innerHTML = `
        <div class="alert alert-danger mb-0">
            <i class="bi bi-exclamation-triangle me-2"></i>${escHtml(message)}
        </div>`;
}

function emptyHistoryHtml() {
    return `
        <div class="text-center py-4 text-muted">
            <i class="bi bi-inbox display-6 d-block mb-2 opacity-50"></i>
            <p class="mb-0">No hay sesiones registradas para esta tarea.</p>
        </div>`;
}

// ── End-session modal ─────────────────────────────────────────────────────────

const PREFERRED_ACTIVITIES = ['development', 'management'];

export function renderActivitiesSelect(activities) {
    const sel = document.getElementById('activitySelect');
    if (!activities?.length) {
        sel.innerHTML = '<option value="">Sin actividades disponibles</option>';
        return;
    }

    sel.innerHTML =
        '<option value="">Selecciona una actividad...</option>' +
        activities.map(a => `<option value="${a.id}">${escHtml(a.name)}</option>`).join('');
    sel.disabled = false;

    const preferred = PREFERRED_ACTIVITIES
        .map(name => activities.find(a => a.name.toLowerCase() === name))
        .find(Boolean);

    if (preferred) sel.value = preferred.id;
}

// ── Vista previa del reporte ──────────────────────────────────────────────────

/**
 * Muestra en pantalla exactamente lo que llevará el Excel, para que el usuario decida
 * si imprimirlo o descargarlo (Nielsen: visibilidad del estado + control del usuario).
 */
export function renderReportPreview(data, { from, to, statusName }) {
    const rows = data?.rows ?? [];
    const pending = data?.pending ?? [];
    const total = data?.totalHours ?? 0;

    document.getElementById('reportPreviewMeta').innerHTML = `
        <span class="me-3"><i class="bi bi-calendar-range me-1"></i>${escHtml(from)} — ${escHtml(to)}</span>
        <span class="me-3"><i class="bi bi-funnel me-1"></i>Estado: ${escHtml(statusName || 'Todos')}</span>
        <span><i class="bi bi-clock me-1"></i>Total: <strong>${total} h</strong></span>`;

    const body = document.getElementById('reportPreviewBody');

    if (!rows.length && !pending.length) {
        body.innerHTML = `
            <div class="text-center py-5 text-muted">
                <i class="bi bi-inbox display-6 d-block mb-2 opacity-25"></i>
                <p class="mb-0">No hay horas registradas con esos filtros.</p>
                <p class="small mb-0">Prueba con otro rango de fechas o quitando el filtro de estado.</p>
            </div>`;
        return;
    }

    body.innerHTML = `
        ${rows.length ? reportTable(rows, total) : ''}
        ${pending.length ? pendingTable(pending) : ''}`;
}

function reportTable(rows, total) {
    return `
        <div class="table-responsive">
            <table class="table table-sm table-hover align-middle mb-4">
                <thead>
                    <tr>
                        <th>Fecha</th><th>Proyecto</th><th>ID</th><th>Tipo</th>
                        <th>Tarea</th><th>Estado</th><th>Actividad</th>
                        <th>Asignado</th><th>Responsable</th><th class="text-end">Horas</th>
                    </tr>
                </thead>
                <tbody>
                    ${rows.map(r => `
                        <tr>
                            <td class="text-nowrap">${escHtml(r.date)}</td>
                            <td>${escHtml(r.projectName)}</td>
                            <td>#${r.workPackageId}</td>
                            <td>${escHtml(r.type || '–')}</td>
                            <td>${escHtml(r.taskName)}</td>
                            <td>${escHtml(r.status || '–')}</td>
                            <td>${escHtml(r.activityName)}</td>
                            <td>${escHtml(r.assignee || '–')}</td>
                            <td>${escHtml(r.responsible || '–')}</td>
                            <td class="text-end font-monospace">${r.hours}</td>
                        </tr>`).join('')}
                </tbody>
                <tfoot>
                    <tr class="fw-bold border-top">
                        <td colspan="9" class="text-end">Total</td>
                        <td class="text-end font-monospace">${total}</td>
                    </tr>
                </tfoot>
            </table>
        </div>`;
}

function pendingTable(pending) {
    return `
        <h6 class="mt-2"><i class="bi bi-cloud-slash me-1 text-warning"></i>Pendientes de subir a OpenProject</h6>
        <div class="table-responsive">
            <table class="table table-sm table-hover align-middle mb-0">
                <thead>
                    <tr>
                        <th>Fecha</th><th>Proyecto</th><th>ID</th><th>Tipo</th>
                        <th>Tarea</th><th>Estado</th><th class="text-end">Horas</th>
                    </tr>
                </thead>
                <tbody>
                    ${pending.map(p => `
                        <tr>
                            <td class="text-nowrap">${escHtml(p.date)}</td>
                            <td>${escHtml(p.projectName)}</td>
                            <td>#${p.workPackageId}</td>
                            <td>${escHtml(p.type || '–')}</td>
                            <td>${escHtml(p.taskName)}</td>
                            <td>${escHtml(p.status || '–')}</td>
                            <td class="text-end font-monospace">${p.hours}</td>
                        </tr>`).join('')}
                </tbody>
            </table>
        </div>`;
}
