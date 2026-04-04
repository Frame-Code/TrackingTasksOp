// Utilidades de interfaz: toasts, spinner de carga y mensajes de error

const TOAST_ICONS = {
    success: 'bi-check-circle-fill',
    danger:  'bi-exclamation-triangle-fill',
    warning: 'bi-exclamation-circle-fill',
    info:    'bi-info-circle-fill'
};

export function showToast(html, type = 'info') {
    const id = `toast_${Date.now()}`;
    const icon = TOAST_ICONS[type] ?? TOAST_ICONS.info;
    const markup = `
        <div id="${id}" class="toast align-items-center text-bg-${type} border-0" role="alert">
            <div class="d-flex">
                <div class="toast-body d-flex align-items-start gap-2">
                    <i class="bi ${icon} mt-1 flex-shrink-0"></i>
                    <span>${html}</span>
                </div>
                <button type="button" class="btn-close btn-close-white me-2 m-auto"
                        data-bs-dismiss="toast"></button>
            </div>
        </div>`;

    const container = document.getElementById('toastContainer');
    container.insertAdjacentHTML('beforeend', markup);
    const el = document.getElementById(id);
    new bootstrap.Toast(el, { delay: 4500 }).show();
    el.addEventListener('hidden.bs.toast', () => el.remove());
}

export function setLoading(show) {
    document.getElementById('loadingSpinner').classList.toggle('d-none', !show);
    document.getElementById('loadBtn').disabled = show;
}

export function showError(msg) {
    document.getElementById('errorMsg').textContent = msg;
    document.getElementById('errorState').classList.remove('d-none');
}

export function hideError() {
    document.getElementById('errorState').classList.add('d-none');
}
