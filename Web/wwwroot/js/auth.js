// Módulo de la página de autenticación: login y registro

import { showToast } from './ui.js';

const API = '/api/v1/auth';

// ── Si ya tiene sesión, redirigir directamente ────────────────────────────────

if (sessionStorage.getItem('currentUser')) {
    window.location.replace('/');
}

// ── Referencias DOM ───────────────────────────────────────────────────────────

const loginForm         = document.getElementById('loginForm');
const registerForm      = document.getElementById('registerForm');
const forgotPasswordForm = document.getElementById('forgotPasswordForm');
const resetPasswordForm  = document.getElementById('resetPasswordForm');
const authTabs           = document.getElementById('authTabs');
const authAlert    = document.getElementById('authAlert');

// ── Aviso de sesión expirada (ej. falló el refresh de OAuth) ──────────────────
// apiFetch guarda acá el motivo antes de redirigir a este login — sin esto, el
// usuario solo veía que lo desloguearon, sin ninguna explicación de por qué.

const authNotice = sessionStorage.getItem('authNotice');
if (authNotice) {
    sessionStorage.removeItem('authNotice');
    showAlert(authNotice, 'warning');
}

// ── Cambio de tabs ────────────────────────────────────────────────────────────

function switchTab(tab) {
    const isLogin = tab === 'login';
    loginForm.classList.toggle('d-none', !isLogin);
    registerForm.classList.toggle('d-none', isLogin);
    forgotPasswordForm.classList.add('d-none');
    resetPasswordForm.classList.add('d-none');
    authTabs.classList.remove('d-none');
    document.getElementById('tab-login').classList.toggle('active', isLogin);
    document.getElementById('tab-register').classList.toggle('active', !isLogin);
    loginForm.classList.remove('was-validated');
    registerForm.classList.remove('was-validated');
    hideAlert();
}

authTabs.addEventListener('click', (e) => {
    const btn = e.target.closest('[data-tab]');
    if (btn) switchTab(btn.dataset.tab);
});

// ── Recuperar contraseña ──────────────────────────────────────────────────────

let forgotEmailValue = '';

function showForgotForm() {
    authTabs.classList.add('d-none');
    loginForm.classList.add('d-none');
    registerForm.classList.add('d-none');
    resetPasswordForm.classList.add('d-none');
    forgotPasswordForm.classList.remove('d-none');
    forgotPasswordForm.classList.remove('was-validated');
    hideAlert();
}

function showResetForm() {
    authTabs.classList.add('d-none');
    loginForm.classList.add('d-none');
    registerForm.classList.add('d-none');
    forgotPasswordForm.classList.add('d-none');
    resetPasswordForm.classList.remove('d-none');
    resetPasswordForm.classList.remove('was-validated');
    hideAlert();
}

document.getElementById('forgotPasswordLink').addEventListener('click', showForgotForm);
document.getElementById('backToLoginFromForgot').addEventListener('click', () => switchTab('login'));
document.getElementById('backToLoginFromReset').addEventListener('click', () => switchTab('login'));

forgotPasswordForm.addEventListener('submit', async (e) => {
    e.preventDefault();
    hideAlert();

    forgotEmailValue = document.getElementById('forgotEmail').value.trim();

    forgotPasswordForm.classList.add('was-validated');
    if (!forgotPasswordForm.checkValidity()) return;

    const btn          = document.getElementById('forgotPasswordBtn');
    const originalHtml = btn.innerHTML;
    setSubmitting(btn, true, originalHtml);

    try {
        const res = await fetch(`${API}/forgot-password`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ email: forgotEmailValue })
        });

        if (!res.ok) {
            const data = await res.json().catch(() => ({}));
            showAlert(data.message ?? data.detail ?? 'No se pudo enviar el código. Intenta nuevamente.');
            return;
        }

        showResetForm();
        showAlert('Si el correo existe, vas a recibir un código de recuperación.', 'success');
    } catch {
        showAlert('No se pudo conectar con el servidor. Intenta nuevamente.');
    } finally {
        setSubmitting(btn, false, originalHtml);
    }
});

