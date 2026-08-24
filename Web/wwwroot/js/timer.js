// Manejo del temporizador de sesión activa

import { getActiveSession, store, NOTIFICATION_TYPES } from './state.js';
import { formatDuration } from './helpers.js';
import { updateNavbar } from './render.js';
import { fetchPendingSummary } from './api.js';
import { refreshPendingBadge, openPendingSessionsModal } from './pending-sessions.js';

let timerInterval   = null;
let notifInterval   = null;
let pendingInterval = null;

const DEFAULT_NOTIF_MINUTES = 15;

/**
 * Preferencia guardada para un tipo de notificación, o el default (activada, 15 min) si
 * store.userSettings todavía no cargó o el usuario nunca la cambió — así un fallo de red al
 * pedir /settings no deja a nadie sin recordatorios.
 */
function getNotifPref(typeCode) {
    const saved = store.userSettings?.notifications?.[typeCode];
    return saved ?? { enabled: true, intervalMinutes: DEFAULT_NOTIF_MINUTES };
}

function fireSessionNotification() {
    if (!('Notification' in window) || Notification.permission !== 'granted') return;
    if (!getNotifPref(NOTIFICATION_TYPES.SESSION_REMINDER).enabled) return;

    const session = getActiveSession();
    if (!session) return;

    const secs    = Math.floor((Date.now() - new Date(session.startTime)) / 1000);
    const elapsed = formatDuration(secs);

    new Notification('⏱ Sesión activa — TrackingTasksOp', {
        body: `Llevas ${elapsed} trabajando en:\n"${session.subject}"`,
        icon: '/favicon.ico',
        tag:  NOTIFICATION_TYPES.SESSION_REMINDER,   // reemplaza la anterior en vez de apilarlas
        renotify: true
    });
}

function startNotifInterval() {
    stopNotifInterval();
    if (!('Notification' in window) || Notification.permission !== 'granted') return;
    const pref = getNotifPref(NOTIFICATION_TYPES.SESSION_REMINDER);
    if (!pref.enabled) return;
    notifInterval = setInterval(fireSessionNotification, pref.intervalMinutes * 60 * 1000);
}

function stopNotifInterval() {
    if (notifInterval) {
        clearInterval(notifInterval);
        notifInterval = null;
    }
}

async function checkPendingSessions() {
    if (!('Notification' in window) || Notification.permission !== 'granted') return;
    if (!getNotifPref(NOTIFICATION_TYPES.PENDING_UPLOAD_REMINDER).enabled) return;

    try {
        const summary = await fetchPendingSummary();
        refreshPendingBadge(summary); // reusa el summary: sin fetch adicional
        if (!summary || summary.count === 0) return;

        const notif = new Notification('📤 Sesiones sin subir — TrackingTasksOp', {
            body: `Tienes ${summary.count} sesión(es) sin enviar a OpenProject (${summary.totalHours} h en total).`,
            icon: '/favicon.ico',
            tag:  NOTIFICATION_TYPES.PENDING_UPLOAD_REMINDER, // reemplaza la anterior en vez de apilarlas
            renotify: true
        });
        notif.onclick = () => {
            window.focus();
            openPendingSessionsModal();
        };
    } catch {
        // Un fallo de red no debe interrumpir al usuario; se reintenta en el próximo ciclo.
    }
}

/**
 * Recordatorio recurrente de sesiones cerradas sin subir a OpenProject. A diferencia del
 * de "sesión activa", corre siempre que haya permiso de notificaciones, sin depender de que
 * haya una sesión en curso: el usuario pudo haber elegido "guardar en local" y cerrado la app.
 */
export function startPendingReminder() {
    stopPendingReminder();
    if (!('Notification' in window) || Notification.permission !== 'granted') return;
    const pref = getNotifPref(NOTIFICATION_TYPES.PENDING_UPLOAD_REMINDER);
    if (!pref.enabled) return;
    checkPendingSessions();
    pendingInterval = setInterval(checkPendingSessions, pref.intervalMinutes * 60 * 1000);
}

function stopPendingReminder() {
    if (pendingInterval) {
        clearInterval(pendingInterval);
        pendingInterval = null;
    }
}

/**
 * Reaplica las preferencias de notificación vigentes (llamado desde el sidebar tras guardar
 * un cambio). El recordatorio de sesión activa solo se reinicia si hay una sesión en curso;
 * el de pendientes siempre, porque no depende de eso.
 */
export function refreshNotificationTimers() {
    if (getActiveSession()) startNotifInterval();
    startPendingReminder();
}

export function startTimer() {
    stopTimer();
    updateTimerDisplay();
    timerInterval = setInterval(updateTimerDisplay, 1000);
    startNotifInterval();
    updateNavbar();
}

export function stopTimer() {
    if (timerInterval) {
        clearInterval(timerInterval);
        timerInterval = null;
    }
    stopNotifInterval();
    updateNavbar();
}

export function updateTimerDisplay() {
    const session = getActiveSession();
    if (!session) return;

    const secs    = Math.floor((Date.now() - new Date(session.startTime)) / 1000);
    const timeStr = formatDuration(secs);

    const navTimer = document.getElementById('navTimer');
    if (navTimer) navTimer.textContent = timeStr;

    const cardTimer = document.querySelector(
        `[data-wp-id="${session.workPackageId}"] .card-timer`
    );
    if (cardTimer) cardTimer.textContent = timeStr;
}
