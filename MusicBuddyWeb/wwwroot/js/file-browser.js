var fileBrowser = (function () {
    var currentPath = '';
    var fileType = 'sid';
    var selectedFiles = [];
    var onConfirm = null;
    var apiBase = '/api/filebrowser';

    function escapeHtml(s) {
        var d = document.createElement('div');
        d.appendChild(document.createTextNode(s));
        return d.innerHTML;
    }

    function formatSize(bytes) {
        if (bytes < 1024) return bytes + ' B';
        if (bytes < 1048576) return (bytes / 1024).toFixed(1) + ' KB';
        return (bytes / 1048576).toFixed(1) + ' MB';
    }

    function renderBreadcrumb() {
        var el = document.getElementById('fb-breadcrumb');
        if (!el) return;
        el.innerHTML = '';

        var home = document.createElement('li');
        home.className = 'breadcrumb-item';
        var homeLink = document.createElement('a');
        homeLink.href = '#';
        homeLink.textContent = 'Root';
        homeLink.addEventListener('click', function (e) { e.preventDefault(); navigateTo(''); });
        home.appendChild(homeLink);
        el.appendChild(home);

        if (!currentPath) return;

        var parts = currentPath.split('/');
        var builtPath = '';
        parts.forEach(function (part, i) {
            builtPath += (i > 0 ? '/' : '') + part;
            var li = document.createElement('li');
            li.className = 'breadcrumb-item' + (i === parts.length - 1 ? ' active' : '');
            if (i === parts.length - 1) {
                li.textContent = part;
            } else {
                var link = document.createElement('a');
                link.href = '#';
                link.textContent = part;
                var p = builtPath;
                link.addEventListener('click', function (e) { e.preventDefault(); navigateTo(p); });
                li.appendChild(link);
            }
            el.appendChild(li);
        });
    }

    function renderListing(listing) {
        var container = document.getElementById('fb-listing');
        if (!container) return;
        container.innerHTML = '';

        if (listing.directories && listing.directories.length > 0) {
            var dirHeader = document.createElement('div');
            dirHeader.className = 'fb-section-header';
            dirHeader.textContent = 'Folders';
            container.appendChild(dirHeader);

            listing.directories.forEach(function (dir) {
                var item = document.createElement('button');
                item.type = 'button';
                item.className = 'list-group-item list-group-item-action d-flex align-items-center';
                item.innerHTML = '<i class="bi bi-folder-fill text-warning me-2"></i>' +
                    '<span class="text-truncate">' + escapeHtml(dir.name) + '</span>';
                item.addEventListener('click', function () { navigateTo(dir.path); });
                container.appendChild(item);
            });
        }

        if (listing.files && listing.files.length > 0) {
            var fileHeader = document.createElement('div');
            fileHeader.className = 'fb-section-header';
            fileHeader.textContent = 'Files (' + listing.files.length + ')';
            container.appendChild(fileHeader);

            listing.files.forEach(function (file) {
                var item = document.createElement('label');
                item.className = 'list-group-item list-group-item-action d-flex align-items-center';

                var checkbox = document.createElement('input');
                checkbox.type = 'checkbox';
                checkbox.className = 'form-check-input me-2';
                checkbox.value = file.path;
                checkbox.dataset.name = file.name;
                checkbox.dataset.size = file.size;

                var isChecked = selectedFiles.some(function (f) { return f.path === file.path; });
                checkbox.checked = isChecked;

                checkbox.addEventListener('change', function () {
                    if (this.checked) {
                        if (!selectedFiles.some(function (f) { return f.path === file.path; })) {
                            selectedFiles.push({ path: file.path, name: file.name, size: file.size });
                        }
                    } else {
                        selectedFiles = selectedFiles.filter(function (f) { return f.path !== file.path; });
                    }
                    updateSelectedCount();
                });

                item.appendChild(checkbox);

                var nameSpan = document.createElement('span');
                nameSpan.className = 'text-truncate me-2';
                nameSpan.textContent = file.name;
                item.appendChild(nameSpan);

                var sizeSmall = document.createElement('small');
                sizeSmall.className = 'text-nowrap text-muted ms-auto';
                sizeSmall.textContent = formatSize(file.size);
                item.appendChild(sizeSmall);

                container.appendChild(item);
            });
        }

        if ((!listing.directories || listing.directories.length === 0) &&
            (!listing.files || listing.files.length === 0)) {
            var empty = document.createElement('div');
            empty.className = 'text-center text-muted py-4';
            empty.textContent = 'Empty directory';
            container.appendChild(empty);
        }

        updateSelectedCount();
    }

    function updateSelectedCount() {
        var el = document.getElementById('fb-selected-count');
        if (el) {
            el.textContent = selectedFiles.length + ' file' + (selectedFiles.length !== 1 ? 's' : '') + ' selected';
        }
        var confirmBtn = document.getElementById('fb-confirm-btn');
        if (confirmBtn) {
            confirmBtn.disabled = selectedFiles.length === 0;
        }
    }

    function navigateTo(path) {
        currentPath = path;
        renderBreadcrumb();

        var container = document.getElementById('fb-listing');
        if (container) {
            container.innerHTML = '<div class="text-center py-4"><div class="spinner-border text-primary" role="status"><span class="visually-hidden">Loading...</span></div></div>';
        }

        var url = apiBase + '/browse?type=' + encodeURIComponent(fileType) + '&path=' + encodeURIComponent(path);
        fetch(url)
            .then(function (res) {
                if (!res.ok) throw new Error('Failed to load directory');
                return res.json();
            })
            .then(function (listing) {
                renderListing(listing);
            })
            .catch(function (err) {
                if (container) {
                    container.innerHTML = '<div class="text-center text-danger py-4">Error loading directory</div>';
                }
            });
    }

    return {
        open: function (options) {
            fileType = options.fileType || 'sid';
            selectedFiles = [];
            onConfirm = options.onConfirm || null;
            currentPath = options.initialPath || '';

            var modal = document.getElementById('fileBrowserModal');
            if (modal) {
                var bsModal = new bootstrap.Modal(modal);
                bsModal.show();

                modal.addEventListener('shown.bs.modal', function () {
                    navigateTo(currentPath);
                }, { once: true });

                modal.addEventListener('hidden.bs.modal', function () {
                    selectedFiles = [];
                    onConfirm = null;
                }, { once: true });
            }
        },

        getSelectedFiles: function () {
            return selectedFiles.slice();
        },

        confirmSelection: function () {
            if (onConfirm && selectedFiles.length > 0) {
                onConfirm(selectedFiles.slice());
            }
            var modal = document.getElementById('fileBrowserModal');
            if (modal) {
                var instance = bootstrap.Modal.getInstance(modal);
                if (instance) instance.hide();
            }
        },

        selectAll: function () {
            var checkboxes = document.querySelectorAll('#fb-listing input[type="checkbox"]');
            checkboxes.forEach(function (cb) {
                if (!cb.checked) {
                    cb.checked = true;
                    if (!selectedFiles.some(function (f) { return f.path === cb.value; })) {
                        selectedFiles.push({ path: cb.value, name: cb.dataset.name, size: parseInt(cb.dataset.size) });
                    }
                }
            });
            updateSelectedCount();
        },

        deselectAll: function () {
            selectedFiles = [];
            var checkboxes = document.querySelectorAll('#fb-listing input[type="checkbox"]');
            checkboxes.forEach(function (cb) { cb.checked = false; });
            updateSelectedCount();
        }
    };
})();
