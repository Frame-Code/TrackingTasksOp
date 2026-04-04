'use strict';

// ─── Config ──────────────────────────────────────────────────────────────────
const API = '/api/v1';
const SESSION_KEY = 'trackingActiveSession';

// ─── State ───────────────────────────────────────────────────────────────────
const state = {
    projects: [],
    workPackages: [],
    timerInterval: null,

    get activeSession() {
        const raw = localStorage.getItem(SESSION_KEY);
        return raw ? JSON.parse(raw) : null;
    }
};

function saveSession(session) {
    localStorage.setItem(SESSION_KEY, JSON.stringify(session));
}

function clearSession() {
    localStorage.removeItem(SESSION_KEY);
}

// ─── API helpers ─────────────────────────────────────────────────────────────
async function apiFetch(url, options = {}) {
    const res = await fetch(url, {
        headers: { 'Content-Type': 'application/json', ...options.headers },
        ...options
    });

    if (!res.ok) {
        let msg = `Error ${res.status}`;
        try {
            const body = await res.json();
            msg = body.title || body.message || body.detail || msg;
        } catch (_) { /* ignore */ }
        throw new Error(msg);
    }

    if (res.status === 204) return null;
    return res.json();
}

// ─── Data loading ─────────────────────────────────────────────────────────────
async function loadProjects() {
    try {
        state.projects = await apiFetch(`${API}/project`);
        renderProjectSelect();
    } catch (e) {
        showToast(`No se pudieron cargar los proyectos: ${e.message}`, 'warning');
        document.getElementById('projectSelect').innerHTML =
            '<option value="">Error al cargar proyectos</option>';
    }
}

async function loadWorkPackages(projectId) {
    setLoading(true);
    hideError();

    try {
        const qs = `offset=0&pageSize=50${projectId ? `&projectId=${projectId}` : ''}`;
        state.workPackages = await apiFetch(`${API}/workpackage?${qs}`);
        renderCards();
    } catch (e) {
        showError(`No se pudieron cargar las tareas: ${e.message}`);
    } finally {
        setLoading(false);
    }
}

async function doStartSession(wp) {
    const payload = {
        workPackageId: wp.id,
        name: wp.subject,
        description: wp.description?.raw || null,
        projectId: extractId(wp._links?.project?.href),
        statusId: extractId(wp._links?.status?.href),
        activityId: null,
        comment: null
    };

    await apiFetch(`${API}/task/start_session`, {
        method: 'POST',
        body: JSON.stringify(payload)
    });

    saveSession({
        workPackageId: wp.id,
        subject: wp.subject,
        startTime: new Date().toISOString()
    });

    startTimer();
    renderCards();
    showToast(`Sesión iniciada: <strong>${escHtml(wp.subject)}</strong>`, 'success');
}

async function doEndSession(workPackageId, activityId, comment) {
    await apiFetch(`${API}/task/end_session`, {
        method: 'POST',
        body: JSON.stringify({ workPackageId, activityId, comment })
    });

    clearSession();
    stopTimer();
    renderCards();
    showToast('Sesión finalizada y tiempo registrado en OpenProject.', 'success');
}

async function loadActivitiesForModal(workPackageId) {
    const sel = document.getElementById('activitySelect');
    sel.innerHTML = '<option value="">Cargando actividades...</option>';
    sel.disabled = true;

    try {
        const activities = await apiFetch(`${API}/activity?workPackageId=${workPackageId}`);

        if (!activities?.length) {
            sel.innerHTML = '<option value="">Sin actividades disponibles</option>';
            return;
        }

        sel.innerHTML = '<option value="">Selecciona una actividad...</option>' +
            activities.map(a =>
                `<option value="${a.id}">${escHtml(a.name)}</option>`
            ).join('');
        sel.disabled = false;
    } catch (e) {
        sel.innerHTML = '<option value="">Error al cargar actividades</option>';
        showToast(`Error al cargar actividades: ${e.message}`, 'danger');
    }

    // Show elapsed time in modal
    const session = state.activeSession;
    if (session) {
        const secs = Math.floor((Date.now() - new Date(session.startTime)) / 1000);
        document.getElementById('sessionDuration').textContent = formatDuration(secs);
        document.getElementById('sessionSummaryBox').classList.remove('d-none');
    }
}

