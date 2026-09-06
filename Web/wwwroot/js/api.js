// Capa de acceso a la API. Solo realiza llamadas HTTP y retorna datos,
// sin efectos secundarios en el DOM.

import { extractId } from './helpers.js';

const API = '/api/v1';

// Caché en memoria de las páginas de tareas. Cada página cuesta ~1 s contra OpenProject y
// volver a una que ya se vio (paginar hacia atrás, apagar un filtro, cambiar de proyecto y
// regresar) no debería pagar ese viaje otra vez.
// ponytail: Map de la pestaña, se pierde con F5. Si hace falta que sobreviva a la recarga o
// se comparta entre pestañas/dispositivos, el siguiente escalón es cachear en Redis.
const wpCache = new Map();
const WP_CACHE_TTL = 60_000;

/**
 * Sesión expirada o no autenticado: redirige al login. El mensaje del servidor (ej. "La sesión
 * de OpenProject expiró y no se puede renovar") queda en sessionStorage para que auth.html lo
 * muestre — sin esto, un refresh de OAuth fallido desloguea sin ninguna explicación.
 */
async function redirectToLogin(res) {
    let message = 'Tu sesión expiró. Iniciá sesión de nuevo.';
    try {
        const body = await res.json();
        message = body.detail || body.message || message;
    } catch (_) { /* sin body o no es JSON */ }

    sessionStorage.removeItem('currentUser');
    sessionStorage.setItem('authNotice', message);
    window.location.replace('/auth.html');
}

async function apiFetch(url, options = {}) {
    const res = await fetch(url, {
        credentials: 'include',
        headers: { 'Content-Type': 'application/json', ...options.headers },
        ...options
    });

    if (res.status === 401) {
        await redirectToLogin(res);
        return;
    }

    if (!res.ok) {
        let msg = `Error ${res.status}`;
        try {
            const body = await res.json();
            msg = body.detail || body.message || body.title || msg;
        } catch (_) { /* ignorar errores de parseo */ }
        const err = new Error(msg);
        err.status = res.status;
        throw err;
    }

    // Cualquier mutación invalida las páginas cacheadas: el estado, el progreso o las fechas
    // que acaban de cambiar tienen que verse en la próxima consulta.
    if (options.method && options.method !== 'GET') wpCache.clear();

    if (res.status === 204) return null;
    return res.json();
}

export async function postLogout() {
    await fetch(`${API}/auth/logout`, {
        method: 'POST',
        credentials: 'include'
    });
    sessionStorage.removeItem('currentUser');
    window.location.replace('/auth.html');
}

export async function fetchProjects() {
    return apiFetch(`${API}/project`);
}

/**
 * Una página de tareas: { items, total, page, pageSize }.
 * El filtro de estado y la búsqueda van al servidor porque es lo que permite NO traer
 * las ~200 tareas para mostrar 12.
 */
export async function fetchWorkPackages({ projectId, page = 1, pageSize = 12, search = '', statusIds = [], force = false } = {}) {
    const qs = new URLSearchParams({ page, pageSize });
    if (projectId) qs.set('projectId', projectId);
    if (search?.trim()) qs.set('search', search.trim());
    if (statusIds.length) qs.set('statusIds', statusIds.join(','));

    const key = qs.toString();
    const cached = wpCache.get(key);
    if (!force && cached && Date.now() - cached.at < WP_CACHE_TTL) return cached.data;

    const data = await apiFetch(`${API}/workpackage?${qs}`);
    if (data) wpCache.set(key, { at: Date.now(), data });
    return data;
}

/**
 * Hijos directos de una tarea, para expandir un nodo del árbol. Sin filtro de asignado:
 * trae los hijos de cualquier persona (los que el usuario pueda ver en OpenProject).
 * El árbol cachea lo que recibe, así que colapsar y reabrir no vuelve a pedirlos.
 */
export async function fetchWorkPackageChildren(workPackageId) {
    return apiFetch(`${API}/workpackage/${workPackageId}/children`);
}

export async function fetchActivities(workPackageId) {
    return apiFetch(`${API}/activity?workPackageId=${workPackageId}`);
}

export async function fetchTask(workPackageId) {
    return apiFetch(`${API}/task/${workPackageId}`);
}

