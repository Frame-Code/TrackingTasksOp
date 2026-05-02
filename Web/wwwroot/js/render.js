// Renderizado del DOM. Solo genera HTML y actualiza elementos;
// no realiza llamadas API ni maneja eventos.

import { escHtml, statusClass, formatDuration, formatDateTime } from './helpers.js';
import { store, getActiveSession } from './state.js';

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
        return `
            <button class="btn btn-sm status-filter-pill ${statusClass(title)}${isActive ? ' is-active' : ''}"
                    data-status="${escHtml(title)}">
                ${escHtml(title)}
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

    // Búsqueda global: ignora filtros de estado
    // Sin búsqueda: aplica filtro de estado normalmente
    const q = store.searchQuery.trim().toLowerCase();
    let filtered;
    if (q) {
        filtered = store.workPackages.filter(wp =>
            wp.subject?.toLowerCase().includes(q) ||
            String(wp.id).includes(q)
        );
    } else {
        filtered = store.activeStatusFilters.size === 0
            ? store.workPackages
            : store.workPackages.filter(wp => {
                const title = wp._links?.status?.title || 'Sin estado';
                return store.activeStatusFilters.has(title);
            });
    }

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

function buildCard(wp, session) {
    const isActive    = session?.workPackageId === wp.id;
    const hasOther    = session && !isActive;
    const statusTitle = wp._links?.status?.title  || 'Sin estado';
    const projectTitle = wp._links?.project?.title || '';
    const assignee    = wp._links?.assignee?.title || '';
    const pct         = wp.percentageDone ?? 0;

    const cardExtraClass = isActive  ? 'wp-card--active'
                         : hasOther  ? 'wp-card--disabled'
                         : '';

    const dueDateHtml = wp.dueDate
        ? `<small class="text-muted">
               <i class="bi bi-calendar3 me-1"></i>${wp.dueDate}
           </small>`
        : '';

    const timerHtml = isActive
        ? `<div class="d-flex flex-column align-items-center py-2 my-1 rounded bg-body-secondary">
               <span class="card-timer">00:00:00</span>
               <small class="text-muted mt-1" style="font-size:.7rem;letter-spacing:.05em">TIEMPO EN SESIÓN</small>
           </div>`
        : '';

    const actionBtn = isActive
        ? `<button class="btn btn-danger btn-sm btn-end" data-id="${wp.id}">
               <i class="bi bi-stop-circle-fill me-1"></i>Finalizar
           </button>`
        : `<button class="btn btn-outline-success btn-sm btn-start" data-id="${wp.id}">
               <i class="bi bi-play-circle me-1"></i>Iniciar
           </button>`;

    return `
        <div class="col-12 col-md-6 col-xl-4">
            <div class="card wp-card h-100 ${cardExtraClass}" data-wp-id="${wp.id}">
                <div class="card-body d-flex flex-column gap-2 p-3">

                    <div class="d-flex justify-content-between align-items-start gap-2">
                        <h6 class="card-title mb-0 fw-semibold lh-sm" title="${escHtml(wp.subject)}">
                            ${escHtml(wp.subject)}
                        </h6>
                        <span class="badge flex-shrink-0 ${statusClass(statusTitle)}">
                            ${escHtml(statusTitle)}
                        </span>
                    </div>

                    <div class="d-flex flex-wrap gap-2">
                        ${projectTitle
                            ? `<small class="text-muted d-flex align-items-center gap-1">
                                   <i class="bi bi-folder2"></i>${escHtml(projectTitle)}
                               </small>`
                            : ''}
                        ${assignee
                            ? `<small class="text-muted d-flex align-items-center gap-1">
                                   <i class="bi bi-person"></i>${escHtml(assignee)}
                               </small>`
                            : ''}
                    </div>

                    <div>
                        <div class="d-flex justify-content-between mb-1">
                            <small class="text-muted">Progreso</small>
                            <small class="fw-medium">${pct}%</small>
                        </div>
                        <div class="progress progress-thin">
                            <div class="progress-bar${isActive ? ' bg-success' : ''}"
                                 style="width:${pct}%" role="progressbar"></div>
                        </div>
                    </div>

                    ${timerHtml}

                    <div class="mt-auto d-flex justify-content-between align-items-center gap-2 pt-2 border-top border-subtle">
                        <div>${dueDateHtml}</div>
                        <div class="d-flex gap-2">
                            <button class="btn btn-outline-secondary btn-sm btn-history"
                                    data-id="${wp.id}" title="Ver historial de sesiones">
                                <i class="bi bi-clock-history"></i>
                            </button>
                            ${actionBtn}
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

    bodyEl.innerHTML = `
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
