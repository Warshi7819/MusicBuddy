var playlistEditor = (function () {
    'use strict';

    var API_PLAYLISTS = '/api/playlists';
    var API_BROWSE = '/api/filebrowser/browse';
    var API_COLLECT = '/api/filebrowser/collect';

    var modal = null;
    var tracksEl = null;
    var libraryEl = null;
    var fileType = 'mp3';
    var playlists = [];
    var currentPlaylistId = null;
    var currentPlaylistName = '';
    var tracks = [];
    var browserPath = '';
    var selection = [];
    var dragSource = null;
    var dragPayload = null;
    var dropIndex = -1;
    var dropRowEl = null;
    var nameMode = null;
    var savedTimer = null;

    function $(id) { return document.getElementById(id); }

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

    function apiFetch(url, options) {
        return fetch(url, Object.assign({ headers: { 'Content-Type': 'application/json' } }, options || {}))
            .then(function (res) {
                if (!res.ok) {
                    return res.json().catch(function () { return {}; }).then(function (body) {
                        throw new Error(body.message || 'API error');
                    });
                }
                if (res.status === 204) return null;
                return res.json();
            });
    }

    function storeLastPlaylist() {
        localStorage.setItem('musicbuddy_last_playlist_' + fileType, currentPlaylistId || '');
    }

    function syncPlayer() {
        if (typeof playlistManager !== 'undefined' && playlistManager.selectPlaylist) {
            playlistManager.selectPlaylist(currentPlaylistId);
        }
    }

    function flashSaved() {
        var el = $('pe-saved-indicator');
        if (!el) return;
        el.classList.add('show');
        clearTimeout(savedTimer);
        savedTimer = setTimeout(function () { el.classList.remove('show'); }, 1500);
    }

    function toast(message, isError) {
        var container = $('pe-toast-container');
        if (!container) return;
        var t = document.createElement('div');
        t.className = 'pe-toast' + (isError ? ' pe-toast-error' : '');
        t.textContent = message;
        container.appendChild(t);
        setTimeout(function () {
            t.classList.add('pe-toast-out');
            setTimeout(function () { t.remove(); }, 300);
        }, 3500);
    }

    function focusNameMode(mode) {
        nameMode = mode;
        $('pe-name-label').textContent = mode === 'rename' ? 'Rename playlist:' : 'New playlist name:';
        $('pe-name-input').value = mode === 'rename' ? currentPlaylistName : '';
        $('pe-name-collapse').classList.add('pe-name-open');
        var input = $('pe-name-input');
        input.focus();
        requestAnimationFrame(function () { input.focus(); });
    }

    function resetNameMode() {
        nameMode = null;
        $('pe-name-collapse').classList.remove('pe-name-open');
        $('pe-name-label').textContent = 'New playlist name:';
        $('pe-name-input').value = '';
    }

    function saveName() {
        if (!nameMode) return;
        var name = $('pe-name-input').value.trim();
        if (!name) {
            toast('Enter a playlist name.', true);
            $('pe-name-input').focus();
            return;
        }
        if (nameMode === 'create') createPlaylist(name);
        else renamePlaylist(name);
    }

    function renderPlaylistSelect() {
        var select = $('pe-playlist-select');
        if (!select) return;
        select.innerHTML = '';
        if (playlists.length === 0) {
            var none = document.createElement('option');
            none.value = '';
            none.textContent = 'No playlists yet';
            select.appendChild(none);
        } else {
            playlists.forEach(function (p) {
                var opt = document.createElement('option');
                opt.value = p.id;
                opt.textContent = p.name;
                if (p.id === currentPlaylistId) opt.selected = true;
                select.appendChild(opt);
            });
        }
        var renameBtn = $('pe-rename-btn');
        var deleteBtn = $('pe-delete-btn');
        var clearBtn = $('pe-clear-btn');
        if (renameBtn) renameBtn.disabled = !currentPlaylistId;
        if (deleteBtn) deleteBtn.disabled = !currentPlaylistId;
        if (clearBtn) clearBtn.disabled = !currentPlaylistId;
    }

    function loadPlaylists() {
        return apiFetch(API_PLAYLISTS + '?type=' + encodeURIComponent(fileType)).then(function (list) {
            playlists = list || [];
            renderPlaylistSelect();

            var requested = null;
            if (typeof playlistManager !== 'undefined' && playlistManager.getCurrentPlaylistId) {
                requested = playlistManager.getCurrentPlaylistId();
            }
            if (!requested) {
                var saved = localStorage.getItem('musicbuddy_last_playlist_' + fileType);
                if (saved) requested = parseInt(saved);
            }
            if (requested && !playlists.some(function (p) { return p.id === requested; })) {
                requested = null;
            }
            switchPlaylist(requested, true);
        }).catch(function (err) {
            toast(err.message, true);
        });
    }

    function switchPlaylist(id, silent) {
        currentPlaylistId = id || null;
        currentPlaylistName = '';
        if (currentPlaylistId) {
            var pl = playlists.find(function (p) { return p.id === currentPlaylistId; });
            if (pl) currentPlaylistName = pl.name;
        }
        storeLastPlaylist();
        var select = $('pe-playlist-select');
        if (select) select.value = currentPlaylistId || '';
        renderPlaylistSelect();
        if (!silent) syncPlayer();
        return loadTracks();
    }

    function loadTracks() {
        if (!currentPlaylistId) {
            tracks = [];
            renderTracks();
            return Promise.resolve();
        }
        return apiFetch(API_PLAYLISTS + '/' + currentPlaylistId).then(function (data) {
            tracks = (data && data.tracks) || [];
            renderTracks();
        }).catch(function (err) {
            toast(err.message, true);
        });
    }

    function renderTracks() {
        if (!tracksEl) return;
        tracksEl.innerHTML = '';
        var countEl = $('pe-track-count');
        if (countEl) countEl.textContent = tracks.length + ' track' + (tracks.length === 1 ? '' : 's');
        var clearBtn = $('pe-clear-btn');
        if (clearBtn) clearBtn.disabled = !currentPlaylistId || tracks.length === 0;
        var emptyEl = $('pe-tracks-empty');
        if (emptyEl) emptyEl.style.display = tracks.length === 0 ? '' : 'none';

        tracks.forEach(function (t, i) {
            var item = document.createElement('div');
            item.className = 'list-group-item pe-track-row d-flex align-items-center';
            item.draggable = true;
            item.dataset.trackId = t.id;
            item.dataset.path = t.filePath;
            item.innerHTML =
                '<i class="bi bi-grip-vertical text-muted me-2"></i>' +
                '<span class="text-muted me-2">' + (i + 1) + '.</span>' +
                '<span class="text-truncate flex-grow-1">' + escapeHtml(t.fileName) + '</span>' +
                '<small class="text-nowrap text-muted ms-2">' + formatSize(t.fileSize) + '</small>' +
                '<button type="button" class="btn btn-sm btn-outline-danger ms-2 pe-remove-btn" title="Remove"><i class="bi bi-x-lg"></i></button>';
            tracksEl.appendChild(item);
        });
    }

    function createPlaylist(name) {
        apiFetch(API_PLAYLISTS, {
            method: 'POST',
            body: JSON.stringify({ name: name, fileType: fileType })
        }).then(function (playlist) {
            resetNameMode();
            playlists.push(playlist);
            playlists.sort(function (a, b) { return (b.updatedAt || '').localeCompare(a.updatedAt || ''); });
            return switchPlaylist(playlist.id);
        }).then(function () {
            syncPlayer();
            flashSaved();
        }).catch(function (err) {
            toast(err.message, true);
        });
    }

    function renamePlaylist(name) {
        apiFetch(API_PLAYLISTS + '/' + currentPlaylistId, {
            method: 'PUT',
            body: JSON.stringify({ name: name })
        }).then(function () {
            var pl = playlists.find(function (p) { return p.id === currentPlaylistId; });
            if (pl) pl.name = name;
            currentPlaylistName = name;
            resetNameMode();
            renderPlaylistSelect();
            if (typeof playlistManager !== 'undefined' && playlistManager.loadPlaylists) {
                playlistManager.loadPlaylists();
            }
            flashSaved();
        }).catch(function (err) {
            toast(err.message, true);
        });
    }

    function deletePlaylist() {
        if (!currentPlaylistId) return;
        if (!confirm('Delete playlist "' + currentPlaylistName + '" and all its tracks?')) return;
        apiFetch(API_PLAYLISTS + '/' + currentPlaylistId, { method: 'DELETE' })
            .then(function () {
                playlists = playlists.filter(function (p) { return p.id !== currentPlaylistId; });
                localStorage.removeItem('musicbuddy_last_playlist_' + fileType);
                if (playlists.length > 0) {
                    return switchPlaylist(playlists[0].id);
                }
                currentPlaylistId = null;
                currentPlaylistName = '';
                renderPlaylistSelect();
                loadTracks();
                syncPlayer();
            }).then(function () {
                flashSaved();
            }).catch(function (err) {
                toast(err.message, true);
            });
    }

    function clearPlaylist() {
        if (!currentPlaylistId) return;
        if (!confirm('Remove all tracks from this playlist?')) return;
        apiFetch(API_PLAYLISTS + '/' + currentPlaylistId + '/tracks', { method: 'DELETE' })
            .then(function () {
                flashSaved();
                syncPlayer();
                return loadTracks();
            }).catch(function (err) {
                toast(err.message, true);
            });
    }

    function removeTrack(trackId) {
        if (!currentPlaylistId) return;
        apiFetch(API_PLAYLISTS + '/' + currentPlaylistId + '/tracks/' + trackId, { method: 'DELETE' })
            .then(function () {
                flashSaved();
                syncPlayer();
                return loadTracks();
            }).catch(function (err) {
                toast(err.message, true);
            });
    }

    function postTracks(files, insertIndex) {
        return apiFetch(API_PLAYLISTS + '/' + currentPlaylistId + '/tracks', {
            method: 'POST',
            body: JSON.stringify(files)
        }).then(function (added) {
            if (insertIndex >= 0 && added && added.length > 0) {
                var ids = added.map(function (a) { return a.id; });
                var order = [];
                tracks.forEach(function (t, i) {
                    if (i === insertIndex) order = order.concat(ids);
                    order.push(t.id);
                });
                if (tracks.length <= insertIndex) order = order.concat(ids);
                var reorder = order.map(function (id, i) { return { id: id, sortOrder: i + 1 }; });
                return apiFetch(API_PLAYLISTS + '/' + currentPlaylistId + '/tracks/reorder', {
                    method: 'PUT',
                    body: JSON.stringify(reorder)
                });
            }
        }).then(function () {
            flashSaved();
            syncPlayer();
            return loadTracks();
        }).catch(function (err) {
            toast(err.message, true);
        });
    }

    function addToPlaylist(payload, insertIndex) {
        if (!currentPlaylistId) {
            toast('Select or create a playlist first.', true);
            return;
        }
        var direct = payload.filter(function (p) { return p.kind === 'file'; }).map(function (p) {
            return { filePath: p.path, fileName: p.name, size: p.size, channelCount: p.channelCount || 2 };
        });
        var albums = payload.filter(function (p) { return p.kind === 'album'; });

        if (albums.length === 0) {
            if (direct.length === 0) return;
            return postTracks(direct, insertIndex);
        }

        toast('Gathering files from ' + albums.length + ' album' + (albums.length === 1 ? '' : 's') + '…');
        var collects = albums.map(function (a) {
            return apiFetch(API_COLLECT + '?type=' + encodeURIComponent(fileType) + '&path=' + encodeURIComponent(a.path))
                .then(function (entries) { return entries || []; });
        });
        return Promise.all(collects).then(function (results) {
            var seen = {};
            tracks.forEach(function (t) { seen[t.filePath] = true; });
            var albumFiles = [];
            results.forEach(function (entries) {
                entries.forEach(function (e) {
                    if (!seen[e.path]) {
                        seen[e.path] = true;
                        albumFiles.push({ filePath: e.path, fileName: e.name, size: e.size, channelCount: e.channelCount || 2 });
                    }
                });
            });
            var all = direct.concat(albumFiles);
            if (all.length === 0) {
                toast('No new tracks found in that folder.');
                return;
            }
            return postTracks(all, insertIndex);
        }).catch(function (err) {
            toast(err.message, true);
        });
    }

    function saveReorder() {
        var rows = Array.from(tracksEl.children).filter(function (el) { return el.classList.contains('pe-track-row'); });
        var order = rows.map(function (el) { return parseInt(el.dataset.trackId); });
        var byId = {};
        tracks.forEach(function (t) { byId[t.id] = t; });
        tracks = order.map(function (id) { return byId[id]; }).filter(function (t) { return !!t; });
        renderTracks();

        var reorder = order.map(function (id, i) { return { id: id, sortOrder: i + 1 }; });
        apiFetch(API_PLAYLISTS + '/' + currentPlaylistId + '/tracks/reorder', {
            method: 'PUT',
            body: JSON.stringify(reorder)
        }).then(function () {
            flashSaved();
            syncPlayer();
        }).catch(function (err) {
            toast(err.message, true);
            loadTracks();
        });
    }

    function renderBreadcrumb() {
        var el = $('pe-breadcrumb');
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

        if (!browserPath) return;

        var parts = browserPath.split('/');
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

    function navigateTo(path) {
        browserPath = path;
        renderBreadcrumb();
        if (libraryEl) {
            libraryEl.innerHTML = '<div class="text-center py-4"><div class="spinner-border text-primary" role="status"><span class="visually-hidden">Loading...</span></div></div>';
        }
        var url = API_BROWSE + '?type=' + encodeURIComponent(fileType) + '&path=' + encodeURIComponent(path);
        fetch(url)
            .then(function (res) {
                if (!res.ok) throw new Error('Failed to load directory');
                return res.json();
            })
            .then(function (listing) { renderLibrary(listing); })
            .catch(function () {
                if (libraryEl) {
                    libraryEl.innerHTML = '<div class="text-center text-danger py-4">Error loading directory</div>';
                }
            });
    }

    function renderLibrary(listing) {
        if (!libraryEl) return;
        libraryEl.innerHTML = '';

        if (listing.directories && listing.directories.length > 0) {
            var dirHeader = document.createElement('div');
            dirHeader.className = 'pe-section-header';
            dirHeader.textContent = 'Directories';
            libraryEl.appendChild(dirHeader);

            listing.directories.forEach(function (dir) {
                var row = document.createElement('div');
                row.className = 'list-group-item pe-library-row d-flex align-items-center';
                row.draggable = true;
                row.dataset.kind = 'album';
                row.dataset.path = dir.path;
                row.dataset.name = dir.name;
                row.innerHTML =
                    '<input type="checkbox" class="form-check-input me-2 pe-check" aria-label="Select directory">' +
                    '<i class="bi bi-folder-fill text-warning me-2"></i>' +
                    '<span class="text-truncate flex-grow-1">' + escapeHtml(dir.name) + '</span>' +
                    '<span class="badge text-bg-secondary">Directory</span>';
                row.querySelector('.pe-check').checked = selection.some(function (s) { return s.kind === 'album' && s.path === dir.path; });
                libraryEl.appendChild(row);
            });
        }

        if (listing.files && listing.files.length > 0) {
            var fileHeader = document.createElement('div');
            fileHeader.className = 'pe-section-header';
            fileHeader.textContent = 'Files (' + listing.files.length + ')';
            libraryEl.appendChild(fileHeader);

            listing.files.forEach(function (file) {
                var row = document.createElement('div');
                row.className = 'list-group-item pe-library-row d-flex align-items-center';
                row.draggable = true;
                row.dataset.kind = 'file';
                row.dataset.path = file.path;
                row.dataset.name = file.name;
                row.dataset.size = file.size;
                row.dataset.channelCount = file.channelCount;
                row.innerHTML =
                    '<input type="checkbox" class="form-check-input me-2 pe-check" aria-label="Select file">' +
                    '<i class="bi bi-file-earmark-music text-primary me-2"></i>' +
                    '<span class="text-truncate flex-grow-1">' + escapeHtml(file.name) + '</span>' +
                    '<small class="text-nowrap text-muted ms-2">' + formatSize(file.size) + '</small>';
                row.querySelector('.pe-check').checked = selection.some(function (s) { return s.kind === 'file' && s.path === file.path; });
                libraryEl.appendChild(row);
            });
        }

        if ((!listing.directories || listing.directories.length === 0) &&
            (!listing.files || listing.files.length === 0)) {
            var empty = document.createElement('div');
            empty.className = 'text-center text-muted py-4';
            empty.textContent = 'Empty folder';
            libraryEl.appendChild(empty);
        }

        updateSelectedCount();
    }

    function itemFromRow(row) {
        if (row.dataset.kind === 'album') {
            return { kind: 'album', path: row.dataset.path, name: row.dataset.name };
        }
        return {
            kind: 'file',
            path: row.dataset.path,
            name: row.dataset.name,
            size: parseInt(row.dataset.size) || 0,
            channelCount: parseInt(row.dataset.channelCount) || 2
        };
    }

    function setRowChecked(row, checked) {
        var cb = row.querySelector('.pe-check');
        if (cb) cb.checked = checked;
    }

    function isRowSelected(row) {
        return selection.some(function (s) { return s.kind === row.dataset.kind && s.path === row.dataset.path; });
    }

    function syncRowSelection(row) {
        var cb = row.querySelector('.pe-check');
        var item = itemFromRow(row);
        var idx = selection.findIndex(function (s) { return s.kind === item.kind && s.path === item.path; });
        if (cb.checked && idx < 0) {
            selection.push(item);
        } else if (!cb.checked && idx >= 0) {
            selection.splice(idx, 1);
        }
        updateSelectedCount();
    }

    function updateSelectedCount() {
        var el = $('pe-selected-count');
        if (el) el.textContent = selection.length + ' selected';
        var addBtn = $('pe-add-selected-btn');
        if (addBtn) addBtn.disabled = selection.length === 0 || !currentPlaylistId;
    }

    function selectAllVisible() {
        if (!libraryEl) return;
        libraryEl.querySelectorAll('.pe-library-row').forEach(function (row) {
            setRowChecked(row, true);
            syncRowSelection(row);
        });
    }

    function deselectAll() {
        selection = [];
        if (libraryEl) {
            libraryEl.querySelectorAll('.pe-library-row').forEach(function (row) { setRowChecked(row, false); });
        }
        updateSelectedCount();
    }

    function updateDropIndicator(e) {
        var rows = Array.from(tracksEl.children).filter(function (el) { return el.classList.contains('pe-track-row'); });
        var index = rows.length;
        for (var i = 0; i < rows.length; i++) {
            var r = rows[i].getBoundingClientRect();
            if (e.clientY < r.top + r.height / 2) { index = i; break; }
        }
        dropIndex = index;

        var newRow = index < rows.length ? rows[index] : rows[rows.length - 1];
        var isBottom = index >= rows.length;
        if (dropRowEl !== newRow) {
            if (dropRowEl) dropRowEl.classList.remove('pe-indicator-top', 'pe-indicator-bottom');
            dropRowEl = newRow;
        }
        if (dropRowEl) {
            dropRowEl.classList.remove('pe-indicator-top', 'pe-indicator-bottom');
            dropRowEl.classList.add(isBottom ? 'pe-indicator-bottom' : 'pe-indicator-top');
        }
    }

    function clearDropIndicator() {
        if (dropRowEl) {
            dropRowEl.classList.remove('pe-indicator-top', 'pe-indicator-bottom');
            dropRowEl = null;
        }
        dropIndex = -1;
    }

    function autoScroll(e) {
        var rect = tracksEl.getBoundingClientRect();
        var margin = 48;
        if (e.clientY < rect.top + margin) tracksEl.scrollTop -= 12;
        else if (e.clientY > rect.bottom - margin) tracksEl.scrollTop += 12;
    }

    function initUI() {
        tracksEl = $('pe-tracks');
        libraryEl = $('pe-library');

        $('pe-playlist-select').addEventListener('change', function () {
            var id = this.value ? parseInt(this.value) : null;
            switchPlaylist(id);
        });

        $('pe-create-btn').addEventListener('click', function () { focusNameMode('create'); });
        $('pe-rename-btn').addEventListener('click', function () {
            if (currentPlaylistId) focusNameMode('rename');
        });
        $('pe-delete-btn').addEventListener('click', deletePlaylist);
        $('pe-clear-btn').addEventListener('click', clearPlaylist);
        $('pe-name-save').addEventListener('click', saveName);
        $('pe-name-cancel').addEventListener('click', resetNameMode);
        $('pe-name-input').addEventListener('keydown', function (e) {
            if (e.key === 'Enter') { e.preventDefault(); saveName(); }
            else if (e.key === 'Escape') { e.preventDefault(); e.stopPropagation(); resetNameMode(); }
        });
        $('pe-select-all-btn').addEventListener('click', selectAllVisible);
        $('pe-deselect-all-btn').addEventListener('click', deselectAll);
        $('pe-add-selected-btn').addEventListener('click', function () {
            if (selection.length > 0) addToPlaylist(selection.slice(), -1);
        });

        if (tracksEl) {
            tracksEl.addEventListener('click', function (e) {
                var btn = e.target.closest('.pe-remove-btn');
                if (!btn) return;
                var row = btn.closest('.pe-track-row');
                if (row) removeTrack(parseInt(row.dataset.trackId));
            });
            tracksEl.addEventListener('dragstart', function (e) {
                var row = e.target.closest('.pe-track-row');
                if (!row) { e.preventDefault(); return; }
                dragSource = 'reorder';
                dragPayload = parseInt(row.dataset.trackId);
                e.dataTransfer.effectAllowed = 'move';
                e.dataTransfer.setData('text/plain', 'reorder');
                row.classList.add('pe-dragging');
            });
            tracksEl.addEventListener('dragend', function () {
                tracksEl.querySelectorAll('.pe-track-row').forEach(function (r) { r.classList.remove('pe-dragging'); });
                clearDropIndicator();
                dragSource = null;
                dragPayload = null;
            });
            tracksEl.addEventListener('dragover', function (e) {
                if (dragSource === null) return;
                e.preventDefault();
                e.dataTransfer.dropEffect = dragSource === 'reorder' ? 'move' : 'copy';
                updateDropIndicator(e);
                autoScroll(e);
            });
            tracksEl.addEventListener('dragleave', function (e) {
                if (!tracksEl.contains(e.relatedTarget)) clearDropIndicator();
            });
            tracksEl.addEventListener('drop', function (e) {
                e.preventDefault();
                if (dragSource === null) return;
                var index = dropIndex;
                clearDropIndicator();

                if (dragSource === 'reorder') {
                    var draggedId = dragPayload;
                    var rows = Array.from(tracksEl.children).filter(function (el) {
                        return el.classList.contains('pe-track-row') && parseInt(el.dataset.trackId) !== draggedId;
                    });
                    var dragged = tracksEl.querySelector('.pe-track-row[data-track-id="' + draggedId + '"]');
                    if (dragged) {
                        var target = rows[index] || null;
                        if (target) tracksEl.insertBefore(dragged, target);
                        else tracksEl.appendChild(dragged);
                        var newPos = tracksEl.querySelectorAll('.pe-track-row');
                        var reordered = Array.from(newPos).map(function (el) { return parseInt(el.dataset.trackId); });
                        if (reordered.join(',') !== tracks.map(function (t) { return t.id; }).join(',')) {
                            saveReorder();
                        } else {
                            renderTracks();
                        }
                    }
                } else if (dragSource === 'library') {
                    addToPlaylist(dragPayload.slice(), index);
                }
                dragSource = null;
                dragPayload = null;
            });
        }

        if (libraryEl) {
            libraryEl.addEventListener('dragstart', function (e) {
                var row = e.target.closest('.pe-library-row');
                if (!row) { e.preventDefault(); return; }
                var payload = isRowSelected(row) ? selection.slice() : [itemFromRow(row)];
                if (payload.length === 0) { e.preventDefault(); return; }
                dragSource = 'library';
                dragPayload = payload;
                e.dataTransfer.effectAllowed = 'copy';
                e.dataTransfer.setData('text/plain', payload.length + ' item(s)');
                row.classList.add('pe-dragging');
            });
            libraryEl.addEventListener('dragend', function (e) {
                var row = e.target.closest('.pe-library-row');
                if (row) row.classList.remove('pe-dragging');
                dragSource = null;
                dragPayload = null;
            });
            libraryEl.addEventListener('click', function (e) {
                var row = e.target.closest('.pe-library-row');
                if (!row) return;
                if (e.target.closest('.form-check-input')) return;
                if (row.dataset.kind === 'album') {
                    navigateTo(row.dataset.path);
                } else {
                    var cb = row.querySelector('.pe-check');
                    cb.checked = !cb.checked;
                    syncRowSelection(row);
                }
            });
            libraryEl.addEventListener('change', function (e) {
                if (e.target.matches('.pe-check')) {
                    syncRowSelection(e.target.closest('.pe-library-row'));
                }
            });
        }
    }

    function loadAll() {
        resetNameMode();
        deselectAll();
        var select = $('pe-playlist-select');
        if (select) select.innerHTML = '<option value="">Loading…</option>';
        loadPlaylists();
        navigateTo('');
    }

    return {
        open: function (options) {
            fileType = options.fileType || 'mp3';
            if (!modal) {
                modal = $('playlistEditorModal');
                if (!modal) return;
                modal.addEventListener('shown.bs.modal', loadAll);
                initUI();
            }
            var bsModal = bootstrap.Modal.getOrCreateInstance(modal);
            bsModal.show();
            if (modal.classList.contains('show')) loadAll();
        }
    };
})();