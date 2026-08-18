// Punto de entrada: orquesta módulos y maneja todos los eventos del usuario

import { store, getActiveSession, saveSession, clearSession,
         markPaused, unmarkPaused } from './state.js';
import { fetchProjects, fetchWorkPackages, fetchActivities, fetchTask,
         postStartSession, postEndSession, fetchStatuses,
         patchWorkPackageStatus, patchWorkPackageProgress,
         patchWorkPackageDates, postCancelSession,
         downloadDailyTaskReport, updateApiKey,
         postPauseSession, postResumeSession, postUploadPending,
         postLogTime } from './api.js';
import { updateNavbar, renderProjectSelect, renderCards, renderStatusFilters,
         renderHistoryLoading, renderHistoryContent, renderHistoryError,
         renderActivitiesSelect } from './render.js';
import { startTimer, stopTimer, startPendingReminder } from './timer.js';
import { showToast, setLoading, showError, hideError } from './ui.js';
import { escHtml, formatDuration, statusClass, extractId } from './helpers.js';

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
            startPendingReminder();
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

// ── Pausar / Continuar ────────────────────────────────────────────────────────

let _pauseWpId = null;

function openPauseModal(wpId) {
    const wp = store.workPackages.find(w => w.id === wpId);
    _pauseWpId = wpId;
    document.getElementById('pauseModalTaskName').textContent =
        wp ? `#${wp.id} — ${wp.subject}` : `#${wpId}`;
    new bootstrap.Modal(document.getElementById('pauseModal')).show();
}

async function handlePauseSession(wpId, uploadNow) {
    try {
        await postPauseSession(wpId, uploadNow);
        markPaused(wpId);
        clearSession();
        stopTimer();
        bootstrap.Modal.getInstance(document.getElementById('pauseModal'))?.hide();
        renderCards();
        const msg = uploadNow ? ' Tiempo subido a OpenProject.' : ' Tiempo guardado localmente.';
        showToast(`Sesión pausada.${msg}`, 'warning');
    } catch (e) {
        showToast(`Error al pausar: ${e.message}`, 'danger');
    }
}

async function handleResumeSession(wpId) {
    const wp = store.workPackages.find(w => w.id === wpId);
    if (!wp) return;

    const btn = document.querySelector(`.btn-resume[data-id="${wpId}"]`);
    if (btn) { btn.disabled = true; btn.innerHTML = '<span class="spinner-border spinner-border-sm"></span>'; }

    try {
        await postResumeSession(wpId);
        unmarkPaused(wpId);
        saveSession({ workPackageId: wpId, subject: wp.subject, startTime: new Date().toISOString() });
        startTimer();
        renderCards();
        showToast(`Sesión reanudada: <strong>${escHtml(wp.subject)}</strong>`, 'success');
    } catch (e) {
        showToast(`Error al reanudar: ${e.message}`, 'danger');
        if (btn) { btn.disabled = false; btn.innerHTML = '<i class="bi bi-play-circle-fill me-1"></i>Continuar'; }
    }
}

