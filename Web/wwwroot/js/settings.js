// Script de /settings.html. Toda la configuración vive acá: antes estaba repartida entre el
// acordeón del sidebar del dashboard y un par de modales, que le comían ancho a las tareas.

import {
    fetchUserSettings, fetchStatuses, setupTwoFactor, enableTwoFactor, changePassword,
    updateAvatar, deleteAvatar, regenerateRecoveryCodes, resetAuthenticator,
    updateAiApiKey, updateApiKey, connectOAuthInstance
} from './api.js';
import { requireAuth } from './auth-guard.js';
import { store } from './state.js';
import { initials } from './avatar.js';
import { renderSettingsFields, bindSettingsFields } from './settings-fields.js';
import { refreshPendingBadge } from './pending-sessions.js';

requireAuth();

/** Lado del cuadrado final. Más que esto no se nota en un avatar de 32-80px. */
const AVATAR_SIZE = 256;
const AVATAR_QUALITY = 0.85;

const el = id => document.getElementById(id);
const show = (id, visible) => el(id).classList.toggle('hidden', !visible);

// ── Navegación entre secciones ────────────────────────────────────────────────────

const DEFAULT_SECTION = 'cuenta';

/**
 * Ruteo por hash en vez de estado interno: la URL queda compartible, el botón "atrás"
 * funciona, y entrar directo a /settings.html#seguridad lleva a donde corresponde.
 */
function showSection(name) {
    const sections = [...document.querySelectorAll('.panel')].map(p => p.dataset.section);
    const target = sections.includes(name) ? name : DEFAULT_SECTION;

    document.querySelectorAll('.panel').forEach(panel => {
        panel.classList.toggle('active', panel.dataset.section === target);
    });

    document.querySelectorAll('.side-nav a').forEach(link => {
        if (link.dataset.section === target) link.setAttribute('aria-current', 'page');
        else link.removeAttribute('aria-current');
    });

    clearMessages();
}

function initNav() {
    window.addEventListener('hashchange', () => showSection(location.hash.slice(1)));
    showSection(location.hash.slice(1));
}

// ── Mensajes ──────────────────────────────────────────────────────────────────────

function showError(message) {
    el('settingsError').textContent = message;
    show('settingsError', true);
    show('settingsOk', false);
}

function showOk(message) {
    el('settingsOk').textContent = message;
    show('settingsOk', true);
    show('settingsError', false);
}

function clearMessages() {
    show('settingsError', false);
    show('settingsOk', false);
}

/** Evita el doble envío y deja ver que algo está pasando. */
async function withBusy(button, action) {
    button.disabled = true;
    try {
        await action();
    } finally {
        button.disabled = false;
    }
}

// ── Avatar ────────────────────────────────────────────────────────────────────────

function avatarUrl() {
    return `/api/v1/account/avatar?v=${Date.now()}`;
}

/**
 * Recorta al centro y achica a 256px, exportando JPEG. Se hace acá y no en el servidor para
 * no meter una librería de imágenes en el backend: sube ~15KB en vez del original.
 * El servidor igual valida tamaño y magic bytes — esto es comodidad, no seguridad.
 */
function resizeToJpegBase64(file) {
    return new Promise((resolve, reject) => {
        const reader = new FileReader();
        reader.onerror = () => reject(new Error('No se pudo leer el archivo.'));
        reader.onload = () => {
            const img = new Image();
            img.onerror = () => reject(new Error('El archivo no es una imagen válida.'));
            img.onload = () => {
                const canvas = document.createElement('canvas');
                canvas.width = AVATAR_SIZE;
                canvas.height = AVATAR_SIZE;

                // Recorte cuadrado centrado: si no, una foto apaisada sale deformada.
                const side = Math.min(img.width, img.height);
                const sx = (img.width - side) / 2;
                const sy = (img.height - side) / 2;

                const ctx = canvas.getContext('2d');
                // Fondo blanco: el JPEG no tiene transparencia y un PNG transparente
                // saldría con fondo negro.
                ctx.fillStyle = '#ffffff';
                ctx.fillRect(0, 0, AVATAR_SIZE, AVATAR_SIZE);
                ctx.drawImage(img, sx, sy, side, side, 0, 0, AVATAR_SIZE, AVATAR_SIZE);

                const dataUri = canvas.toDataURL('image/jpeg', AVATAR_QUALITY);
                resolve(dataUri.slice(dataUri.indexOf(',') + 1));
            };
            img.src = reader.result;
        };
        reader.readAsDataURL(file);
    });
}