resetPasswordForm.addEventListener('submit', async (e) => {
    e.preventDefault();
    hideAlert();

    const code        = document.getElementById('resetCode').value.trim();
    const newPassword = document.getElementById('resetNewPassword').value;

    resetPasswordForm.classList.add('was-validated');
    if (!resetPasswordForm.checkValidity()) return;

    const btn          = document.getElementById('resetPasswordBtn');
    const originalHtml = btn.innerHTML;
    setSubmitting(btn, true, originalHtml);

    try {
        const res = await fetch(`${API}/reset-password`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ email: forgotEmailValue, code, newPassword })
        });

        if (!res.ok) {
            const data = await res.json().catch(() => ({}));
            showAlert(data.message ?? data.detail ?? 'Código inválido o vencido.');
            return;
        }

        switchTab('login');
        showAlert('Contraseña actualizada. Iniciá sesión con tu nueva contraseña.', 'success');
    } catch {
        showAlert('No se pudo conectar con el servidor. Intenta nuevamente.');
    } finally {
        setSubmitting(btn, false, originalHtml);
    }
});

// ── OAuth con OpenProject ─────────────────────────────────────────────────────
// Sin sesión todavía (esta página es pública), por eso GET /opinstance también lo es:
// solo devuelve instancias con OAuth conectado (id + baseUrl + alias, nunca client_id/secret).

let oauthInstances = [];
const oauthPickerModalEl = document.getElementById('oauthPickerModal');
const oauthPickerModal = new bootstrap.Modal(oauthPickerModalEl);
const oauthPickerList = document.getElementById('oauthPickerList');
const oauthPickerEmpty = document.getElementById('oauthPickerEmpty');
const oauthPickerSearch = document.getElementById('oauthPickerSearch');

function renderOAuthPickerList(filterText = '') {
    const term = filterText.trim().toLowerCase();
    const filtered = term
        ? oauthInstances.filter(i =>
            (i.alias || '').toLowerCase().includes(term) || i.baseUrl.toLowerCase().includes(term))
        : oauthInstances;

    oauthPickerEmpty.classList.toggle('d-none', filtered.length > 0);
    oauthPickerList.innerHTML = filtered.map(i => `
        <button type="button" class="list-group-item list-group-item-action" data-instance-id="${i.id}">
            <div class="fw-medium">${escapeHtml(i.alias && i.alias !== '-' ? i.alias : i.baseUrl)}</div>
            ${i.alias && i.alias !== '-' ? `<div class="small text-muted">${escapeHtml(i.baseUrl)}</div>` : ''}
        </button>`).join('');
}

// auth.js no importa helpers.js (queda deliberadamente independiente del resto de la app),
// así que se repite este escape mínimo en vez de traer un módulo entero para una función.
function escapeHtml(str) {
    return str.replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
}

oauthPickerList.addEventListener('click', (e) => {
    const btn = e.target.closest('[data-instance-id]');
    if (btn) window.location.href = `/api/v1/auth/oauth/authorize?instanceId=${btn.dataset.instanceId}`;
});

oauthPickerSearch.addEventListener('input', () => renderOAuthPickerList(oauthPickerSearch.value));

function openOAuthPicker() {
    oauthPickerSearch.value = '';
    renderOAuthPickerList();
    oauthPickerModal.show();
    setTimeout(() => oauthPickerSearch.focus(), 300); // tras la animación del modal
}

function setOAuthButtonState(btn, badge) {
    if (!oauthInstances.length) {
        badge.textContent = 'No disponible';
        return;
    }

    btn.disabled = false;
    badge.remove();
    btn.addEventListener('click', openOAuthPicker);
}

(async function initOAuthButtons() {
    const loginBtn = document.getElementById('oauthLoginBtn');
    const registerBtn = document.getElementById('oauthRegisterBtn');
    const loginBadge = document.getElementById('oauthLoginBadge');
    const registerBadge = document.getElementById('oauthRegisterBadge');

    try {
        const res = await fetch('/api/v1/opinstance');
        oauthInstances = res.ok ? await res.json() : [];
        setOAuthButtonState(loginBtn, loginBadge);
        setOAuthButtonState(registerBtn, registerBadge);
    } catch {
        loginBadge.textContent = 'No disponible';
        registerBadge.textContent = 'No disponible';
    }
})();

