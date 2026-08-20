// Manejo del estado de sesión activa (persistido en localStorage)
// y listas en memoria para proyectos y work packages

import { getCurrentUser } from './auth-guard.js';

const SESSION_KEY = 'trackingActiveSession';

export const NOTIFICATION_TYPES = {
    SESSION_REMINDER: 'session-reminder',
    PENDING_UPLOAD_REMINDER: 'pending-upload-reminder'
};

const THEME_KEY = 'appTheme';
const PAGE_SIZE_KEY = 'appPageSize';

function getStoredPageSize() {
    const saved = parseInt(localStorage.getItem(PAGE_SIZE_KEY));
    return (!isNaN(saved) && saved > 0) ? saved : 12;
}

export const store = {
    projects: [],
    // Solo la página que se está mostrando; el resto se queda en OpenProject.
    workPackages: [],
    total: 0,                        // total de tareas que cumplen los filtros actuales
    statuses: [],                    // estados de OpenProject
    activeStatusFilters: new Set(),  // IDs de estado; vacío = todos
    searchQuery: '',
    projectId: null,
    currentPage: 1,
    pageSize: getStoredPageSize(),
    userSettings: null               // null hasta que loadUserSettings() resuelva (ver app.js)
};

/** 'light' | 'dark'. Aplicado también inline en <head> de cada página para evitar el flash. */
export function getTheme() {
    return localStorage.getItem(THEME_KEY) || 'dark';
}

export function setTheme(theme) {
    localStorage.setItem(THEME_KEY, theme);
    document.documentElement.setAttribute('data-bs-theme', theme);
}

export function setPageSize(size) {
    localStorage.setItem(PAGE_SIZE_KEY, String(size));
    store.pageSize = size;
    store.currentPage = 1;
}

const SIDEBAR_COLLAPSED_KEY = 'sidebarCollapsed';

/** Aplicado también inline en <head> de index.html para evitar el flash de ancho. */
export function getSidebarCollapsed() {
    return localStorage.getItem(SIDEBAR_COLLAPSED_KEY) === 'true';
}

export function setSidebarCollapsed(collapsed) {
    localStorage.setItem(SIDEBAR_COLLAPSED_KEY, String(collapsed));
    document.documentElement.classList.toggle('sidebar-collapsed', collapsed);
}

/** Una sola clave global (sin el userId) hacía que el último usuario en escribir pisara la
 *  sesión del anterior — no alcanza con validar el dueño al leer si al guardar se sigue
 *  usando la misma casilla para todos. Una casilla por usuario evita el pisado sin importar
 *  cuántas cuentas alternen sesión de tracking en el mismo navegador. */
function sessionKey(userId) {
    return `${SESSION_KEY}:${userId}`;
}

export function getActiveSession() {
    const userId = getCurrentUser()?.userId;
    if (!userId) return null;
    const raw = localStorage.getItem(sessionKey(userId));
    return raw ? JSON.parse(raw) : null;
}

export function saveSession(session) {
    const userId = getCurrentUser()?.userId;
    if (!userId) return;
    localStorage.setItem(sessionKey(userId), JSON.stringify(session));
}

export function clearSession() {
    const userId = getCurrentUser()?.userId;
    if (!userId) return;
    localStorage.removeItem(sessionKey(userId));
}

const PAUSED_KEY = 'trackingPausedTasks';

function pausedKey(userId) {
    return `${PAUSED_KEY}:${userId}`;
}

export function getPausedIds() {
    const userId = getCurrentUser()?.userId;
    if (!userId) return new Set();
    try { return new Set(JSON.parse(localStorage.getItem(pausedKey(userId)) || '[]')); }
    catch { return new Set(); }
}

function savePausedIds(ids) {
    const userId = getCurrentUser()?.userId;
    if (!userId) return;
    localStorage.setItem(pausedKey(userId), JSON.stringify([...ids]));
}

export function markPaused(id) {
    const s = getPausedIds(); s.add(id);
    savePausedIds(s);
}

export function unmarkPaused(id) {
    const s = getPausedIds(); s.delete(id);
    savePausedIds(s);
}
