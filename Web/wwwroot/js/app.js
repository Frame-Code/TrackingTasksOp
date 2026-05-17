// Punto de entrada: orquesta módulos y maneja todos los eventos del usuario

import { store, getActiveSession, saveSession, clearSession } from './state.js';
import { fetchProjects, fetchWorkPackages, fetchActivities, fetchTask,
         postStartSession, postEndSession, fetchStatuses, patchWorkPackageStatus } from './api.js';
import { updateNavbar, renderProjectSelect, renderCards, renderStatusFilters,
         renderHistoryLoading, renderHistoryContent, renderHistoryError,
         renderActivitiesSelect } from './render.js';
import { startTimer, stopTimer } from './timer.js';
import { showToast, setLoading, showError, hideError } from './ui.js';
import { escHtml, formatDuration, statusClass } from './helpers.js';

// ── Carga de datos ────────────────────────────────────────────────────────────

async function loadProjects() {
    try {
        store.projects = await fetchProjects();
        renderProjectSelect();
    } catch (e) {
        showToast(`No se pudieron cargar los proyectos: ${e.message}`, 'warning');
        document.getElementById('projectSelect').innerHTML =
            '<option value="">Error al cargar proyectos</option>';
    }
}

const DEFAULT_STATUSES = ['new', 'nuevo', 'in progress', 'en progreso'];

async function loadStatuses() {
    try {
        store.statuses = await fetchStatuses();
        // Si ya hay tarjetas renderizadas, refrescarlas para mostrar los dropdowns
        if (store.workPackages.length) renderCards();
    } catch (e) {
        console.warn('No se pudieron cargar los estados de OpenProject:', e.message);
    }
}

async function loadWorkPackages(projectId) {
    setLoading(true);
    hideError();
    try {
        store.workPackages = await fetchWorkPackages(projectId);
        store.currentPage  = 1;
        store.searchQuery  = '';
        const searchInput = document.getElementById('searchInput');
        if (searchInput) { searchInput.value = ''; }
        const clearBtn = document.getElementById('clearSearchBtn');
        if (clearBtn) clearBtn.classList.add('d-none');
        initDefaultStatusFilters();
        renderStatusFilters();
        renderCards();
    } catch (e) {
        showError(`No se pudieron cargar las tareas: ${e.message}`);
    } finally {
        setLoading(false);
    }
}

function initDefaultStatusFilters() {
    store.activeStatusFilters.clear();
    store.workPackages.forEach(wp => {
        const title = wp._links?.status?.title || 'Sin estado';
        if (DEFAULT_STATUSES.some(d => title.toLowerCase().includes(d))) {
            store.activeStatusFilters.add(title);
        }
    });
}

// ── Acciones de sesión ────────────────────────────────────────────────────────

async function handleStartSession(wpId) {
    const wp = store.workPackages.find(w => w.id === wpId);
    if (!wp) return;

    // Feedback visual inmediato en el botón
    const btn = document.querySelector(`.btn-start[data-id="${wpId}"]`);
    if (btn) {
        btn.disabled = true;
        btn.innerHTML = '<span class="spinner-border spinner-border-sm"></span>';
    }

    try {
        await postStartSession(wp);
        saveSession({ workPackageId: wp.id, subject: wp.subject, startTime: new Date().toISOString() });
        startTimer();
        renderCards();
        showToast(`Sesión iniciada: <strong>${escHtml(wp.subject)}</strong>`, 'success');
    } catch (e) {
        showToast(`Error al iniciar: ${e.message}`, 'danger');
        if (btn) {
            btn.disabled = false;
            btn.innerHTML = '<i class="bi bi-play-circle me-1"></i>Iniciar';
        }
    }
}

async function handleChangeStatus(wpId, statusId, statusName) {
    const badgeBtn = document.querySelector(`.status-badge-btn[data-wp-id="${wpId}"]`);
    const originalHtml = badgeBtn?.innerHTML;

    if (badgeBtn) {
        badgeBtn.innerHTML = '<span class="spinner-border spinner-border-sm" style="width:.6rem;height:.6rem;border-width:1.5px"></span>';
        badgeBtn.disabled = true;
    }

    try {
        await patchWorkPackageStatus(wpId, statusId);

        // Actualizar estado en el store local
        const wp = store.workPackages.find(w => w.id === wpId);
        if (wp?._links?.status) {
            wp._links.status.title = statusName;
            wp._links.status.href  = `/api/v3/statuses/${statusId}`;
        }

        renderStatusFilters();
        renderCards();
        showToast(`Estado cambiado a <strong>${escHtml(statusName)}</strong>`, 'success');
    } catch (e) {
        showToast(`Error al cambiar estado: ${e.message}`, 'danger');
        if (badgeBtn) {
            badgeBtn.innerHTML = originalHtml;
            badgeBtn.disabled  = false;
        }
    }
}

async function handleEndSession(activityId, comment) {
    const session = getActiveSession();
    if (!session) return;

    await postEndSession(session.workPackageId, activityId, comment);
    clearSession();
    stopTimer();
    renderCards();
    showToast('Sesión finalizada y tiempo registrado en OpenProject.', 'success');
}

async function handleOpenHistory(wpId) {
    const wp = store.workPackages.find(w => w.id === wpId);
    if (!wp) return;

    new bootstrap.Modal(document.getElementById('historyModal')).show();
    renderHistoryLoading(wp.subject);

    try {
        const task = await fetchTask(wp.id);
        renderHistoryContent(task);
    } catch (e) {
        const isNotFound = e.message.includes('404') ||
                           e.message.toLowerCase().includes('not found');
        isNotFound ? renderHistoryContent(null) : renderHistoryError(e.message);
    }
}

