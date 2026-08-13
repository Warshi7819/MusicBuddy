(function () {
    function topOpenModalZ() {
        var z = 1055;
        document.querySelectorAll('.modal.show').forEach(function (m) {
            var elZ = parseInt(m.style.zIndex) || parseInt(getComputedStyle(m).zIndex);
            if (elZ > z) z = elZ;
        });
        return z;
    }

    function musicbuddyConfirm(message, options) {
        options = options || {};
        var modalEl = document.getElementById('confirmDialogModal');
        if (!modalEl) return Promise.resolve(false);
        var titleEl = document.getElementById('confirmDialogTitle');
        var msgEl = document.getElementById('confirmDialogMessage');
        var okBtn = document.getElementById('confirmDialogOk');
        var cancelBtn = document.getElementById('confirmDialogCancel');
        if (titleEl) titleEl.textContent = options.title || 'Confirm';
        if (msgEl) msgEl.textContent = message;
        if (okBtn) {
            okBtn.textContent = options.confirmText || 'Confirm';
            okBtn.className = 'btn ' + (options.variant === 'primary' ? 'btn-primary' : 'btn-danger');
        }
        if (cancelBtn) cancelBtn.textContent = options.cancelText || 'Cancel';
        return new Promise(function (resolve) {
            function onOk() {
                cleanup();
                modalEl.removeEventListener('hidden.bs.modal', onHidden);
                bootstrap.Modal.getOrCreateInstance(modalEl).hide();
                resolve(true);
            }
            function onCancel() {
                cleanup();
                bootstrap.Modal.getOrCreateInstance(modalEl).hide();
            }
            function onHidden() {
                cleanup();
                resolve(false);
            }
            function cleanup() {
                if (okBtn) okBtn.removeEventListener('click', onOk);
                if (cancelBtn) cancelBtn.removeEventListener('click', onCancel);
                modalEl.removeEventListener('hidden.bs.modal', onHidden);
            }
            if (okBtn) okBtn.addEventListener('click', onOk);
            if (cancelBtn) cancelBtn.addEventListener('click', onCancel);
            modalEl.addEventListener('hidden.bs.modal', onHidden);
            var modalZ = topOpenModalZ() + 10;
            modalEl.style.zIndex = modalZ;
            modalEl.addEventListener('shown.bs.modal', function () {
                var backdrops = document.querySelectorAll('.modal-backdrop');
                var last = backdrops[backdrops.length - 1];
                if (last) last.style.zIndex = (modalZ - 1) + '';
            });
            modalEl.addEventListener('hidden.bs.modal', function () {
                modalEl.style.zIndex = '';
            });
            bootstrap.Modal.getOrCreateInstance(modalEl).show();
        });
    }

    window.musicbuddyConfirm = musicbuddyConfirm;

    document.addEventListener('submit', function (e) {
        var form = e.target;
        var message = form.getAttribute('data-confirm-message');
        if (!message) return;
        e.preventDefault();
        musicbuddyConfirm(message, {
            title: form.getAttribute('data-confirm-title') || 'Confirm',
            confirmText: form.getAttribute('data-confirm-text') || 'Delete',
            variant: form.getAttribute('data-confirm-variant') || 'danger'
        }).then(function (ok) {
            if (ok) form.submit();
        });
    });
})();