function bindPauseModalButtons() {
    document.getElementById('pauseUploadBtn').addEventListener('click', () => {
        if (_pauseWpId) handlePauseSession(_pauseWpId, true);
    });
    document.getElementById('pauseLocalBtn').addEventListener('click', () => {
        if (_pauseWpId) handlePauseSession(_pauseWpId, false);
    });
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

// El modal de historial lo monta Bootstrap fuera de #wpGrid, así que su delegación de
// clicks va sobre el propio modal: colgarla del grid dejaba el botón sin responder.
function bindHistoryModalEvents() {
    document.getElementById('historyModal').addEventListener('click', async (e) => {
        const uploadBtn = e.target.closest('.btn-upload-pending');
        if (uploadBtn) await handleUploadPending(parseInt(uploadBtn.dataset.id), uploadBtn);
    });
}

async function handleUploadPending(wpId, btn) {
    // Feedback inmediato y botón bloqueado: la subida llama a OpenProject una vez por
    // sesión, así que puede tardar, y un doble clic dispararía entradas repetidas.
    const originalHtml = btn.innerHTML;
    btn.disabled = true;
    btn.innerHTML = '<span class="spinner-border spinner-border-sm me-1"></span>Subiendo…';

    // La subida y el refresco se manejan por separado a propósito: si el tiempo ya se
    // registró en OpenProject y falla el refresco, decir "no se pudo subir" sería mentira
    // y llevaría a reintentar, duplicando entradas.
    let uploaded;
    try {
        ({ uploaded } = await postUploadPending(wpId));
    } catch (e) {
        btn.disabled = false;
        btn.innerHTML = originalHtml;
        setHistoryNotice('danger', 'bi-exclamation-triangle-fill',
            `No se pudo subir: ${escHtml(e.message)}. El tiempo sigue guardado en local; puedes reintentar.`);
        showToast(`No se pudo subir el tiempo pendiente: ${e.message}`, 'danger');
        return;
    }

    const label = `${uploaded} sesión${uploaded !== 1 ? 'es' : ''}`;
    const okMsg = uploaded > 0
        ? `${label} registrada${uploaded !== 1 ? 's' : ''} en OpenProject.`
        : 'No había sesiones pendientes por subir.';

    showToast(okMsg, uploaded > 0 ? 'success' : 'info');
    btn.innerHTML = '<i class="bi bi-check-lg me-1"></i>Subido';

    try {
        renderHistoryContent(await fetchTask(wpId));
        setHistoryNotice('success', 'bi-cloud-check-fill', okMsg);
    } catch {
        // El registro sí ocurrió: se dice explícitamente para que nadie reintente.
        setHistoryNotice('success', 'bi-cloud-check-fill',
            `${okMsg} No se pudo refrescar la lista — vuelve a abrir el historial para verla actualizada.`);
    }
}

/** Aviso persistente dentro del modal: el toast se desvanece y puede pasar desapercibido. */
function setHistoryNotice(type, icon, html) {
    const body = document.getElementById('historyBody');
    if (!body) return;

    body.querySelector('.history-notice')?.remove();
    body.insertAdjacentHTML('afterbegin',
        `<div class="alert alert-${type} history-notice d-flex align-items-center gap-2 py-2 mb-3" role="status">
             <i class="bi ${icon}"></i><span>${html}</span>
         </div>`);
}

// ── Modal: Registrar tiempo a mano ────────────────────────────────────────────

let _logTimeWpId = null;

async function openLogTimeModal(wpId) {
    const wp = store.workPackages.find(w => w.id === wpId);
    if (!wp) return;

    _logTimeWpId = wpId;
    document.getElementById('logTimeTaskName').textContent = `#${wp.id} — ${wp.subject}`;

    // Por defecto hoy: el caso típico es "se me olvidó trackear lo de hoy".
    // max evita elegir una fecha futura desde el propio control, antes de enviar.
    const today = new Date().toISOString().slice(0, 10);
    const dateInput = document.getElementById('logTimeDate');
    dateInput.value = today;
    dateInput.max = today;

    ['logTimeStart', 'logTimeEnd', 'logTimeHours', 'logTimeComment']
        .forEach(id => { document.getElementById(id).value = ''; });
    document.getElementById('logTimeHours').readOnly = false;
    hideLogTimeError();

    new bootstrap.Modal(document.getElementById('logTimeModal')).show();

    // Las actividades dependen de la tarea, así que se cargan al abrir.
    const sel = document.getElementById('logTimeActivity');
    sel.innerHTML = '<option value="">Cargando actividades…</option>';
    try {
        const activities = await fetchActivities(wpId);
        sel.innerHTML = '<option value="">Actividad por defecto</option>' +
            activities.map(a => `<option value="${a.id}">${escHtml(a.name)}</option>`).join('');
    } catch {
        // Sin actividades el backend elige una por defecto: se avisa, no se bloquea el registro.
        sel.innerHTML = '<option value="">Actividad por defecto</option>';
    }
}

/** Si hay hora de inicio y fin, las horas se derivan de ellas y el campo pasa a solo lectura. */
function syncHoursFromRange() {
    const start = document.getElementById('logTimeStart').value;
    const end   = document.getElementById('logTimeEnd').value;
    const hours = document.getElementById('logTimeHours');

    if (!start || !end) {
        hours.readOnly = false;
        return;
    }

    const [sh, sm] = start.split(':').map(Number);
    const [eh, em] = end.split(':').map(Number);
    const diff = (eh * 60 + em) - (sh * 60 + sm);

    if (diff <= 0) {
        showLogTimeError('La hora de finalización debe ser posterior a la de inicio.');
        hours.value = '';
        hours.readOnly = false;
        return;
    }

    hideLogTimeError();
    hours.value = (diff / 60).toFixed(2);
    hours.readOnly = true;
}

async function handleLogTime() {
    const btn = document.getElementById('confirmLogTimeBtn');
    const spentOn = document.getElementById('logTimeDate').value;
    const hours   = parseFloat(document.getElementById('logTimeHours').value);
    const start   = document.getElementById('logTimeStart').value;
    const end     = document.getElementById('logTimeEnd').value;
    const activityId = document.getElementById('logTimeActivity').value;

    // Validación en el cliente para mostrar el error junto al formulario, sin viaje al
    // servidor. El backend valida igual: esto es comodidad, no la barrera.
    if (!spentOn) return showLogTimeError('Indica la fecha en que trabajaste.');
    if (!hours || hours <= 0) return showLogTimeError('Indica cuántas horas trabajaste.');
    if (hours > 24) return showLogTimeError('No se pueden registrar más de 24 horas en una entrada.');

    const wp = store.workPackages.find(w => w.id === _logTimeWpId);
    const originalHtml = btn.innerHTML;
    btn.disabled = true;
    btn.innerHTML = '<span class="spinner-border spinner-border-sm me-1"></span>Registrando…';
    hideLogTimeError();

    try {
        const result = await postLogTime({
            workPackageId: _logTimeWpId,
            spentOn,
            hours,
            startTime: start || null,
            endTime: end || null,
            activityId: activityId ? parseInt(activityId) : null,
            comment: document.getElementById('logTimeComment').value,
            // Solo se usan si la tarea aún no está registrada localmente.
            projectId: extractId(wp?._links?.project?.href),
            statusId: extractId(wp?._links?.status?.href),
            name: wp?.subject
        });

        bootstrap.Modal.getInstance(document.getElementById('logTimeModal'))?.hide();
        showToast(
            `${result.hours} h registradas en OpenProject para <strong>#${_logTimeWpId}</strong>`,
            'success'
        );
    } catch (e) {
        // El error se queda en el modal, junto al formulario que hay que corregir.
        showLogTimeError(e.message);
    } finally {
        btn.disabled = false;
        btn.innerHTML = originalHtml;
    }
}

function showLogTimeError(msg) {
    const el = document.getElementById('logTimeError');
    el.textContent = msg;
    el.classList.remove('d-none');
}

function hideLogTimeError() {
    document.getElementById('logTimeError').classList.add('d-none');
}

function bindLogTimeModal() {
    document.getElementById('confirmLogTimeBtn').addEventListener('click', handleLogTime);
    ['logTimeStart', 'logTimeEnd'].forEach(id =>
        document.getElementById(id).addEventListener('change', syncHoursFromRange));
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
        const pauseBtn     = e.target.closest('.btn-pause');
        const resumeBtn    = e.target.closest('.btn-resume');
        const historyBtn   = e.target.closest('.btn-history');
        const setStatusBtn = e.target.closest('.btn-set-status');

        if (startBtn)     await handleStartSession(parseInt(startBtn.dataset.id));
        if (endBtn)       openEndModal();
        if (cancelBtn)    await handleCancelSession(parseInt(cancelBtn.dataset.id));
        if (pauseBtn)     openPauseModal(parseInt(pauseBtn.dataset.id));
        if (resumeBtn)    await handleResumeSession(parseInt(resumeBtn.dataset.id));
        if (historyBtn)   await handleOpenHistory(parseInt(historyBtn.dataset.id));

        const logTimeBtn = e.target.closest('.btn-log-time');
        if (logTimeBtn) await openLogTimeModal(parseInt(logTimeBtn.dataset.id));

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

// ── Modal: Actualizar API key ─────────────────────────────────────────────────

function openApiKeyModal() {
    document.getElementById('apiKeyInput').value = '';
    document.getElementById('apiKeyError').classList.add('d-none');

    const confirmBtn = document.getElementById('confirmApiKeyBtn');
    confirmBtn.disabled = false;
    confirmBtn.innerHTML = '<i class="bi bi-check-lg me-1"></i>Actualizar';

    new bootstrap.Modal(document.getElementById('apiKeyModal')).show();
}

function bindApiKeyButton() {
    document.getElementById('apiKeyBtn').addEventListener('click', openApiKeyModal);
}

function bindConfirmApiKeyButton() {
    document.getElementById('confirmApiKeyBtn').addEventListener('click', async () => {
        const apiKey = document.getElementById('apiKeyInput').value.trim();
        const errorBox = document.getElementById('apiKeyError');
        errorBox.classList.add('d-none');

        if (!apiKey) {
            errorBox.textContent = 'Debes indicar la API key.';
            errorBox.classList.remove('d-none');
            return;
        }

        const btn = document.getElementById('confirmApiKeyBtn');
        btn.disabled = true;
        btn.innerHTML = '<span class="spinner-border spinner-border-sm me-2"></span>Actualizando...';

        try {
            await updateApiKey(apiKey);
            bootstrap.Modal.getInstance(document.getElementById('apiKeyModal'))?.hide();
            showToast('API key actualizada correctamente.', 'success');
        } catch (e) {
            errorBox.textContent = `Error al actualizar: ${e.message}`;
            errorBox.classList.remove('d-none');
        } finally {
            btn.disabled = false;
            btn.innerHTML = '<i class="bi bi-check-lg me-1"></i>Actualizar';
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

// ── Sincronización entre pestañas (ej. acciones del bot en /bot.html) ──────────

function bindStorageSync() {
    window.addEventListener('storage', (e) => {
        if (e.key !== 'trackingActiveSession' && e.key !== 'trackingPausedTasks') return;

        if (getActiveSession()) {
            startTimer();
        } else {
            stopTimer();
        }
        if (store.workPackages.length) renderCards();
    });
}

// ── Init ──────────────────────────────────────────────────────────────────────

bindGridEvents();
bindLoadButton();
bindConfirmEndButton();
bindConfirmDatesButton();
bindReportButton();
bindConfirmReportButton();
bindApiKeyButton();
bindConfirmApiKeyButton();
bindStatusFilterEvents();
bindSearchEvents();
bindPaginationEvents();
bindStorageSync();
bindPauseModalButtons();
bindHistoryModalEvents();
bindLogTimeModal();

loadProjects();
loadStatuses();
startPendingReminder();

if (getActiveSession()) {
    startTimer();
}