// ── Modal: Finalizar sesión ───────────────────────────────────────────────────

function openEndModal() {
    const session = getActiveSession();
    if (!session) return;

    document.getElementById('modalTaskName').textContent = session.subject;
    document.getElementById('activitySelect').disabled = true;
    document.getElementById('activitySelect').innerHTML =
        '<option value="">Cargando actividades...</option>';
    document.getElementById('commentInput').value = '';
    document.getElementById('sessionSummaryBox').classList.add('d-none');

    const confirmBtn = document.getElementById('confirmEndBtn');
    confirmBtn.disabled = false;
    confirmBtn.innerHTML = '<i class="bi bi-stop-circle me-2"></i>Finalizar y registrar';

    new bootstrap.Modal(document.getElementById('endSessionModal')).show();
    populateActivities(session);
}

async function populateActivities(session) {
    try {
        const activities = await fetchActivities(session.workPackageId);
        renderActivitiesSelect(activities);

        const secs = Math.floor((Date.now() - new Date(session.startTime)) / 1000);
        document.getElementById('sessionDuration').textContent = formatDuration(secs);
        document.getElementById('sessionSummaryBox').classList.remove('d-none');
    } catch (e) {
        document.getElementById('activitySelect').innerHTML =
            '<option value="">Error al cargar actividades</option>';
        showToast(`Error al cargar actividades: ${e.message}`, 'danger');
    }
}

// ── Event delegation ──────────────────────────────────────────────────────────

function bindStatusFilterEvents() {
    document.getElementById('statusFilterPills').addEventListener('click', (e) => {
        const pill = e.target.closest('.status-filter-pill');
        if (!pill) return;

        const status = pill.dataset.status;
        if (store.activeStatusFilters.has(status)) {
            store.activeStatusFilters.delete(status);
            pill.classList.remove('is-active');
        } else {
            store.activeStatusFilters.add(status);
            pill.classList.add('is-active');
        }
        store.currentPage = 1;
        renderCards();
    });
}

function debounce(fn, delay) {
    let timer;
    return (...args) => { clearTimeout(timer); timer = setTimeout(() => fn(...args), delay); };
}

function bindSearchEvents() {
    const input    = document.getElementById('searchInput');
    const clearBtn = document.getElementById('clearSearchBtn');

    const onInput = debounce(() => {
        store.searchQuery  = input.value;
        store.currentPage  = 1;
        clearBtn.classList.toggle('d-none', !input.value);
        if (store.workPackages.length) renderCards();
    }, 250);

    input.addEventListener('input', onInput);

    clearBtn.addEventListener('click', () => {
        input.value        = '';
        store.searchQuery  = '';
        store.currentPage  = 1;
        clearBtn.classList.add('d-none');
        if (store.workPackages.length) renderCards();
        input.focus();
    });
}

function bindPaginationEvents() {
    document.getElementById('pagination').addEventListener('click', (e) => {
        const btn = e.target.closest('[data-page]');
        if (!btn || btn.closest('.disabled')) return;
        store.currentPage = parseInt(btn.dataset.page);
        renderCards();
        window.scrollTo({ top: 0, behavior: 'smooth' });
    });
}

function bindGridEvents() {
    document.getElementById('wpGrid').addEventListener('click', async (e) => {
        const startBtn     = e.target.closest('.btn-start');
        const endBtn       = e.target.closest('.btn-end');
        const historyBtn   = e.target.closest('.btn-history');
        const setStatusBtn = e.target.closest('.btn-set-status');

        if (startBtn)     await handleStartSession(parseInt(startBtn.dataset.id));
        if (endBtn)       openEndModal();
        if (historyBtn)   await handleOpenHistory(parseInt(historyBtn.dataset.id));
        if (setStatusBtn) await handleChangeStatus(
            parseInt(setStatusBtn.dataset.wpId),
            parseInt(setStatusBtn.dataset.statusId),
            setStatusBtn.dataset.statusName
        );
    });
}

function bindLoadButton() {
    document.getElementById('loadBtn').addEventListener('click', () => {
        const projectId = document.getElementById('projectSelect').value || null;
        loadWorkPackages(projectId);
    });
}

function bindConfirmEndButton() {
    document.getElementById('confirmEndBtn').addEventListener('click', async () => {
        const activityId = parseInt(document.getElementById('activitySelect').value);
        const comment    = document.getElementById('commentInput').value.trim();

        if (!activityId) { showToast('Debes seleccionar una actividad.', 'warning'); return; }
        if (!comment)    { showToast('El comentario es requerido.', 'warning'); return; }

        const btn = document.getElementById('confirmEndBtn');
        btn.disabled = true;
        btn.innerHTML = '<span class="spinner-border spinner-border-sm me-2"></span>Registrando...';

        try {
            await handleEndSession(activityId, comment);
            bootstrap.Modal.getInstance(document.getElementById('endSessionModal'))?.hide();
        } catch (e) {
            showToast(`Error al finalizar: ${e.message}`, 'danger');
            btn.disabled = false;
            btn.innerHTML = '<i class="bi bi-stop-circle me-2"></i>Finalizar y registrar';
        }
    });
}

// ── Init ──────────────────────────────────────────────────────────────────────

bindGridEvents();
bindLoadButton();
bindConfirmEndButton();
bindStatusFilterEvents();
bindSearchEvents();
bindPaginationEvents();

loadProjects();
loadStatuses();

if (getActiveSession()) {
    startTimer();
}
