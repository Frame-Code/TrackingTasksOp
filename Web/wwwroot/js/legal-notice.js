// Aviso de cookies. Es informativo, no un banner de consentimiento: la unica cookie que
// pone la app es la de sesion (estrictamente necesaria), y esas se informan, no se consienten.
// Si alguna vez se agrega analitica o seguimiento, esto tiene que pasar a pedir consentimiento
// previo y bloquear la cookie hasta tenerlo.
(function () {
    var KEY = 'cookieNoticeAck';

    try {
        if (localStorage.getItem(KEY)) return;
    } catch (e) {
        return; // localStorage bloqueado: no insistimos en cada carga
    }

    var bar = document.createElement('div');
    bar.className = 'position-fixed bottom-0 start-0 end-0 p-3';
    bar.style.zIndex = '1080';
    bar.innerHTML =
        '<div class="container">' +
          '<div class="alert alert-dark border shadow-lg d-flex flex-wrap align-items-center gap-3 mb-0 py-2 px-3">' +
            '<i class="bi bi-cookie fs-5 text-primary flex-shrink-0"></i>' +
            '<span class="small flex-grow-1 mb-0">' +
              'Usamos una sola cookie, necesaria para mantener tu sesion iniciada. ' +
              'No usamos cookies de analitica ni de publicidad. ' +
              '<a href="/legal.html#cookies" class="alert-link">Mas informacion</a>.' +
            '</span>' +
            '<button type="button" class="btn btn-sm btn-primary flex-shrink-0">Entendido</button>' +
          '</div>' +
        '</div>';

    bar.querySelector('button').addEventListener('click', function () {
        try { localStorage.setItem(KEY, '1'); } catch (e) { /* sesion privada: se volvera a mostrar */ }
        bar.remove();
    });

    document.body.appendChild(bar);
})();