export async function postStartSession(wp) {
    const payload = {
        workPackageId: wp.id,
        name: wp.subject,
        description: wp.description?.raw || null,
        projectId: extractId(wp._links?.project?.href),
        statusId: extractId(wp._links?.status?.href),
        activityId: null,
        comment: null,
        startTracking: true
    };
    return apiFetch(`${API}/task/start_session`, {
        method: 'POST',
        body: JSON.stringify(payload)
    });
}

export async function postEndSession(workPackageId, activityId, comment) {
    return apiFetch(`${API}/task/end_session`, {
        method: 'POST',
        body: JSON.stringify({ workPackageId, activityId, comment })
    });
}

export async function fetchStatuses() {
    return apiFetch(`${API}/status`);
}

export async function patchWorkPackageStatus(wpId, statusId) {
    return apiFetch(`${API}/workpackage/${wpId}/status`, {
        method: 'PATCH',
        body: JSON.stringify({ statusId })
    });
}

export async function patchWorkPackageProgress(wpId, percentageDone) {
    return apiFetch(`${API}/workpackage/${wpId}/progress`, {
        method: 'PATCH',
        body: JSON.stringify({ percentageDone })
    });
}

export async function patchWorkPackageDates(wpId, startDate, dueDate) {
    return apiFetch(`${API}/workpackage/${wpId}/dates`, {
        method: 'PATCH',
        body: JSON.stringify({ startDate, dueDate })
    });
}

export async function postCancelSession(workPackageId) {
    return apiFetch(`${API}/task/cancel_session`, {
        method: 'POST',
        body: JSON.stringify({ workPackageId })
    });
}

function reportQuery(from, to, statusId) {
    return `from=${from}&to=${to}${statusId ? `&statusId=${statusId}` : ''}`;
}

/** Datos del reporte en JSON, para mostrarlos en pantalla antes de imprimir o descargar. */
export async function fetchReportPreview(from, to, statusId) {
    return apiFetch(`${API}/report/daily-tasks/preview?${reportQuery(from, to, statusId)}`);
}

export async function downloadDailyTaskReport(from, to, statusId) {
    const res = await fetch(`${API}/report/daily-tasks?${reportQuery(from, to, statusId)}`, { credentials: 'include' });

    if (res.status === 401) {
        await redirectToLogin(res);
        return;
    }

    if (!res.ok) {
        let msg = `Error ${res.status}`;
        try {
            const body = await res.json();
            msg = body.detail || body.message || body.title || msg;
        } catch (_) { /* ignorar errores de parseo */ }
        const err = new Error(msg);
        err.status = res.status;
        throw err;
    }

    const blob = await res.blob();
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `Reporte_Tareas_${from}_${to}.xlsx`;
    document.body.appendChild(a);
    a.click();
    a.remove();
    URL.revokeObjectURL(url);
}

export async function updateApiKey(apiKey) {
    return apiFetch(`${API}/auth/api-key`, {
        method: 'PUT',
        body: JSON.stringify({ apiKey })
    });
}

/** Conecta OAuth para la organización del usuario autenticado. Requiere ser admin en OpenProject. */
export async function connectOAuthInstance(alias, clientId, clientSecret) {
    return apiFetch(`${API}/opinstance`, {
        method: 'POST',
        body: JSON.stringify({ alias, clientId, clientSecret })
    });
}

export async function postPauseSession(workPackageId, uploadNow = true) {
    return apiFetch(`${API}/task/pause_session`, {
        method: 'POST',
        body: JSON.stringify({ workPackageId, uploadNow })
    });
}

export async function postResumeSession(workPackageId) {
    return apiFetch(`${API}/task/resume_session`, {
        method: 'POST',
        body: JSON.stringify({ workPackageId })
    });
}

/** Registra en OpenProject las sesiones de la tarea que quedaron guardadas solo en local. */
export async function postUploadPending(workPackageId) {
    return apiFetch(`${API}/task/upload_pending`, {
        method: 'POST',
        body: JSON.stringify({ workPackageId })
    });
}

/**
 * Avisa al servidor que la sesión abierta sigue viva. El servidor sella la hora; acá no se
 * manda ningún timestamp a propósito, para que el reloj del navegador no pueda inflar horas.
 *
 * Es la evidencia con la que se cierra una sesión que quedó abierta (server apagado, pestaña
 * cerrada): sin latidos no hay forma de saber hasta cuándo se trabajó de verdad.
 */