function renderProfile() {
    const hasAvatar = store.userSettings?.hasAvatar ?? false;

    show('accountAvatarPreview', hasAvatar);
    show('accountAvatarInitials', !hasAvatar);
    show('accountAvatarRemoveBtn', hasAvatar);

    if (hasAvatar) el('accountAvatarPreview').src = avatarUrl();
    else el('accountAvatarInitials').textContent = initials(store.userSettings?.email);

    el('accountEmail').textContent = store.userSettings?.email || '—';
}

async function onAvatarSelected(event) {
    const file = event.target.files?.[0];
    if (!file) return;

    clearMessages();
    try {
        const base64 = await resizeToJpegBase64(file);
        await updateAvatar(base64);
        store.userSettings.hasAvatar = true;
        renderProfile();
        showOk('Foto actualizada.');
    } catch (err) {
        showError(err.message);
    } finally {
        event.target.value = '';
    }
}

function onAvatarRemove() {
    return withBusy(el('accountAvatarRemoveBtn'), async () => {
        clearMessages();
        try {
            await deleteAvatar();
            store.userSettings.hasAvatar = false;
            renderProfile();
            showOk('Foto quitada.');
        } catch (err) {
            showError(err.message);
        }
    });
}

// ── Segundo factor ────────────────────────────────────────────────────────────────

/**
 * Una sola función decide qué se ve, según si el 2FA está activo. Tener el estado en un
 * único lugar evita que las secciones queden en combinaciones imposibles.
 */
async function renderSecurity() {
    const enabled = store.userSettings?.twoFactorEnabled ?? false;

    const badge = el('account2faBadge');
    badge.textContent = enabled ? 'Activada' : 'No activada';
    badge.className = `badge ${enabled ? 'badge-on' : 'badge-off'}`;

    show('account2faManage', enabled);
    show('account2faSetup', !enabled);
    show('accountResetAuthForm', false);
    show('accountRecoveryCodes', false);

    show('accountPasswordForm', enabled);
    show('accountPasswordLocked', !enabled);

    if (enabled) return;

    try {
        const setup = await setupTwoFactor();
        el('account2faQr').src = setup.qrCodeDataUri;
        el('account2faManualKey').textContent = setup.manualKey;
    } catch (err) {
        showError(err.message);
    }
}

function showRecoveryCodes(codes) {
    el('accountRecoveryCodesList').textContent = (codes ?? []).join('\n');
    show('accountRecoveryCodes', true);
    el('accountRecoveryCodes').scrollIntoView({ behavior: 'smooth', block: 'nearest' });
}

function onEnable2fa() {
    return withBusy(el('account2faEnableBtn'), async () => {
        clearMessages();
        const code = el('account2faCode').value.trim();
        if (!code) return showError('Escribí el código que muestra la app.');

        try {
            const result = await enableTwoFactor(code);
            store.userSettings.twoFactorEnabled = true;
            el('account2faCode').value = '';

            await renderSecurity();
            showRecoveryCodes(result.recoveryCodes);
            showOk('Verificación en dos pasos activada.');
        } catch (err) {
            showError(err.message);
        }
    });
}

function onRegenerateCodes() {
    return withBusy(el('accountRegenerateCodesBtn'), async () => {
        clearMessages();
        const input = el('account2faManageCode');
        const code = input.value.trim();
        if (!code) return showError('Escribí un código para confirmar que sos vos.');

        try {
            const result = await regenerateRecoveryCodes(code);
            input.value = '';
            showRecoveryCodes(result.recoveryCodes);
            showOk('Códigos nuevos generados. Los anteriores ya no sirven.');
        } catch (err) {
            showError(err.message);
        }
    });
}

/** Abre el paso de confirmación. La contraseña se pide en un campo real, no en un prompt(). */
function onResetAuthStart() {
    clearMessages();
    if (!el('account2faManageCode').value.trim())
        return showError('Escribí primero un código de la app o uno de recuperación.');

    show('accountResetAuthForm', true);
    el('accountResetPassword').focus();
}

function onResetAuthCancel() {
    show('accountResetAuthForm', false);
    el('accountResetPassword').value = '';
}

