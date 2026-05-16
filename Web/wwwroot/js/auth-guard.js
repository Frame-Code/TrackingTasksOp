// Utilidad de protección de rutas: redirige a auth.html si no hay sesión activa

export function requireAuth() {
    if (!sessionStorage.getItem('currentUser')) {
        window.location.replace('/auth.html');
    }
}

export function getCurrentUser() {
    const raw = sessionStorage.getItem('currentUser');
    return raw ? JSON.parse(raw) : null;
}

export function clearCurrentUser() {
    sessionStorage.removeItem('currentUser');
}