// ── Mostrar / ocultar contraseña ──────────────────────────────────────────────

document.querySelectorAll('.toggle-password').forEach(btn => {
    btn.addEventListener('click', () => {
        const input  = document.getElementById(btn.dataset.target);
        const isText = input.type === 'text';
        input.type   = isText ? 'password' : 'text';
        btn.querySelector('i').className = `bi bi-eye${isText ? '' : '-slash'}`;
    });
});

// ── Helpers de alerta ─────────────────────────────────────────────────────────

function showAlert(msg, type = 'danger') {
    const icon = type === 'danger'
        ? 'bi-exclamation-triangle-fill'
        : 'bi-info-circle-fill';
    authAlert.className = `alert alert-${type} d-flex align-items-center gap-2 mb-3 py-2`;
    authAlert.innerHTML = `<i class="bi ${icon} flex-shrink-0"></i><span>${msg}</span>`;
}

function hideAlert() {
    authAlert.className = 'alert d-none';
    authAlert.innerHTML = '';
}

// ── Helper de botón en proceso ────────────────────────────────────────────────

function setSubmitting(btn, submitting, original) {
    btn.disabled  = submitting;
    btn.innerHTML = submitting
        ? '<span class="spinner-border spinner-border-sm me-2" role="status"></span>Procesando...'
        : original;
}

// ── Login ─────────────────────────────────────────────────────────────────────

loginForm.addEventListener('submit', async (e) => {
    e.preventDefault();
    hideAlert();

    const email    = document.getElementById('loginEmail').value.trim();
    const password = document.getElementById('loginPassword').value;

    loginForm.classList.add('was-validated');
    if (!loginForm.checkValidity()) return;

    const btn          = document.getElementById('loginBtn');
    const originalHtml = btn.innerHTML;
    setSubmitting(btn, true, originalHtml);

    try {
        const res = await fetch(`${API}/local-login`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ email, password })
        });

        const data = await res.json().catch(() => ({}));

        if (!res.ok) {
            showAlert(data.message ?? data.detail ?? 'Credenciales inválidas. Verifica tu correo y contraseña.');
            return;
        }

        sessionStorage.setItem('currentUser', JSON.stringify(data));
        window.location.replace('/');
    } catch {
        showAlert('No se pudo conectar con el servidor. Intenta nuevamente.');
    } finally {
        setSubmitting(btn, false, originalHtml);
    }
});

// ── Registro ──────────────────────────────────────────────────────────────────

registerForm.addEventListener('submit', async (e) => {
    e.preventDefault();
    hideAlert();

    const email               = document.getElementById('regEmail').value.trim();
    const password            = document.getElementById('regPassword').value;
    const confirm             = document.getElementById('regPasswordConfirm').value;
    const apiKey              = document.getElementById('regApiKey').value.trim();
    const instanceUrl         = document.getElementById('regInstanceUrl').value.trim();
    const validateSemanticUrl = document.getElementById('regValidateUrl').checked;

    // Validación: contraseñas coinciden
    const confirmInput = document.getElementById('regPasswordConfirm');
    confirmInput.setCustomValidity(password !== confirm ? 'no-match' : '');

    registerForm.classList.add('was-validated');
    if (!registerForm.checkValidity()) return;

    const btn          = document.getElementById('registerBtn');
    const originalHtml = btn.innerHTML;
    setSubmitting(btn, true, originalHtml);

    try {
        const res = await fetch(`${API}/local-register`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ email, password, apiKey, openProjectInstanceUrl: instanceUrl, validateSemanticOpenProjectUrl: validateSemanticUrl })
        });

        const data = await res.json().catch(() => ({}));

        if (!res.ok) {
            showAlert(data.message ?? data.detail ?? 'Error al crear la cuenta. Verifica los datos ingresados.');
            return;
        }

        sessionStorage.setItem('currentUser', JSON.stringify(data));
        window.location.replace('/');
    } catch {
        showAlert('No se pudo conectar con el servidor. Intenta nuevamente.');
    } finally {
        setSubmitting(btn, false, originalHtml);
    }
});
