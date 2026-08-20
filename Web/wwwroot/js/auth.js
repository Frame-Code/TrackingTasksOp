// Módulo de la página de autenticación: login y registro

import { showToast } from './ui.js';

const API = '/api/v1/auth';

// ── Si ya tiene sesión, redirigir directamente ────────────────────────────────

if (sessionStorage.getItem('currentUser')) {
    window.location.replace('/');
}

// ── Referencias DOM ───────────────────────────────────────────────────────────

const loginForm    = document.getElementById('loginForm');
const registerForm = document.getElementById('registerForm');
const authAlert    = document.getElementById('authAlert');

// ── Cambio de tabs ────────────────────────────────────────────────────────────

function switchTab(tab) {
    const isLogin = tab === 'login';
    loginForm.classList.toggle('d-none', !isLogin);
    registerForm.classList.toggle('d-none', isLogin);
    document.getElementById('tab-login').classList.toggle('active', isLogin);
    document.getElementById('tab-register').classList.toggle('active', !isLogin);
    loginForm.classList.remove('was-validated');
    registerForm.classList.remove('was-validated');
    hideAlert();
}

document.getElementById('authTabs').addEventListener('click', (e) => {
    const btn = e.target.closest('[data-tab]');
    if (btn) switchTab(btn.dataset.tab);
});

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
