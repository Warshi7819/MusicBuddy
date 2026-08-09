var playlistManager = (function () {
    var apiBase = '/api/playlists';
    var currentFileType = 'mp3';
    var currentPlaylistId = null;
    var onPlaylistLoaded = null;

    function apiFetch(url, options) {
        return fetch(url, Object.assign({ headers: { 'Content-Type': 'application/json' } }, options || {}))
            .then(function (res) {
                if (!res.ok) throw new Error('API error');
                if (res.status === 204) return null;
                return res.json();
            });
    }

    function updateButtonStates() {
        var addBtn = document.getElementById('pm-add-files-btn');
        var clearBtn = document.getElementById('pm-clear-btn');
        var deleteBtn = document.getElementById('pm-delete-btn');
        var hasPlaylist = !!currentPlaylistId;
        if (addBtn) addBtn.disabled = !hasPlaylist;
        if (clearBtn) clearBtn.disabled = !hasPlaylist;
        if (deleteBtn) deleteBtn.disabled = !hasPlaylist;
    }

    function renderPlaylistDropdown(playlists) {
        var select = document.getElementById('pm-playlist-select');
        if (!select) return;
        select.innerHTML = '<option value="">-- Select playlist --</option>';
        playlists.forEach(function (p) {
            var opt = document.createElement('option');
            opt.value = p.id;
            opt.textContent = p.name;
            if (p.id === currentPlaylistId) opt.selected = true;
            select.appendChild(opt);
        });
    }

    function renderTrackList(tracks) {
        var el = document.getElementById('pm-tracks');
        if (!el) return;
        el.innerHTML = '';
        var countEl = document.getElementById('pm-track-count');
        if (countEl) countEl.textContent = tracks.length;

        tracks.forEach(function (t, i) {
            var item = document.createElement('div');
            item.className = 'list-group-item d-flex align-items-center';
            item.draggable = true;
            item.dataset.trackId = t.id;
            item.dataset.sortOrder = t.sortOrder;

            var grip = document.createElement('span');
            grip.className = 'bi bi-grip-vertical text-muted me-2';
            grip.style.cursor = 'grab';
            item.appendChild(grip);

            var num = document.createElement('span');
            num.className = 'text-muted me-2';
            num.style.minWidth = '24px';
            num.textContent = (i + 1) + '.';
            item.appendChild(num);

            var name = document.createElement('span');
            name.className = 'text-truncate flex-grow-1';
            name.textContent = t.fileName;
            item.appendChild(name);

            var size = document.createElement('small');
            size.className = 'text-muted ms-2';
            size.textContent = formatSize(t.fileSize);
            item.appendChild(size);

            var removeBtn = document.createElement('button');
            removeBtn.type = 'button';
            removeBtn.className = 'btn btn-sm btn-outline-danger ms-2';
            removeBtn.innerHTML = '<i class="bi bi-x"></i>';
            removeBtn.addEventListener('click', function () {
                removeTrack(t.id);
            });
            item.appendChild(removeBtn);

            el.appendChild(item);
        });

        setupDragDrop();
    }

    function formatSize(bytes) {
        if (bytes < 1024) return bytes + ' B';
        if (bytes < 1048576) return (bytes / 1024).toFixed(1) + ' KB';
        return (bytes / 1048576).toFixed(1) + ' MB';
    }

    function setupDragDrop() {
        var container = document.getElementById('pm-tracks');
        if (!container) return;
        var draggedItem = null;

        container.addEventListener('dragstart', function (e) {
            var item = e.target.closest('[data-track-id]');
            if (item) {
                draggedItem = item;
                item.classList.add('opacity-50');
                e.dataTransfer.effectAllowed = 'move';
            }
        });

        container.addEventListener('dragend', function (e) {
            var item = e.target.closest('[data-track-id]');
            if (item) item.classList.remove('opacity-50');
            draggedItem = null;
        });

        container.addEventListener('dragover', function (e) {
            e.preventDefault();
            e.dataTransfer.dropEffect = 'move';
        });

        container.addEventListener('drop', function (e) {
            e.preventDefault();
            if (!draggedItem) return;
            var target = e.target.closest('[data-track-id]');
            if (!target || target === draggedItem) return;

            var items = Array.from(container.children);
            var fromIdx = items.indexOf(draggedItem);
            var toIdx = items.indexOf(target);

            if (fromIdx < toIdx) {
                container.insertBefore(draggedItem, target.nextSibling);
            } else {
                container.insertBefore(draggedItem, target);
            }

            var reordered = Array.from(container.children).map(function (el, i) {
                return { id: parseInt(el.dataset.trackId), sortOrder: i + 1 };
            });
            apiFetch(apiBase + '/' + currentPlaylistId + '/tracks/reorder', {
                method: 'PUT',
                body: JSON.stringify(reordered)
            });
        });
    }

    function removeTrack(trackId) {
        if (!currentPlaylistId) return;
        apiFetch(apiBase + '/' + currentPlaylistId + '/tracks/' + trackId, { method: 'DELETE' })
            .then(function () { return loadPlaylistTracks(); });
    }

    function loadPlaylistTracks() {
        if (!currentPlaylistId) {
            renderTrackList([]);
            updateButtonStates();
            if (onPlaylistLoaded) onPlaylistLoaded(null);
            return Promise.resolve();
        }
        return apiFetch(apiBase + '/' + currentPlaylistId)
            .then(function (data) {
                renderTrackList(data.tracks || []);
                updateButtonStates();
                if (onPlaylistLoaded) onPlaylistLoaded(data);
            });
    }

    function refreshPlaylistList() {
        return apiFetch(apiBase + '?type=' + currentFileType).then(function (playlists) {
            renderPlaylistDropdown(playlists);
        });
    }

    return {
        init: function (options) {
            currentFileType = options.fileType || 'mp3';
            onPlaylistLoaded = options.onPlaylistLoaded || null;

            var select = document.getElementById('pm-playlist-select');
            if (select) {
                select.addEventListener('change', function () {
                    currentPlaylistId = this.value ? parseInt(this.value) : null;
                    loadPlaylistTracks();
                });
            }

            var createBtn = document.getElementById('pm-create-btn');
            if (createBtn) {
                createBtn.addEventListener('click', function () {
                    var name = prompt('Playlist name:');
                    if (!name) return;
                    apiFetch(apiBase, {
                        method: 'POST',
                        body: JSON.stringify({ name: name, fileType: currentFileType })
                    }).then(function (playlist) {
                        currentPlaylistId = playlist.id;
                        return refreshPlaylistList();
                    }).then(function () {
                        loadPlaylistTracks();
                    });
                });
            }

            var deleteBtn = document.getElementById('pm-delete-btn');
            if (deleteBtn) {
                deleteBtn.addEventListener('click', function () {
                    if (!currentPlaylistId) return;
                    if (!confirm('Delete this playlist?')) return;
                    apiFetch(apiBase + '/' + currentPlaylistId, { method: 'DELETE' })
                        .then(function () {
                            currentPlaylistId = null;
                            return refreshPlaylistList();
                        }).then(function () {
                            loadPlaylistTracks();
                        });
                });
            }

            var addBtn = document.getElementById('pm-add-files-btn');
            if (addBtn) {
                addBtn.addEventListener('click', function () {
                    if (!currentPlaylistId) {
                        alert('Please select or create a playlist first.');
                        return;
                    }
                    fileBrowser.open({
                        fileType: currentFileType,
                        onConfirm: function (files) {
                            var tracks = files.map(function (f) {
                                return { filePath: f.path, fileName: f.name, size: f.size };
                            });
                            apiFetch(apiBase + '/' + currentPlaylistId + '/tracks', {
                                method: 'POST',
                                body: JSON.stringify(tracks)
                            }).then(function () {
                                return loadPlaylistTracks();
                            });
                        }
                    });
                });
            }

            var clearBtn = document.getElementById('pm-clear-btn');
            if (clearBtn) {
                clearBtn.addEventListener('click', function () {
                    if (!currentPlaylistId) return;
                    if (!confirm('Remove all tracks from this playlist?')) return;
                    apiFetch(apiBase + '/' + currentPlaylistId + '/tracks', { method: 'DELETE' })
                        .then(function () { return loadPlaylistTracks(); });
                });
            }

            return refreshPlaylistList().then(function () {
                loadPlaylistTracks();
            });
        },

        loadPlaylists: function () {
            return refreshPlaylistList();
        },

        selectPlaylist: function (id) {
            currentPlaylistId = id;
            var select = document.getElementById('pm-playlist-select');
            if (select) select.value = id || '';
            return loadPlaylistTracks();
        },

        addTracksToPlaylist: function (playlistId, tracks) {
            return apiFetch(apiBase + '/' + playlistId + '/tracks', {
                method: 'POST',
                body: JSON.stringify(tracks)
            });
        },

        getCurrentPlaylistId: function () {
            return currentPlaylistId;
        },

        getTracks: function () {
            if (!currentPlaylistId) return Promise.resolve([]);
            return apiFetch(apiBase + '/' + currentPlaylistId).then(function (data) {
                return data.tracks || [];
            });
        }
    };
})();
