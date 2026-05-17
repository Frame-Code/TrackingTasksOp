// Manejo del temporizador de sesión activa

import { getActiveSession } from './state.js';
import { formatDuration } from './helpers.js';
import { updateNavbar } from './render.js';

let timerInterval  = null;
let notifInterval  = null;

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
