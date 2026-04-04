// Punto de entrada: orquesta módulos y maneja todos los eventos del usuario

import { store, getActiveSession, saveSession, clearSession } from './state.js';
import { fetchProjects, fetchWorkPackages, fetchActivities, fetchTask,
         postStartSession, postEndSession } from './api.js';
import { updateNavbar, renderProjectSelect, renderCards,
         renderHistoryLoading, renderHistoryContent, renderHistoryError,
         renderActivitiesSelect } from './render.js';
import { startTimer, stopTimer } from './timer.js';
import { showToast, setLoading, showError, hideError } from './ui.js';
import { escHtml, formatDuration } from './helpers.js';

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

async function loadWorkPackages(projectId) {
    setLoading(true);
    hideError();
    try {
        store.workPackages = await fetchWorkPackages(projectId);
        renderCards();
    } catch (e) {
        showError(`No se pudieron cargar las tareas: ${e.message}`);
    } finally {
        setLoading(false);
    }
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

function bindGridEvents() {
    document.getElementById('wpGrid').addEventListener('click', async (e) => {
        const startBtn   = e.target.closest('.btn-start');
        const endBtn     = e.target.closest('.btn-end');
        const historyBtn = e.target.closest('.btn-history');

        if (startBtn)   await handleStartSession(parseInt(startBtn.dataset.id));
        if (endBtn)     openEndModal();
        if (historyBtn) await handleOpenHistory(parseInt(historyBtn.dataset.id));
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

loadProjects();

if (getActiveSession()) {
    startTimer();
}