async function loadHistory(workPackageId, taskName) {
    const bodyEl = document.getElementById('historyBody');
    document.getElementById('historyTaskName').textContent = taskName;
    bodyEl.innerHTML = '<div class="text-center py-4"><div class="spinner-border text-primary"></div></div>';

    try {
        const task = await apiFetch(`${API}/task/${workPackageId}`);

        if (!task?.tasksTimeDetails?.length) {
            bodyEl.innerHTML = `
                <div class="text-center py-4 text-muted">
                    <i class="bi bi-inbox display-6 d-block mb-2 opacity-50"></i>
                    <p class="mb-0">No hay sesiones registradas para esta tarea.</p>
                </div>`;
            return;
        }

        const details = [...task.tasksTimeDetails].sort(
            (a, b) => new Date(b.startTime) - new Date(a.startTime)
        );

        let totalSecs = 0;
        const rows = details.map(d => {
            const start = new Date(d.startTime);
            const end = d.endTime ? new Date(d.endTime) : null;
            const durSecs = end ? (end - start) / 1000 : null;
            if (durSecs) totalSecs += durSecs;

            const isActive = !d.endTime;
            const rowClass = isActive ? 'session-active-row' : '';

            return `
                <tr class="${rowClass}">
                    <td class="text-nowrap">${formatDateTime(start)}</td>
                    <td class="text-nowrap">
                        ${end
                            ? formatDateTime(end)
                            : '<span class="badge bg-success">Activa</span>'
                        }
                    </td>
                    <td class="font-monospace text-nowrap">
                        ${durSecs != null
                            ? formatDuration(durSecs)
                            : '<span class="text-success">En progreso</span>'
                        }
                    </td>
                    <td class="text-center">
                        ${d.uploaded
                            ? '<i class="bi bi-cloud-check-fill text-success" title="Registrado en OpenProject"></i>'
                            : '<i class="bi bi-cloud-slash text-muted" title="Pendiente de subir"></i>'
                        }
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
                    <thead class="table-borderless">
                        <tr>
                            <th>Inicio</th>
                            <th>Fin</th>
                            <th>Duración</th>
                            <th class="text-center" title="Registrado en OpenProject">
                                <i class="bi bi-cloud"></i>
                            </th>
                        </tr>
                    </thead>
                    <tbody>${rows}</tbody>
                </table>
            </div>`;
    } catch (e) {
        if (e.message.includes('404') || e.message.toLowerCase().includes('not found')) {
            bodyEl.innerHTML = `
                <div class="text-center py-4 text-muted">
                    <i class="bi bi-inbox display-6 d-block mb-2 opacity-50"></i>
                    <p class="mb-0">No hay sesiones registradas para esta tarea.</p>
                </div>`;
        } else {
            bodyEl.innerHTML = `
                <div class="alert alert-danger mb-0">
                    <i class="bi bi-exclamation-triangle me-2"></i>${escHtml(e.message)}
                </div>`;
        }
    }
}

// ─── Timer ────────────────────────────────────────────────────────────────────
function startTimer() {
    stopTimer();
    updateTimerDisplay();
    state.timerInterval = setInterval(updateTimerDisplay, 1000);
    updateNavbar();
}

function stopTimer() {
    if (state.timerInterval) {
        clearInterval(state.timerInterval);
        state.timerInterval = null;
    }
    updateNavbar();
}

function updateTimerDisplay() {
    const session = state.activeSession;
    if (!session) return;

    const secs = Math.floor((Date.now() - new Date(session.startTime)) / 1000);
    const timeStr = formatDuration(secs);

    const navTimer = document.getElementById('navTimer');
    if (navTimer) navTimer.textContent = timeStr;

    const cardTimer = document.querySelector(
        `[data-wp-id="${session.workPackageId}"] .card-timer`
    );
    if (cardTimer) cardTimer.textContent = timeStr;
}

// ─── Rendering ───────────────────────────────────────────────────────────────
function renderProjectSelect() {
    const sel = document.getElementById('projectSelect');
    sel.innerHTML =
        '<option value="">Todos los proyectos</option>' +
        state.projects
            .filter(p => p.isActive !== false)
            .map(p => `<option value="${p.id}">${escHtml(p.name)}</option>`)
            .join('');
}

function renderCards() {
    const grid = document.getElementById('wpGrid');
    const empty = document.getElementById('emptyState');
    const countBadge = document.getElementById('wpCount');

    if (!state.workPackages.length) {
        grid.innerHTML = '';
        empty.classList.remove('d-none');
        countBadge.classList.add('d-none');
        return;
    }

    empty.classList.add('d-none');
    countBadge.textContent = `${state.workPackages.length} tarea${state.workPackages.length !== 1 ? 's' : ''}`;
    countBadge.classList.remove('d-none');

    const session = state.activeSession;
    grid.innerHTML = state.workPackages.map(wp => buildCard(wp, session)).join('');

    // Bind start buttons
    grid.querySelectorAll('.btn-start').forEach(btn => {
        btn.addEventListener('click', async () => {
            const wp = state.workPackages.find(w => w.id === parseInt(btn.dataset.id));
            if (!wp) return;

            btn.disabled = true;
            const originalHtml = btn.innerHTML;
            btn.innerHTML = '<span class="spinner-border spinner-border-sm"></span>';

            try {
                await doStartSession(wp);
            } catch (e) {
                showToast(`Error al iniciar: ${e.message}`, 'danger');
                btn.disabled = false;
                btn.innerHTML = originalHtml;
            }
        });
    });

    // Bind end buttons
    grid.querySelectorAll('.btn-end').forEach(btn => {
        btn.addEventListener('click', () => openEndModal());
    });

    // Bind history buttons
    grid.querySelectorAll('.btn-history').forEach(btn => {
        btn.addEventListener('click', () => {
            const wp = state.workPackages.find(w => w.id === parseInt(btn.dataset.id));
            if (!wp) return;
            new bootstrap.Modal(document.getElementById('historyModal')).show();
            loadHistory(wp.id, wp.subject);
        });
    });
}

function buildCard(wp, session) {
    const isActive = session?.workPackageId === wp.id;
    const hasOther = session && !isActive;

    const statusTitle = wp._links?.status?.title || 'Sin estado';
    const projectTitle = wp._links?.project?.title || '';
    const assignee = wp._links?.assignee?.title || '';
    const pct = wp.percentageDone ?? 0;

    const cardExtraClass = isActive ? 'wp-card--active' : hasOther ? 'wp-card--disabled' : '';

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

                    <!-- Header: title + status badge -->
                    <div class="d-flex justify-content-between align-items-start gap-2">
                        <h6 class="card-title mb-0 fw-semibold lh-sm" title="${escHtml(wp.subject)}">
                            ${escHtml(wp.subject)}
                        </h6>
                        <span class="badge flex-shrink-0 ${statusClass(statusTitle)}">
                            ${escHtml(statusTitle)}
                        </span>
                    </div>

                    <!-- Meta: project + assignee -->
                    <div class="d-flex flex-wrap gap-x-3 gap-2">
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

                    <!-- Progress bar -->
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

                    <!-- Footer: due date + buttons -->
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

function updateNavbar() {
    const session = state.activeSession;
    const badge = document.getElementById('activeSessionBadge');
    if (session) {
        document.getElementById('activeSessionName').textContent = session.subject;
        badge.classList.remove('d-none');
    } else {
        badge.classList.add('d-none');
    }
}

// ─── Modal: End session ───────────────────────────────────────────────────────
function openEndModal() {
    const session = state.activeSession;
    if (!session) return;

    document.getElementById('modalTaskName').textContent = session.subject;
    document.getElementById('activitySelect').value = '';
    document.getElementById('activitySelect').disabled = true;
    document.getElementById('commentInput').value = '';
    document.getElementById('sessionSummaryBox').classList.add('d-none');

    const confirmBtn = document.getElementById('confirmEndBtn');
    confirmBtn.disabled = false;
    confirmBtn.innerHTML = '<i class="bi bi-stop-circle me-2"></i>Finalizar y registrar';

    new bootstrap.Modal(document.getElementById('endSessionModal')).show();
    loadActivitiesForModal(session.workPackageId);
}

// ─── UI utilities ─────────────────────────────────────────────────────────────
function setLoading(show) {
    document.getElementById('loadingSpinner').classList.toggle('d-none', !show);
    document.getElementById('loadBtn').disabled = show;
}

function showError(msg) {
    document.getElementById('errorMsg').textContent = msg;
    document.getElementById('errorState').classList.remove('d-none');
}

function hideError() {
    document.getElementById('errorState').classList.add('d-none');
}

function showToast(html, type = 'info') {
    const icons = {
        success: 'bi-check-circle-fill',
        danger:  'bi-exclamation-triangle-fill',
        warning: 'bi-exclamation-circle-fill',
        info:    'bi-info-circle-fill'
    };
    const id = `toast_${Date.now()}`;
    const markup = `
        <div id="${id}" class="toast align-items-center text-bg-${type} border-0" role="alert">
            <div class="d-flex">
                <div class="toast-body d-flex align-items-start gap-2">
                    <i class="bi ${icons[type] ?? icons.info} mt-1 flex-shrink-0"></i>
                    <span>${html}</span>
                </div>
                <button type="button" class="btn-close btn-close-white me-2 m-auto"
                        data-bs-dismiss="toast"></button>
            </div>
        </div>`;
    const container = document.getElementById('toastContainer');
    container.insertAdjacentHTML('beforeend', markup);
    const el = document.getElementById(id);
    new bootstrap.Toast(el, { delay: 4500 }).show();
    el.addEventListener('hidden.bs.toast', () => el.remove());
}

// ─── Pure helpers ─────────────────────────────────────────────────────────────
function statusClass(title) {
    const t = (title || '').toLowerCase();
    if (t.includes('new') || t.includes('nuevo'))               return 'status-new';
    if (t.includes('progress') || t.includes('progreso'))       return 'status-inprogress';
    if (t.includes('done') || t.includes('completado'))         return 'status-done';
    if (t.includes('closed') || t.includes('cerrado'))          return 'status-closed';
    if (t.includes('hold') || t.includes('espera'))             return 'status-hold';
    return 'status-default';
}

function extractId(href) {
    if (!href) return 0;
    return parseInt(href.split('/').pop()) || 0;
}

function formatDuration(totalSeconds) {
    const h = Math.floor(totalSeconds / 3600);
    const m = Math.floor((totalSeconds % 3600) / 60);
    const s = Math.floor(totalSeconds % 60);
    return `${String(h).padStart(2, '0')}:${String(m).padStart(2, '0')}:${String(s).padStart(2, '0')}`;
}

function formatDateTime(date) {
    return date.toLocaleString('es-MX', {
        day: '2-digit', month: '2-digit', year: 'numeric',
        hour: '2-digit', minute: '2-digit'
    });
}

function escHtml(str) {
    if (!str) return '';
    return str
        .replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;');
}

// ─── Init ─────────────────────────────────────────────────────────────────────
document.addEventListener('DOMContentLoaded', () => {

    // Load projects on start
    loadProjects();

    // Restore timer if a session was active
    if (state.activeSession) {
        startTimer();
    }

    // Load tasks button
    document.getElementById('loadBtn').addEventListener('click', () => {
        const projectId = document.getElementById('projectSelect').value || null;
        loadWorkPackages(projectId);
    });

    // Confirm end session
    document.getElementById('confirmEndBtn').addEventListener('click', async () => {
        const activityId = parseInt(document.getElementById('activitySelect').value);
        const comment = document.getElementById('commentInput').value.trim();
        const session = state.activeSession;

        if (!activityId) {
            showToast('Debes seleccionar una actividad.', 'warning');
            return;
        }
        if (!comment) {
            showToast('El comentario es requerido.', 'warning');
            return;
        }
        if (!session) return;

        const btn = document.getElementById('confirmEndBtn');
        btn.disabled = true;
        btn.innerHTML = '<span class="spinner-border spinner-border-sm me-2"></span>Registrando...';

        try {
            await doEndSession(session.workPackageId, activityId, comment);
            bootstrap.Modal.getInstance(document.getElementById('endSessionModal'))?.hide();
        } catch (e) {
            showToast(`Error al finalizar: ${e.message}`, 'danger');
            btn.disabled = false;
            btn.innerHTML = '<i class="bi bi-stop-circle me-2"></i>Finalizar y registrar';
        }
    });
});
