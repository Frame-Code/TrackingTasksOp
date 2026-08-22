// Avatar del usuario, compartido entre el dashboard (sidebar) y la vista Mi cuenta.
// Vive aparte porque account.js pasó a ser el script de /account.html: si el sidebar
// importara de ahí, el dashboard arrastraría toda la lógica de una página que no usa.

import { store } from './state.js';

export function initials(email) {
    if (!email) return '?';
    return email.split('@')[0].slice(0, 2).toUpperCase();
}

/** El ?v= evita que el navegador siga mostrando la foto anterior desde su caché. */
export function avatarUrl() {
    return `/api/v1/account/avatar?v=${Date.now()}`;
}

/** Pinta el avatar del sidebar: la foto si existe, las iniciales si no. */
export function renderSidebarAvatar() {
    const slot = document.getElementById('sidebarAvatarInitials');
    if (!slot) return;

    if (store.userSettings?.hasAvatar) {
        slot.innerHTML = '';
        const img = new Image();
        img.src = avatarUrl();
        img.width = 32;
        img.height = 32;
        img.alt = 'Tu foto de perfil';
        img.className = 'rounded-circle';
        slot.appendChild(img);
    } else {
        slot.textContent = initials(store.userSettings?.email);
    }
}