function onResetAuthConfirm() {
    return withBusy(el('accountResetConfirmBtn'), async () => {
        clearMessages();
        const code = el('account2faManageCode').value.trim();
        const password = el('accountResetPassword').value;
        if (!password) return showError('Escribí tu contraseña actual.');

        try {
            await resetAuthenticator(password, code);
            store.userSettings.twoFactorEnabled = false;
            el('account2faManageCode').value = '';
            el('accountResetPassword').value = '';

            await renderSecurity();
            showOk('App desvinculada. Escaneá el código nuevo con tu otro teléfono.');
        } catch (err) {
            showError(err.message);
        }
    });
}

// ── Contraseña ────────────────────────────────────────────────────────────────────

function onChangePassword() {
    return withBusy(el('accountPasswordBtn'), async () => {
        clearMessages();

        const current = el('accountCurrentPassword').value;
        const next = el('accountNewPassword').value;
        const confirm = el('accountConfirmPassword').value;
        const code = el('accountPasswordCode').value.trim();

        // Se valida acá solo para no gastar un código TOTP en un error de tipeo:
        // el servidor vuelve a validar todo lo demás.
        if (next !== confirm) return showError('Las contraseñas nuevas no coinciden.');
        if (!code) return showError('Escribí el código de verificación.');

        try {
            await changePassword(current, next, code);
            ['accountCurrentPassword', 'accountNewPassword', 'accountConfirmPassword', 'accountPasswordCode']
                .forEach(id => { el(id).value = ''; });
            showOk('Contraseña actualizada.');
        } catch (err) {
            showError(err.message);
        }
    });
}

// ── OpenProject ───────────────────────────────────────────────────────────────────

function onSaveOpApiKey() {
    return withBusy(el('saveOpApiKeyBtn'), async () => {
        clearMessages();
        const apiKey = el('apiKeyInput').value.trim();
        if (!apiKey) return showError('Pegá la API key de OpenProject.');

        try {
            await updateApiKey(apiKey);
            el('apiKeyInput').value = '';
            showOk('API key de OpenProject actualizada.');
        } catch (err) {
            showError(`No se pudo actualizar: ${err.message}`);
        }
    });
}

/** Solo se ve para admins de OpenProject (ver renderInstanceUrl en settings-fields.js). */
function onConnectOAuth() {
    return withBusy(el('oauthConnectBtn'), async () => {
        clearMessages();
        const alias = el('oauthConnectAlias').value.trim();
        const clientId = el('oauthConnectClientId').value.trim();
        const clientSecret = el('oauthConnectClientSecret').value.trim();

        if (!alias || !clientId || !clientSecret)
            return showError('Completá alias, client ID y client secret.');

        try {
            await connectOAuthInstance(alias, clientId, clientSecret);
            el('oauthConnectAlias').value = '';
            el('oauthConnectClientId').value = '';
            el('oauthConnectClientSecret').value = '';
            showOk('OpenProject conectado. Ya se puede iniciar sesión con OAuth.');
        } catch (err) {
            showError(err.message === 'Error 403'
                ? 'No tenés permisos de administrador en OpenProject para hacer esto.'
                : `No se pudo conectar: ${err.message}`);
        }
    });
}

// ── Asistente de IA ───────────────────────────────────────────────────────────────

/**
 * El backend nunca devuelve la key guardada, ni cifrada: solo un booleano. Por eso el campo
 * queda siempre vacío y el estado se comunica con el badge y el texto, no rellenando el input.
 */
function renderAiKey() {
    const hasCustomKey = store.userSettings?.hasCustomAiApiKey ?? false;

    const badge = el('aiKeyBadge');
    badge.textContent = hasCustomKey ? 'Key propia' : 'Key compartida';
    badge.className = `badge ${hasCustomKey ? 'badge-on' : 'badge-off'}`;

    el('aiKeyStatus').textContent = hasCustomKey
        ? 'Estás usando tu propia API key: el bot no tiene límite diario de mensajes.'
        : 'Estás usando la key compartida del servidor, con un límite diario de mensajes.';

    show('clearAiApiKeyBtn', hasCustomKey);
    el('saveAiApiKeyBtn').innerHTML = hasCustomKey
        ? '<i class="bi bi-check2"></i> Reemplazar key'
        : '<i class="bi bi-check2"></i> Guardar';

    el('aiApiKeyInput').value = '';
}

function onSaveAiKey() {
    return withBusy(el('saveAiApiKeyBtn'), async () => {
        clearMessages();
        const apiKey = el('aiApiKeyInput').value.trim();
        if (!apiKey) return showError('Pegá tu API key de Groq, o usá "Quitar" para volver a la compartida.');

        try {
            await updateAiApiKey(apiKey);
            store.userSettings.hasCustomAiApiKey = true;
            renderAiKey();
            showOk('API key guardada. El bot ya no tiene límite diario.');
        } catch (err) {
            showError(`No se pudo guardar: ${err.message}`);
        }
    });
}

