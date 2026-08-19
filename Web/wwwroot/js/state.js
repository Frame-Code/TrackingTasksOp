// Manejo del estado de sesión activa (persistido en localStorage)
// y listas en memoria para proyectos y work packages

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
    workPackages: [],
    statuses: [],                    // estados de OpenProject
    activeStatusFilters: new Set(),  // vacío = mostrar todos
    searchQuery: '',
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

export function getActiveSession() {
    const raw = localStorage.getItem(SESSION_KEY);
    return raw ? JSON.parse(raw) : null;
}

export function saveSession(session) {
    localStorage.setItem(SESSION_KEY, JSON.stringify(session));
}

export function clearSession() {
    localStorage.removeItem(SESSION_KEY);
}

const PAUSED_KEY = 'trackingPausedTasks';

export function getPausedIds() {
    try { return new Set(JSON.parse(localStorage.getItem(PAUSED_KEY) || '[]')); }
    catch { return new Set(); }
}

export function markPaused(id) {
    const s = getPausedIds(); s.add(id);
    localStorage.setItem(PAUSED_KEY, JSON.stringify([...s]));
}

export function unmarkPaused(id) {
    const s = getPausedIds(); s.delete(id);
    localStorage.setItem(PAUSED_KEY, JSON.stringify([...s]));
}
