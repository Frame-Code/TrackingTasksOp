// Capa de acceso a la API. Solo realiza llamadas HTTP y retorna datos,
// sin efectos secundarios en el DOM.

import { extractId } from './helpers.js';

const API = '/api/v1';

async function apiFetch(url, options = {}) {
    const res = await fetch(url, {
        credentials: 'include',
        headers: { 'Content-Type': 'application/json', ...options.headers },
        ...options
    });

    // Sesión expirada o no autenticado → redirigir al login
    if (res.status === 401) {
        sessionStorage.removeItem('currentUser');
        window.location.replace('/auth.html');
        return;
    }

    if (!res.ok) {
        let msg = `Error ${res.status}`;
        try {
            const body = await res.json();
            msg = body.title || body.message || body.detail || msg;
        } catch (_) { /* ignorar errores de parseo */ }
        throw new Error(msg);
    }

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

export async function fetchWorkPackages(projectId) {
    const qs = `offset=0&pageSize=50${projectId ? `&projectId=${projectId}` : ''}`;
    return apiFetch(`${API}/workpackage?${qs}`);
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
        comment: null
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