export async function postSessionHeartbeat() {
    return apiFetch(`${API}/task/heartbeat`, { method: 'POST' });
}

/** Registra tiempo a mano en OpenProject (sesión que no se cronometró). */
export async function postLogTime(payload) {
    return apiFetch(`${API}/task/log_time`, {
        method: 'POST',
        body: JSON.stringify(payload)
    });
}

/** Resumen de sesiones cerradas sin subir a OpenProject: { count, totalHours }. */
export async function fetchPendingSummary() {
    return apiFetch(`${API}/task/pending_summary`);
}

/** Detalle por tarea de las sesiones cerradas sin subir: [{ workPackageId, taskName, projectName, hours }]. */
export async function fetchPendingSessions() {
    return apiFetch(`${API}/task/pending_sessions`);
}

/** Preferencias del usuario: notificaciones por tipo, actividad por defecto, etc. */
export async function fetchUserSettings() {
    return apiFetch(`${API}/settings`);
}

export async function updateNotificationSetting(typeCode, enabled, intervalMinutes) {
    return apiFetch(`${API}/settings/notifications`, {
        method: 'PUT',
        body: JSON.stringify({ typeCode, enabled, intervalMinutes })
    });
}

export async function updateTaskPreferences(pauseDefaultBehavior, skipCancelConfirmation, addRandomSlackTime, defaultStatusIds) {
    return apiFetch(`${API}/settings/task-preferences`, {
        method: 'PUT',
        body: JSON.stringify({ pauseDefaultBehavior, skipCancelConfirmation, addRandomSlackTime, defaultStatusIds })
    });
}

/** apiKey vacío/null quita la key propia y vuelve a la compartida (con límite diario). */
export async function updateAiApiKey(apiKey) {
    return apiFetch(`${API}/settings/ai-api-key`, {
        method: 'PUT',
        body: JSON.stringify({ apiKey })
    });
}

// --- Mi cuenta ---

/** Devuelve { qrCodeDataUri, manualKey }. No activa el 2FA todavía. */
export async function setupTwoFactor() {
    return apiFetch(`${API}/account/2fa/setup`, { method: 'POST' });
}

/** Confirma el código y activa el 2FA. Devuelve { recoveryCodes }, que solo se emiten una vez. */
export async function enableTwoFactor(code) {
    return apiFetch(`${API}/account/2fa/enable`, {
        method: 'POST',
        body: JSON.stringify({ code })
    });
}

/** Emite códigos nuevos e invalida los anteriores. Devuelve { recoveryCodes }. */
export async function regenerateRecoveryCodes(code) {
    return apiFetch(`${API}/account/2fa/recovery-codes`, {
        method: 'POST',
        body: JSON.stringify({ code })
    });
}

/** Desvincula la app de autenticación para enrolar otro teléfono. Deja el 2FA desactivado. */
export async function resetAuthenticator(currentPassword, twoFactorCode) {
    return apiFetch(`${API}/account/2fa/reset`, {
        method: 'POST',
        body: JSON.stringify({ currentPassword, twoFactorCode })
    });
}

/** twoFactorCode acepta el código de la app o uno de recuperación. */
export async function changePassword(currentPassword, newPassword, twoFactorCode) {
    return apiFetch(`${API}/account/password`, {
        method: 'PUT',
        body: JSON.stringify({ currentPassword, newPassword, twoFactorCode })
    });
}

/** Solo admins de OpenProject. Resetea la contraseña de otro usuario de la misma instancia. */
export async function adminResetPassword(email, newPassword) {
    return apiFetch(`${API}/account/admin/reset-password`, {
        method: 'POST',
        body: JSON.stringify({ email, newPassword })
    });
}

/** jpegBase64 sin el prefijo "data:". El navegador ya lo redimensionó a 256px. */
export async function updateAvatar(jpegBase64) {
    return apiFetch(`${API}/account/avatar`, {
        method: 'PUT',
        body: JSON.stringify({ jpegBase64 })
    });
}

export async function deleteAvatar() {
    return apiFetch(`${API}/account/avatar`, { method: 'DELETE' });
}