function onClearAiKey() {
    return withBusy(el('clearAiApiKeyBtn'), async () => {
        clearMessages();
        try {
            await updateAiApiKey(null);
            store.userSettings.hasCustomAiApiKey = false;
            renderAiKey();
            showOk('Volviste a la key compartida, con límite diario.');
        } catch (err) {
            showError(`No se pudo quitar la key: ${err.message}`);
        }
    });
}

// ── Mostrar/ocultar contraseña ────────────────────────────────────────────────────

/**
 * Le agrega el ojito a todos los campos de contraseña de la página.
 *
 * Se genera por JS y no se escribe en el HTML para no repetir el mismo bloque en cada campo;
 * si mañana se suma otro, queda cubierto solo. Es mejora progresiva: sin JS los campos siguen
 * andando, nada más que sin el toggle.
 */
function initPasswordToggles() {
    document.querySelectorAll('input[type=password]').forEach(input => {
        const wrap = document.createElement('div');
        wrap.className = 'password-field';
        input.parentNode.insertBefore(wrap, input);
        wrap.appendChild(input);

        const btn = document.createElement('button');
        btn.type = 'button';
        btn.className = 'password-toggle';
        btn.tabIndex = -1; // fuera del recorrido con Tab: no debe interrumpir el tipeo
        btn.setAttribute('aria-label', 'Mostrar contraseña');
        btn.setAttribute('aria-pressed', 'false');
        btn.innerHTML = '<i class="bi bi-eye"></i>';

        btn.addEventListener('click', () => {
            const wasVisible = input.type === 'text';
            input.type = wasVisible ? 'password' : 'text';

            btn.setAttribute('aria-pressed', String(!wasVisible));
            btn.setAttribute('aria-label', wasVisible ? 'Mostrar contraseña' : 'Ocultar contraseña');
            btn.innerHTML = wasVisible ? '<i class="bi bi-eye"></i>' : '<i class="bi bi-eye-slash"></i>';

            input.focus(); // devuelve el foco al campo para no cortar el tipeo
        });

        wrap.appendChild(btn);
    });
}

// ── Init ──────────────────────────────────────────────────────────────────────────

initNav();
initPasswordToggles();

el('accountAvatarInput').addEventListener('change', onAvatarSelected);
el('accountAvatarRemoveBtn').addEventListener('click', onAvatarRemove);
el('account2faEnableBtn').addEventListener('click', onEnable2fa);
el('accountRegenerateCodesBtn').addEventListener('click', onRegenerateCodes);
el('accountResetAuthBtn').addEventListener('click', onResetAuthStart);
el('accountResetConfirmBtn').addEventListener('click', onResetAuthConfirm);
el('accountResetCancelBtn').addEventListener('click', onResetAuthCancel);
el('accountRecoveryDoneBtn').addEventListener('click', () => show('accountRecoveryCodes', false));
el('accountPasswordBtn').addEventListener('click', onChangePassword);
el('saveOpApiKeyBtn').addEventListener('click', onSaveOpApiKey);
el('oauthConnectBtn').addEventListener('click', onConnectOAuth);
el('saveAiApiKeyBtn').addEventListener('click', onSaveAiKey);
el('clearAiApiKeyBtn').addEventListener('click', onClearAiKey);
// El modal en sí solo existe en index.html (esta página no carga Bootstrap JS).
el('viewPendingSessionsBtn').addEventListener('click', () => { location.href = '/?openPending=1'; });

bindSettingsFields();
refreshPendingBadge();

(async () => {
    try {
        store.userSettings = await fetchUserSettings();
        renderProfile();
        renderAiKey();
        renderSettingsFields();
        await renderSecurity();
    } catch (err) {
        showError(`No se pudo cargar la configuración: ${err.message}`);
        return;
    }

    // Los estados de OpenProject alimentan los checks de "filtro por defecto". Van aparte
    // porque tardan más (salen de la API de OpenProject) y su demora no debe retrasar
    // el resto de la pantalla.
    try {
        store.statuses = await fetchStatuses();
        renderSettingsFields();
    } catch (err) {
        console.warn('No se pudieron cargar los estados de OpenProject:', err.message);
    }
})();
