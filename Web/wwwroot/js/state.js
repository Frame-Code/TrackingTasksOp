// Manejo del estado de sesión activa (persistido en localStorage)
// y listas en memoria para proyectos y work packages

const SESSION_KEY = 'trackingActiveSession';

export const store = {
    projects: [],
    workPackages: []
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
