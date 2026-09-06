(function () {
    'use strict';

    var container = document.getElementById('pe-container');
    if (!container) return;

    var fileType = container.getAttribute('data-file-type') || 'mp3';
    var currentPlaylistId = null;
    var currentPlaylistName = '';
    var nameMode = null;
    var savedTimer = null;
    var selection = [];
    var dragSource = null;
    var dragPayload = null;
    var dropIndex = -1;
    var dropRowEl = null;
    var previewContext = [];
    var previewIndex = -1;
    var pendingRemoves = new Set();
    var libraryMode = 'browse';
    var currentLibPath = '';
    var searchSortField = '';
    var searchSortAsc = true;

    // --- Self-contained playback ---
    var audio = new Audio();
    var playing = false;
    var seekDragging = false;

    function $(id) { return document.getElementById(id); }

    function flashSaved() {
        var el = $('pe-saved-indicator');
        if (!el) return;
        el.style.display = '';
        clearTimeout(savedTimer);
        savedTimer = setTimeout(function () { el.style.display = 'none'; }, 1500);
    }

    function toast(message, isError) {
        var c = $('pe-toast-container');
        if (!c) return;
        var t = document.createElement('div');
        t.className = 'pe-toast' + (isError ? ' pe-toast-error' : '');
        t.textContent = message;
        c.appendChild(t);
        setTimeout(function () {
            t.classList.add('pe-toast-out');
            setTimeout(function () { t.remove(); }, 300);
        }, 3500);
    }

    function formatTimeShort(sec) {
        if (isNaN(sec) || sec < 0) return '0:00';
        var m = Math.floor(sec / 60);
        var s = Math.floor(sec % 60);
        return m + ':' + (s < 10 ? '0' : '') + s;
    }

    function setPlayingRow(path) {
        var tracksList = $('pe-tracks');
        if (tracksList) {
            var rows = tracksList.querySelectorAll('.pe-track-row');
            var target = -1;
            for (var i = 0; i < rows.length; i++) {
                if (rows[i].dataset.path === path) { target = i; break; }
            }
            for (var j = 0; j < rows.length; j++) {
                var active = j === target;
                rows[j].classList.toggle('pe-playing', active);
                if (active) rows[j].scrollIntoView({ block: 'nearest' });
            }
        }
        var libList = $('pe-library');
        if (libList) {
            libList.querySelectorAll('.pe-library-row').forEach(function (row) {
                row.classList.toggle('pe-playing', row.dataset.path === path);
            });
        }
    }

    function updateTransportUI() {
        var playEl = $('pe-t-play');
        var timeEl = $('pe-t-time');
        var seekEl = $('pe-t-seek');
        var icon = playEl ? playEl.querySelector('i') : null;

        if (fileType === 'mp3') {
            if (icon) icon.className = playing ? 'bi bi-pause-fill' : 'bi bi-play-fill';
            if (timeEl) timeEl.textContent = formatTimeShort(audio.currentTime) + ' / ' + formatTimeShort(audio.duration);
            if (seekEl) {
                var dur = audio.duration || 0;
                seekEl.disabled = dur <= 0 || isNaN(dur);
                if (dur > 0 && !seekDragging) seekEl.value = Math.round((audio.currentTime / dur) * 1000);
            }
        } else if (fileType === 'sid') {
            if (icon) icon.className = playing ? 'bi bi-pause-fill' : 'bi bi-play-fill';
            if (timeEl) timeEl.textContent = playing ? 'Playing...' : '0:00 / 0:00';
            if (seekEl) seekEl.disabled = true;
        }
    }

    function playTrack(path, context) {
        previewContext = context || [];
        previewIndex = previewContext.indexOf(path);
        setPlayingRow(path);

        if (fileType === 'mp3') {
            audio.src = path;
            audio.play().then(function () {
                playing = true;
                updateTransportUI();
            }).catch(function () {});
        } else if (fileType === 'sid') {
            if (typeof playSID === 'function') {
                playSID(path, 0);
                playing = true;
                updateTransportUI();
            }
        }
    }

    function stopPlayback() {
        if (fileType === 'mp3') {
            audio.pause();
            audio.currentTime = 0;
            audio.src = '';
        }
        playing = false;
        updateTransportUI();
        // Clear playing highlights
        document.querySelectorAll('.pe-playing').forEach(function (el) { el.classList.remove('pe-playing'); });
    }

    function togglePlayPause() {
        if (fileType === 'mp3') {
            if (playing) {
                audio.pause();
                playing = false;
            } else if (audio.src) {
                audio.play().then(function () { playing = true; }).catch(function () {});
            }
        } else if (fileType === 'sid') {
            // SID emulation doesn't expose pause — just toggle the flag for UI
            if (typeof SIDplayer !== 'undefined' && SIDplayer.pause) {
                if (playing) { SIDplayer.pause(); playing = false; }
                else { SIDplayer.playcont(); playing = true; }
            }
        }
        updateTransportUI();
    }

    function playNext() {
        if (previewContext.length === 0) return;
        var nextIndex = previewIndex + 1;
        if (nextIndex < previewContext.length) {
            playTrack(previewContext[nextIndex], previewContext);
        }
    }

    function playPrev() {
        if (previewContext.length === 0) return;
        var prevIndex = previewIndex - 1;
        if (prevIndex >= 0) {
            playTrack(previewContext[prevIndex], previewContext);
        }
    }

    // MP3 audio events
    audio.addEventListener('timeupdate', function () {
        if (fileType === 'mp3' && !seekDragging) updateTransportUI();
    });
    audio.addEventListener('ended', function () {
        playNext();
    });
    audio.addEventListener('play', function () {
        playing = true;
        updateTransportUI();
    });
    audio.addEventListener('pause', function () {
        if (!audio.ended) { playing = false; updateTransportUI(); }
    });

    // Periodic UI update for transport
    var transportTimer = setInterval(updateTransportUI, 250);

    function focusNameMode(mode) {
        nameMode = mode;
        $('pe-name-label').textContent = mode === 'rename' ? 'Rename playlist:' : 'New playlist name:';
        $('pe-name-input').value = mode === 'rename' ? currentPlaylistName : '';
        $('pe-name-collapse').classList.add('pe-name-open');
        $('pe-name-input').focus();
    }

    function resetNameMode() {
        nameMode = null;
        $('pe-name-collapse').classList.remove('pe-name-open');
        $('pe-name-input').value = '';
    }

    function updateButtons() {
        var hasSelection = currentPlaylistId !== null;
        $('pe-rename-btn').disabled = !hasSelection;
        $('pe-delete-btn').disabled = !hasSelection;
        var clearBtn = $('pe-clear-btn');
        if (clearBtn) clearBtn.disabled = !currentPlaylistId;
    }

    function updateTrackCount() {
        var count = $('pe-tracks').querySelectorAll('.pe-track-row').length;
        var clearBtn = $('pe-clear-btn');
        if (clearBtn) clearBtn.disabled = !currentPlaylistId || count === 0;
    }

    // --- Library mode toggle ---
    function setLibraryMode(mode) {
        libraryMode = mode;
        var browseControls = $('pe-browse-controls');
        var searchControls = $('pe-search-controls');
        if (browseControls) browseControls.style.display = mode === 'browse' ? '' : 'none';
        if (searchControls) searchControls.style.display = mode === 'search' ? '' : 'none';
        deselectAll();
        if (mode === 'browse') {
            navigateTo(currentLibPath);
        } else {
            $('pe-library').innerHTML = '<div class="text-center text-muted py-4"><i class="bi bi-search" style="font-size: 2rem;"></i><div class="mt-2">Type to search by song name, artist, or genre</div></div>';
        }
    }

    // --- Browse mode ---
    function navigateTo(path) {
        currentLibPath = path;
        $('pe-library').innerHTML = '<div class="text-center py-4"><div class="spinner-border text-primary" role="status"></div></div>';
        htmx.ajax('GET', '/Playlists?handler=Library&type=' + encodeURIComponent(fileType) + '&path=' + encodeURIComponent(path), {
            target: '#pe-library',
            swap: 'innerHTML'
        }).catch(function (err) {
            console.error('Browse request failed:', err);
            $('pe-library').innerHTML = '<div class="text-danger py-3"><i class="bi bi-exclamation-triangle me-2"></i>Error loading directory.</div>';
        });
        renderBreadcrumb(path);
    }

    function renderBreadcrumb(path) {
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
        if (!path) return;
        var parts = path.split('/');
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

    // --- Search mode ---
    function doSearch() {
        var songVal = ($('pe-search-song') || {}).value || '';
        var artistVal = ($('pe-search-artist') || {}).value || '';
        var genreVal = ($('pe-search-genre') || {}).value || '';
        if (!songVal.trim() && !artistVal.trim() && !genreVal.trim()) {
            $('pe-library').innerHTML = '<div class="text-center text-muted py-4"><i class="bi bi-search" style="font-size: 2rem;"></i><div class="mt-2">Type to search by song name, artist, or genre</div></div>';
            return;
        }
        $('pe-library').innerHTML = '<div class="text-center py-4"><div class="spinner-border text-primary" role="status"></div></div>';
        var qs = 'type=' + encodeURIComponent(fileType);
        if (songVal.trim()) qs += '&song=' + encodeURIComponent(songVal.trim());
        if (artistVal.trim()) qs += '&artist=' + encodeURIComponent(artistVal.trim());
        if (genreVal.trim()) qs += '&genre=' + encodeURIComponent(genreVal.trim());
        htmx.ajax('GET', '/Playlists?handler=Search&' + qs, {
            target: '#pe-library',
            swap: 'innerHTML'
        }).catch(function (err) {
            console.error('Search request failed:', err);
            $('pe-library').innerHTML = '<div class="text-danger py-3"><i class="bi bi-exclamation-triangle me-2"></i>Error searching.</div>';
        });
    }

    // --- Search sort ---
    function sortSearchResults(field) {
        var resultsEl = $('pe-search-results');
        if (!resultsEl) return;
        if (searchSortField === field) {
            searchSortAsc = !searchSortAsc;
        } else {
            searchSortField = field;
            searchSortAsc = true;
        }
        var rows = Array.from(resultsEl.querySelectorAll('.pe-library-row'));
        rows.sort(function (a, b) {
            var va = a.getAttribute('data-' + field) || '';
            var vb = b.getAttribute('data-' + field) || '';
            var cmp = va.localeCompare(vb);
            return searchSortAsc ? cmp : -cmp;
        });
        rows.forEach(function (row) { resultsEl.appendChild(row); });
        document.querySelectorAll('.pe-sort-col').forEach(function (el) {
            var icon = el.querySelector('i');
            if (!icon) return;
            if (el.getAttribute('data-sort-field') === field) {
                icon.className = 'bi ' + (searchSortAsc ? 'bi-arrow-down' : 'bi-arrow-up') + ' ms-1 small';
            } else {
                icon.className = 'bi bi-arrow-down-up ms-1 small';
            }
        });
    }

    // --- API helpers ---
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

    // --- Playlist CRUD ---
    function saveName() {
        if (!nameMode) return;
        var name = $('pe-name-input').value.trim();
        if (!name) { toast('Enter a playlist name.', true); $('pe-name-input').focus(); return; }

        if (nameMode === 'create') {
            apiFetch('/Playlists?handler=Create', {
                method: 'POST',
                body: JSON.stringify({ name: name, fileType: fileType }),
                headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': document.querySelector('input[name="__RequestVerificationToken"]')?.value || '' }
            }).then(function (playlist) {
                currentPlaylistId = playlist.id;
                currentPlaylistName = name;
                resetNameMode();
                refreshPlaylistSelect(playlist.id);
                flashSaved();
            }).catch(function (err) { toast(err.message, true); });
        } else {
            apiFetch('/Playlists?handler=Rename&id=' + currentPlaylistId + '&name=' + encodeURIComponent(name) + '&type=' + encodeURIComponent(fileType), {
                method: 'POST',
                headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': document.querySelector('input[name="__RequestVerificationToken"]')?.value || '' }
            }).then(function () {
                currentPlaylistName = name;
                resetNameMode();
                refreshPlaylistSelect(currentPlaylistId);
                flashSaved();
            }).catch(function (err) { toast(err.message, true); });
        }
    }

    function refreshPlaylistSelect(selectId) {
        var listUrl = '/Playlists?handler=PlaylistList&type=' + encodeURIComponent(fileType);
        htmx.ajax('GET', listUrl, {
            target: '#pe-playlist-select',
            swap: 'innerHTML'
        }).then(function () {
            if (selectId) {
                $('pe-playlist-select').value = selectId;
                currentPlaylistId = selectId;
                var opt = $('pe-playlist-select').options[$('pe-playlist-select').selectedIndex];
                currentPlaylistName = opt ? opt.textContent : '';
                htmx.ajax('GET', '/Playlists?handler=TrackList&playlistId=' + selectId, {
                    target: '#pe-tracks', swap: 'innerHTML'
                }).catch(function (err) {
                    console.error('Track list load failed:', err);
                    $('pe-tracks').innerHTML = '<div class="text-danger py-3">Error loading tracks.</div>';
                });
            }
            updateButtons();
        }).catch(function (err) {
            console.error('Playlist list load failed:', err);
        });
    }

    function deletePlaylist() {
        if (!currentPlaylistId) return;
        musicbuddyConfirm('Delete playlist "' + currentPlaylistName + '" and all its tracks?', {
            title: 'Delete playlist', confirmText: 'Delete', variant: 'danger'
        }).then(function (ok) {
            if (!ok) return;
            apiFetch('/Playlists?handler=Delete&id=' + currentPlaylistId + '&type=' + encodeURIComponent(fileType), {
                method: 'POST',
                headers: { 'RequestVerificationToken': document.querySelector('input[name="__RequestVerificationToken"]')?.value || '' }
            }).then(function () {
                currentPlaylistId = null;
                currentPlaylistName = '';
                refreshPlaylistSelect();
                $('pe-tracks').innerHTML = '<div class="text-center text-muted py-4 small"><i class="bi bi-arrow-right-circle me-2"></i>Select or create a playlist</div>';
                flashSaved();
            }).catch(function (err) { toast(err.message, true); });
        });
    }

    function clearPlaylist() {
        if (!currentPlaylistId) return;
        musicbuddyConfirm('Remove all tracks from this playlist?', {
            title: 'Clear playlist', confirmText: 'Clear', variant: 'danger'
        }).then(function (ok) {
            if (!ok) return;
            apiFetch('/Playlists?handler=Clear&id=' + currentPlaylistId, {
                method: 'POST',
                headers: { 'RequestVerificationToken': document.querySelector('input[name="__RequestVerificationToken"]')?.value || '' }
            }).then(function () {
                htmx.ajax('GET', '/Playlists?handler=TrackList&playlistId=' + currentPlaylistId, {
                    target: '#pe-tracks', swap: 'innerHTML'
                }).catch(function (err) {
                    console.error('Track list reload failed:', err);
                });
                flashSaved();
            }).catch(function (err) { toast(err.message, true); });
        });
    }

    function removeTrack(trackId) {
        if (!currentPlaylistId || pendingRemoves.has(trackId)) return;
        pendingRemoves.add(trackId);
        apiFetch('/Playlists?handler=RemoveTrack&playlistId=' + currentPlaylistId + '&trackId=' + trackId, {
            method: 'POST',
            headers: { 'RequestVerificationToken': document.querySelector('input[name="__RequestVerificationToken"]')?.value || '' }
        }).then(function () {
            pendingRemoves.delete(trackId);
            htmx.ajax('GET', '/Playlists?handler=TrackList&playlistId=' + currentPlaylistId, {
                target: '#pe-tracks', swap: 'innerHTML'
            }).catch(function (err) {
                console.error('Track list reload failed:', err);
            });
            flashSaved();
        }).catch(function (err) {
            pendingRemoves.delete(trackId);
            toast(err.message, true);
        });
    }

    function addSelectedToPlaylist() {
        if (!currentPlaylistId || selection.length === 0) {
            toast('Select or create a playlist first.', true);
            return;
        }
        var files = selection.filter(function (s) { return s.kind === 'file'; }).map(function (s) {
            return { filePath: s.path, fileName: s.name, size: s.size || 0, channelCount: s.channelCount || 2 };
        });
        var albums = selection.filter(function (s) { return s.kind === 'album'; });

        if (albums.length === 0) {
            if (files.length === 0) return;
            postTracks(files);
            return;
        }

        toast('Gathering files from ' + albums.length + ' album' + (albums.length === 1 ? '' : 's') + '...');
        var collects = albums.map(function (a) {
            return apiFetch('/Playlists?handler=CollectFiles&type=' + encodeURIComponent(fileType) + '&path=' + encodeURIComponent(a.path), { method: 'POST', headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': document.querySelector('input[name="__RequestVerificationToken"]')?.value || '' } })
                .then(function (entries) { return entries || []; });
        });
        Promise.all(collects).then(function (results) {
            var seen = {};
            files.forEach(function (t) { seen[t.filePath] = true; });
            var albumFiles = [];
            results.forEach(function (entries) {
                entries.forEach(function (e) {
                    if (!seen[e.path]) {
                        seen[e.path] = true;
                        albumFiles.push({ filePath: e.path, fileName: e.name, size: e.size, channelCount: e.channelCount || 2 });
                    }
                });
            });
            var all = files.concat(albumFiles);
            if (all.length === 0) { toast('No new tracks found in that folder.'); return; }
            postTracks(all);
        }).catch(function (err) { toast(err.message, true); });
    }

    function postTracks(files, insertIndex, previousIds) {
        apiFetch('/Playlists?handler=AddTracks&playlistId=' + currentPlaylistId, {
            method: 'POST',
            body: JSON.stringify(files),
            headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': document.querySelector('input[name="__RequestVerificationToken"]')?.value || '' }
        }).then(function () {
            htmx.ajax('GET', '/Playlists?handler=TrackList&playlistId=' + currentPlaylistId, {
                target: '#pe-tracks', swap: 'innerHTML'
            }).then(function () {
                if (typeof insertIndex === 'number' && previousIds && previousIds.length > 0) {
                    var rows = Array.from($('pe-tracks').querySelectorAll('.pe-track-row'));
                    var newRows = rows.filter(function (r) { return previousIds.indexOf(parseInt(r.dataset.trackId)) === -1; });
                    newRows.forEach(function (r) {
                        var target = rows[insertIndex] || null;
                        if (target) $('pe-tracks').insertBefore(r, target);
                        else $('pe-tracks').appendChild(r);
                    });
                    var allRows = $('pe-tracks').querySelectorAll('.pe-track-row');
                    allRows.forEach(function (r, i) {
                        var numEl = r.querySelector('.pe-track-num');
                        if (numEl) numEl.textContent = (i + 1) + '.';
                    });
                    saveReorder();
                }
            }).catch(function (err) {
                console.error('Track list reload failed:', err);
            });
            flashSaved();
            selection = [];
            document.querySelectorAll('.pe-library-row .pe-check').forEach(function (cb) { cb.checked = false; });
            $('pe-selected-count').textContent = '0 selected';
            $('pe-add-selected-btn').disabled = true;
        }).catch(function (err) { toast(err.message, true); });
    }

    function saveReorder() {
        var rows = Array.from($('pe-tracks').querySelectorAll('.pe-track-row'));
        var order = rows.map(function (el, i) {
            return { id: parseInt(el.dataset.trackId), sortOrder: i + 1 };
        });
        apiFetch('/Playlists?handler=Reorder&playlistId=' + currentPlaylistId, {
            method: 'POST',
            body: JSON.stringify(order),
            headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': document.querySelector('input[name="__RequestVerificationToken"]')?.value || '' }
        }).then(function () { flashSaved(); })
          .catch(function (err) { toast(err.message, true); });
    }

    // --- Selection ---
    function selectAllVisible() {
        $('pe-library').querySelectorAll('.pe-library-row').forEach(function (row) {
            var cb = row.querySelector('.pe-check');
            if (cb) cb.checked = true;
            syncRowSelection(row);
        });
    }

    function deselectAll() {
        selection = [];
        $('pe-library').querySelectorAll('.pe-check').forEach(function (cb) { cb.checked = false; });
        $('pe-selected-count').textContent = '0 selected';
        $('pe-add-selected-btn').disabled = true;
    }

    function syncRowSelection(row) {
        var cb = row.querySelector('.pe-check');
        var item = { kind: row.dataset.kind, path: row.dataset.path, name: row.dataset.name, size: parseInt(row.dataset.size) || 0, channelCount: parseInt(row.dataset.channelCount) || 2 };
        var idx = selection.findIndex(function (s) { return s.kind === item.kind && s.path === item.path; });
        if (cb.checked && idx < 0) selection.push(item);
        else if (!cb.checked && idx >= 0) selection.splice(idx, 1);
        $('pe-selected-count').textContent = selection.length + ' selected';
        $('pe-add-selected-btn').disabled = selection.length === 0 || !currentPlaylistId;
    }

    // --- Event bindings ---
    // Playlist select
    $('pe-playlist-select').addEventListener('change', function () {
        currentPlaylistId = this.value ? parseInt(this.value) : null;
        var opt = this.options[this.selectedIndex];
        currentPlaylistName = opt ? opt.textContent : '';
        updateButtons();
        if (currentPlaylistId) {
            htmx.ajax('GET', '/Playlists?handler=TrackList&playlistId=' + currentPlaylistId, {
                target: '#pe-tracks', swap: 'innerHTML'
            }).catch(function (err) {
                console.error('Track list load failed:', err);
                $('pe-tracks').innerHTML = '<div class="text-danger py-3">Error loading tracks.</div>';
            });
        } else {
            $('pe-tracks').innerHTML = '<div class="text-center text-muted py-4 small"><i class="bi bi-arrow-right-circle me-2"></i>Select or create a playlist</div>';
        }
    });

    // Playlist CRUD buttons
    $('pe-create-btn').addEventListener('click', function () { focusNameMode('create'); });
    $('pe-rename-btn').addEventListener('click', function () { if (currentPlaylistId) focusNameMode('rename'); });
    $('pe-delete-btn').addEventListener('click', deletePlaylist);
    $('pe-name-save').addEventListener('click', saveName);
    $('pe-name-cancel').addEventListener('click', resetNameMode);
    $('pe-name-input').addEventListener('keydown', function (e) {
        if (e.key === 'Enter') { e.preventDefault(); saveName(); }
        else if (e.key === 'Escape') { e.preventDefault(); resetNameMode(); }
    });

    var clearBtn = $('pe-clear-btn');
    if (clearBtn) clearBtn.addEventListener('click', clearPlaylist);

    // Back button
    var backBtn = $('pe-back-btn');
    if (backBtn) backBtn.addEventListener('click', function () {
        var referrer = document.referrer;
        if (referrer && referrer.indexOf(window.location.origin) === 0) {
            window.location.href = referrer;
        } else {
            window.location.href = '/';
        }
    });

    // Selection buttons
    $('pe-select-all-btn').addEventListener('click', selectAllVisible);
    $('pe-deselect-all-btn').addEventListener('click', deselectAll);
    $('pe-add-selected-btn').addEventListener('click', addSelectedToPlaylist);

    // Library mode toggle
    var browseRadio = $('pe-mode-browse');
    var searchRadio = $('pe-mode-search');
    if (browseRadio) browseRadio.addEventListener('change', function () { if (this.checked) setLibraryMode('browse'); });
    if (searchRadio) searchRadio.addEventListener('change', function () { if (this.checked) setLibraryMode('search'); });

    // Search
    var searchBtn = $('pe-search-btn');
    var searchInputs = ['pe-search-song', 'pe-search-artist', 'pe-search-genre'].map(function (id) { return $(id); }).filter(Boolean);
    if (searchBtn) searchBtn.addEventListener('click', function () { doSearch(); });
    searchInputs.forEach(function (input) {
        input.addEventListener('keydown', function (e) {
            if (e.key === 'Enter') { e.preventDefault(); doSearch(); }
        });
    });

    // Library click handling
    $('pe-library').addEventListener('click', function (e) {
        var sortCol = e.target.closest('.pe-sort-col');
        if (sortCol) {
            e.preventDefault();
            sortSearchResults(sortCol.getAttribute('data-sort-field'));
            return;
        }
        var playBtn = e.target.closest('.pe-play-btn');
        if (playBtn) {
            e.stopPropagation();
            var path = playBtn.dataset.path;
            var files = [];
            $('pe-library').querySelectorAll('.pe-library-row[data-kind="file"]').forEach(function (r) { files.push(r.dataset.path); });
            playTrack(path, files);
            return;
        }
        var row = e.target.closest('.pe-library-row');
        if (!row) return;
        if (e.target.closest('.form-check-input')) return;
        if (row.dataset.kind === 'album') {
            navigateTo(row.dataset.path);
        }
    });

    $('pe-library').addEventListener('change', function (e) {
        if (e.target.matches('.pe-check')) {
            syncRowSelection(e.target.closest('.pe-library-row'));
        }
    });

    // Track list click handling
    $('pe-tracks').addEventListener('click', function (e) {
        var playBtn = e.target.closest('.pe-play-btn');
        if (playBtn) {
            e.stopPropagation();
            var row = playBtn.closest('.pe-track-row');
            if (row) {
                var paths = [];
                $('pe-tracks').querySelectorAll('.pe-track-row').forEach(function (r) { paths.push(r.dataset.path); });
                playTrack(row.dataset.path, paths);
            }
            return;
        }
        var removeBtn = e.target.closest('.pe-remove-btn');
        if (removeBtn) {
            removeTrack(parseInt(removeBtn.dataset.trackId));
        }
    });

    // Drag and drop for reorder
    $('pe-tracks').addEventListener('dragstart', function (e) {
        var row = e.target.closest('.pe-track-row');
        if (!row) { e.preventDefault(); return; }
        dragSource = 'reorder';
        dragPayload = parseInt(row.dataset.trackId);
        e.dataTransfer.effectAllowed = 'move';
        e.dataTransfer.setData('text/plain', 'reorder');
        row.classList.add('pe-dragging');
    });

    $('pe-tracks').addEventListener('dragend', function () {
        $('pe-tracks').querySelectorAll('.pe-track-row').forEach(function (r) { r.classList.remove('pe-dragging'); });
        if (dropRowEl) dropRowEl.classList.remove('pe-indicator-top', 'pe-indicator-bottom');
        dropRowEl = null;
        dropIndex = -1;
        dragSource = null;
    });

    $('pe-tracks').addEventListener('dragover', function (e) {
        if (dragSource === null) return;
        e.preventDefault();
        e.dataTransfer.dropEffect = dragSource === 'reorder' ? 'move' : 'copy';
        var rows = Array.from($('pe-tracks').querySelectorAll('.pe-track-row'));
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
    });

    $('pe-tracks').addEventListener('drop', function (e) {
        e.preventDefault();
        if (dragSource === null) return;
        if (dropRowEl) dropRowEl.classList.remove('pe-indicator-top', 'pe-indicator-bottom');
        dropRowEl = null;

        if (dragSource === 'reorder') {
            var index = dropIndex;
            var draggedId = dragPayload;
            var rows = Array.from($('pe-tracks').querySelectorAll('.pe-track-row')).filter(function (el) {
                return parseInt(el.dataset.trackId) !== draggedId;
            });
            var dragged = $('pe-tracks').querySelector('.pe-track-row[data-track-id="' + draggedId + '"]');
            if (dragged) {
                var target = rows[index] || null;
                if (target) $('pe-tracks').insertBefore(dragged, target);
                else $('pe-tracks').appendChild(dragged);
                saveReorder();
            }
        } else if (dragSource === 'library') {
            if (!currentPlaylistId) { toast('Select or create a playlist first.', true); dragSource = null; dragPayload = null; return; }
            var items = dragPayload || [];
            var files = items.filter(function (s) { return s.kind === 'file'; }).map(function (s) {
                return { filePath: s.path, fileName: s.name, size: s.size || 0, channelCount: s.channelCount || 2 };
            });
            var albums = items.filter(function (s) { return s.kind === 'album'; });
            var insertAt = dropIndex;
            var prevIds = Array.from($('pe-tracks').querySelectorAll('.pe-track-row')).map(function (r) { return parseInt(r.dataset.trackId); });
            if (albums.length === 0) {
                if (files.length === 0) { dragSource = null; dragPayload = null; return; }
                postTracks(files, insertAt, prevIds);
            } else {
                toast('Gathering files from ' + albums.length + ' album' + (albums.length === 1 ? '' : 's') + '...');
                var collects = albums.map(function (a) {
                    return apiFetch('/Playlists?handler=CollectFiles&type=' + encodeURIComponent(fileType) + '&path=' + encodeURIComponent(a.path), { method: 'POST', headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': document.querySelector('input[name="__RequestVerificationToken"]')?.value || '' } })
                        .then(function (entries) { return entries || []; });
                });
                Promise.all(collects).then(function (results) {
                    var seen = {};
                    files.forEach(function (t) { seen[t.filePath] = true; });
                    var albumFiles = [];
                    results.forEach(function (entries) {
                        entries.forEach(function (e) {
                            if (!seen[e.path]) {
                                seen[e.path] = true;
                                albumFiles.push({ filePath: e.path, fileName: e.name, size: e.size, channelCount: e.channelCount || 2 });
                            }
                        });
                    });
                    var all = files.concat(albumFiles);
                    if (all.length === 0) { toast('No new tracks found in that folder.'); return; }
                    postTracks(all, insertAt, prevIds);
                }).catch(function (err) { toast(err.message, true); });
            }
        }

        dragSource = null;
        dragPayload = null;
    });

    // Drag from library to tracks
    $('pe-library').addEventListener('dragstart', function (e) {
        var row = e.target.closest('.pe-library-row');
        if (!row) { e.preventDefault(); return; }
        var payload = selection.length > 0 ? selection.slice() : [{ kind: row.dataset.kind, path: row.dataset.path, name: row.dataset.name, size: parseInt(row.dataset.size) || 0, channelCount: parseInt(row.dataset.channelCount) || 2 }];
        if (payload.length === 0) { e.preventDefault(); return; }
        dragSource = 'library';
        dragPayload = payload;
        e.dataTransfer.effectAllowed = 'copy';
        e.dataTransfer.setData('text/plain', payload.length + ' item(s)');
        row.classList.add('pe-dragging');
    });

    $('pe-library').addEventListener('dragend', function () {
        var row = $('pe-library').querySelector('.pe-dragging');
        if (row) row.classList.remove('pe-dragging');
        dragSource = null;
        dragPayload = null;
    });

    $('pe-tracks').addEventListener('dragleave', function (e) {
        if (!$('pe-tracks').contains(e.relatedTarget)) {
            if (dropRowEl) dropRowEl.classList.remove('pe-indicator-top', 'pe-indicator-bottom');
            dropRowEl = null;
        }
    });

    // Transport controls
    $('pe-t-prev').addEventListener('click', playPrev);
    $('pe-t-next').addEventListener('click', playNext);
    $('pe-t-play').addEventListener('click', togglePlayPause);

    $('pe-t-seek').addEventListener('mousedown', function () { seekDragging = true; });
    $('pe-t-seek').addEventListener('touchstart', function () { seekDragging = true; });
    document.addEventListener('mouseup', function () { seekDragging = false; });
    document.addEventListener('touchend', function () { seekDragging = false; });
    $('pe-t-seek').addEventListener('input', function () {
        if (fileType !== 'mp3') return;
        var dur = audio.duration || 0;
        if (dur > 0) audio.currentTime = (this.value / 1000) * dur;
    });

    // HTMX callbacks
    document.addEventListener('htmx:afterSwap', function (e) {
        if (e.detail.target.id === 'pe-library') {
            renderBreadcrumb(currentLibPath);
        }
        if (e.detail.target.id === 'pe-tracks') {
            updateTrackCount();
        }
        if (e.detail.target.id === 'pe-playlist-select') {
            var saved = (typeof resumeCookie !== 'undefined') ? resumeCookie.loadResume(fileType) : null;
            if (saved && saved.playlistId) {
                var sel = $('pe-playlist-select');
                var match = Array.from(sel.options).find(function (o) { return o.value == saved.playlistId; });
                if (match) {
                    sel.value = saved.playlistId;
                    sel.dispatchEvent(new Event('change'));
                }
            }
        }
    });

    document.addEventListener('htmx:responseError', function (e) {
        console.error('HTMX response error:', e.detail.xhr.status, e.detail.requestConfig.path);
        var target = e.detail.target;
        if (target) {
            target.innerHTML = '<div class="text-danger py-3"><i class="bi bi-exclamation-triangle me-2"></i>Server error (' + e.detail.xhr.status + ').</div>';
        }
    });

    document.addEventListener('htmx:sendError', function (e) {
        console.error('HTMX send error:', e.detail.requestConfig.path);
        var target = e.detail.target;
        if (target) {
            target.innerHTML = '<div class="text-danger py-3"><i class="bi bi-exclamation-triangle me-2"></i>Network error — is the server running?</div>';
        }
    });

    // Playback stop on navigation
    window.addEventListener('beforeunload', stopPlayback);
    window.addEventListener('pagehide', stopPlayback);
    window.addEventListener('popstate', stopPlayback);

    // Initialize
    var transportEl = $('pe-transport');
    if (transportEl) transportEl.style.display = '';
})();
