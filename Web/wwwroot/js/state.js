// Manejo del estado de sesión activa (persistido en localStorage)
// y listas en memoria para proyectos y work packages

const SESSION_KEY = 'trackingActiveSession';

export const store = {
    projects: [],
    workPackages: [],
    statuses: [],                    // estados de OpenProject
    activeStatusFilters: new Set(),  // vacío = mostrar todos
    searchQuery: '',
    currentPage: 1,
    pageSize: 12
};

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
