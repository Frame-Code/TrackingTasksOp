// Manejo del temporizador de sesión activa

import { getActiveSession } from './state.js';
import { formatDuration } from './helpers.js';
import { updateNavbar } from './render.js';
import { fetchPendingSummary } from './api.js';

let timerInterval   = null;
let notifInterval   = null;
let pendingInterval = null;

const NOTIF_STORAGE_KEY    = 'notifIntervalMinutes';
const DEFAULT_NOTIF_MINUTES = 15;

function getNotifIntervalMs() {
    const saved = parseInt(localStorage.getItem(NOTIF_STORAGE_KEY));
    const minutes = (!isNaN(saved) && saved > 0) ? saved : DEFAULT_NOTIF_MINUTES;
    return minutes * 60 * 1000;
}

function fireSessionNotification() {
    if (!('Notification' in window) || Notification.permission !== 'granted') return;

    const session = getActiveSession();
    if (!session) return;

    const secs    = Math.floor((Date.now() - new Date(session.startTime)) / 1000);
    const elapsed = formatDuration(secs);

    new Notification('⏱ Sesión activa — TrackingTasksOp', {
        body: `Llevas ${elapsed} trabajando en:\n"${session.subject}"`,
        icon: '/favicon.ico',
        tag:  'session-reminder',   // reemplaza la anterior en vez de apilarlas
        renotify: true
    });
}

function startNotifInterval() {
    stopNotifInterval();
    if (!('Notification' in window) || Notification.permission !== 'granted') return;
    notifInterval = setInterval(fireSessionNotification, getNotifIntervalMs());
}

function stopNotifInterval() {
    if (notifInterval) {
        clearInterval(notifInterval);
        notifInterval = null;
    }
}

async function checkPendingSessions() {
    if (!('Notification' in window) || Notification.permission !== 'granted') return;

    try {
        const summary = await fetchPendingSummary();
        if (!summary || summary.count === 0) return;

        new Notification('📤 Sesiones sin subir — TrackingTasksOp', {
            body: `Tienes ${summary.count} sesión(es) sin enviar a OpenProject (${summary.totalHours} h en total).`,
            icon: '/favicon.ico',
            tag:  'pending-upload-reminder', // reemplaza la anterior en vez de apilarlas
            renotify: true
        });
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
    checkPendingSessions();
    pendingInterval = setInterval(checkPendingSessions, getNotifIntervalMs());
}

function stopPendingReminder() {
    if (pendingInterval) {
        clearInterval(pendingInterval);
        pendingInterval = null;
    }
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
