// Página puente del callback OAuth: la cookie de sesión ya la emitió el backend
// (SignInAsync corrió en el redirect que trajo hasta acá), pero sessionStorage.currentUser
// todavía no existe porque esta navegación la hizo el navegador, no un fetch de esta SPA.
// GET /auth/me usa esa cookie para traer los mismos datos que local-login/register guardan.

try {
    const res = await fetch('/api/v1/auth/me', { credentials: 'include' });

    if (!res.ok) throw new Error(`Error ${res.status}`);

    const data = await res.json();
    sessionStorage.setItem('currentUser', JSON.stringify(data));
    window.location.replace('/');
} catch {
    document.getElementById('callbackLoading').classList.add('d-none');
    document.getElementById('callbackError').classList.remove('d-none');
}
