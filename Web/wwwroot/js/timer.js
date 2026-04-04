// Manejo del temporizador de sesión activa

import { getActiveSession } from './state.js';
import { formatDuration } from './helpers.js';
import { updateNavbar } from './render.js';

let timerInterval = null;

export function startTimer() {
    stopTimer();
    updateTimerDisplay();
    timerInterval = setInterval(updateTimerDisplay, 1000);
    updateNavbar();
}

export function stopTimer() {
    if (timerInterval) {
        clearInterval(timerInterval);
        timerInterval = null;
    }
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
