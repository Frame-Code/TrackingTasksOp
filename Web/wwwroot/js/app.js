// Punto de entrada: orquesta módulos y maneja todos los eventos del usuario

import { store, getActiveSession, saveSession, clearSession } from './state.js';
import { fetchProjects, fetchWorkPackages, fetchActivities, fetchTask,
         postStartSession, postEndSession, fetchStatuses,
         patchWorkPackageStatus, patchWorkPackageProgress,
         patchWorkPackageDates, postCancelSession } from './api.js';
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

async function requestNotifPermission() {
    if (!('Notification' in window)) return;
    if (Notification.permission === 'default') {
        const result = await Notification.requestPermission();
        if (result === 'granted') {
            showToast(
                'Notificaciones activadas. Asegúrate de que tu navegador tenga permiso de notificaciones en la configuración de Windows.',
                'info'
            );
        } else if (result === 'denied') {
            showToast('Notificaciones bloqueadas. No podrás recibir recordatorios de sesión activa.', 'warning');
        }
    }
}

async function handleStartSession(wpId) {
    const wp = store.workPackages.find(w => w.id === wpId);
    if (!wp) return;

    // Pedir permiso de notificaciones (solo la primera vez, desde un gesto del usuario)
    await requestNotifPermission();

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

async function handleCancelSession(wpId) {
    try {
        await postCancelSession(wpId);
        clearSession();
        stopTimer();
        renderCards();
        showToast('Sesión cancelada. No se registró ningún tiempo.', 'secondary');
    } catch (e) {
        showToast(`Error al cancelar: ${e.message}`, 'danger');
    }
}

async function handleChangeProgress(wpId, pct) {
    const wp = store.workPackages.find(w => w.id === wpId);
    if (!wp) return;

    const originalPct = wp.percentageDone ?? 0;

    try {
        await patchWorkPackageProgress(wpId, pct);
        wp.percentageDone = pct;
        // Actualizar solo el display %, no re-renderizar todo (el slider ya está en la posición correcta)
        const display = document.querySelector(`.wp-progress-input[data-wp-id="${wpId}"]`)
            ?.closest('.card-body')
            ?.querySelector('.wp-pct-display');
        if (display) display.textContent = `${pct}%`;
        showToast(`Progreso actualizado a <strong>${pct}%</strong>`, 'success');
    } catch (e) {
        showToast(`Error al actualizar progreso: ${e.message}`, 'danger');
        // Revertir el slider
        const slider = document.querySelector(`.wp-progress-input[data-wp-id="${wpId}"]`);
        if (slider) {
            slider.value = originalPct;
            const display = slider.closest('.card-body')?.querySelector('.wp-pct-display');
            if (display) display.textContent = `${originalPct}%`;
        }
    }
}

// field = 'startDate' | 'dueDate' | 'both'
// cuando field='both', startValue y dueValue son los dos valores nuevos
async function handleChangeDate(wpId, field, startValue, dueValue) {
    const wp = store.workPackages.find(w => w.id === wpId);
    if (!wp) return;

    let newStart, newDue;

    if (field === 'both') {
        newStart = startValue || null;
        newDue   = dueValue   || null;
    } else {
        const isStart = field === 'startDate';
        const value   = startValue; // único valor cuando field != 'both'
        newStart = isStart ? (value || null) : (wp.startDate || null);
        newDue   = isStart ? (wp.dueDate || null) : (value || null);
    }

    try {
        await patchWorkPackageDates(wpId, newStart, newDue);

        wp.startDate = newStart || '';
        wp.dueDate   = newDue   || '';

        // Actualizar el botón de fechas en la tarjeta sin re-renderizar todo
        const datesBtn = document.querySelector(`.btn-dates[data-id="${wpId}"]`);
        if (datesBtn) {
            datesBtn.dataset.start = wp.startDate;
            datesBtn.dataset.due   = wp.dueDate;
        }

        renderCards(); // re-render para actualizar el texto de fechas
        showToast('Fechas actualizadas correctamente.', 'success');
    } catch (e) {
        showToast(`Error al actualizar fechas: ${e.message}`, 'danger');
        throw e; // para que el llamador del modal pueda revertir el botón
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
    const grid = document.getElementById('wpGrid');

    // ── Clicks ────────────────────────────────────────────────────────────────
    grid.addEventListener('click', async (e) => {
        const startBtn     = e.target.closest('.btn-start');
        const endBtn       = e.target.closest('.btn-end');
        const cancelBtn    = e.target.closest('.btn-cancel');
        const historyBtn   = e.target.closest('.btn-history');
        const setStatusBtn = e.target.closest('.btn-set-status');

        if (startBtn)     await handleStartSession(parseInt(startBtn.dataset.id));
        if (endBtn)       openEndModal();
        if (cancelBtn)    await handleCancelSession(parseInt(cancelBtn.dataset.id));
        if (historyBtn)   await handleOpenHistory(parseInt(historyBtn.dataset.id));

        const datesBtn = e.target.closest('.btn-dates');
        if (datesBtn)     openDatesModal(parseInt(datesBtn.dataset.id), datesBtn.dataset.start, datesBtn.dataset.due);

        if (setStatusBtn) await handleChangeStatus(
            parseInt(setStatusBtn.dataset.wpId),
            parseInt(setStatusBtn.dataset.statusId),
            setStatusBtn.dataset.statusName
        );
    });

    // ── Slider progreso: actualiza % y fill azul en tiempo real ──────────────
    grid.addEventListener('input', (e) => {
        const slider = e.target.closest('.wp-progress-input');
        if (!slider) return;
        const pct = slider.value;
        slider.style.background =
            `linear-gradient(to right, #0d6efd ${pct}%, rgba(255,255,255,0.15) ${pct}%)`;
        const display = slider.closest('.card-body')?.querySelector('.wp-pct-display');
        if (display) display.textContent = `${pct}%`;
    });

    // ── Slider progreso: guarda al soltar ────────────────────────────────────
    grid.addEventListener('change', async (e) => {
        const slider = e.target.closest('.wp-progress-input');
        if (slider) {
            await handleChangeProgress(
                parseInt(slider.dataset.wpId),
                parseInt(slider.value)
            );
        }
    });
}

// ── Modal: Editar fechas ──────────────────────────────────────────────────────

let _datesModalWpId = null;

function openDatesModal(wpId, currentStart, currentDue) {
    _datesModalWpId = wpId;

    const wp = store.workPackages.find(w => w.id === wpId);
    document.getElementById('datesModalTaskName').textContent =
        wp ? `#${wp.id} — ${wp.subject}` : `#${wpId}`;
    document.getElementById('datesModalStart').value = currentStart || '';
    document.getElementById('datesModalDue').value   = currentDue   || '';

    const confirmBtn = document.getElementById('confirmDatesBtn');
    confirmBtn.disabled = false;
    confirmBtn.innerHTML = '<i class="bi bi-check-lg me-1"></i>Guardar fechas';

    new bootstrap.Modal(document.getElementById('datesModal')).show();
}

function bindConfirmDatesButton() {
    document.getElementById('confirmDatesBtn').addEventListener('click', async () => {
        if (!_datesModalWpId) return;

        const startVal = document.getElementById('datesModalStart').value;
        const dueVal   = document.getElementById('datesModalDue').value;
        const btn      = document.getElementById('confirmDatesBtn');

        btn.disabled = true;
        btn.innerHTML = '<span class="spinner-border spinner-border-sm me-2"></span>Guardando...';

        try {
            await handleChangeDate(_datesModalWpId, 'both', startVal, dueVal);
            bootstrap.Modal.getInstance(document.getElementById('datesModal'))?.hide();
        } catch (_) {
            // handleChangeDate ya muestra el toast de error
            btn.disabled = false;
            btn.innerHTML = '<i class="bi bi-check-lg me-1"></i>Guardar fechas';
        }
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
bindConfirmDatesButton();
bindStatusFilterEvents();
bindSearchEvents();
bindPaginationEvents();

loadProjects();
loadStatuses();

if (getActiveSession()) {
    startTimer();
}
