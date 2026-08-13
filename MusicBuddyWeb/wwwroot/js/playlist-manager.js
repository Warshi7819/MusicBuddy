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

    function loadPlaylistTracks() {
        if (!currentPlaylistId) {
            if (onPlaylistLoaded) onPlaylistLoaded(null);
            return Promise.resolve();
        }
        return apiFetch(apiBase + '/' + currentPlaylistId)
            .then(function (data) {
                if (onPlaylistLoaded) onPlaylistLoaded(data);
            });
    }

    function refreshPlaylistList() {
        return apiFetch(apiBase + '?type=' + currentFileType).then(function (playlists) {
            renderPlaylistDropdown(playlists);
            var saved = resumeCookie.loadResume(currentFileType);
            if (saved && saved.playlistId) {
                var id = saved.playlistId;
                var exists = playlists.some(function (p) { return p.id === id; });
                if (exists) {
                    currentPlaylistId = id;
                    var select = document.getElementById('pm-playlist-select');
                    if (select) select.value = id;
                }
            }
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
                    resumeCookie.saveResume(currentFileType, currentPlaylistId);
                    loadPlaylistTracks();
                });
            }

            return refreshPlaylistList().then(function () {
                return loadPlaylistTracks();
            });
        },

        loadPlaylists: function () {
            return refreshPlaylistList();
        },

        selectPlaylist: function (id) {
            currentPlaylistId = id;
            return refreshPlaylistList().then(function () {
                var select = document.getElementById('pm-playlist-select');
                if (select) select.value = id || '';
                return loadPlaylistTracks();
            });
        },

        reload: function () {
            return loadPlaylistTracks();
        },

        getCurrentPlaylistId: function () {
            return currentPlaylistId;
        },

        getCurrentFileType: function () {
            return currentFileType;
        },

        addTracksToPlaylist: function (playlistId, tracks) {
            return apiFetch(apiBase + '/' + playlistId + '/tracks', {
                method: 'POST',
                body: JSON.stringify(tracks)
            });
        },

        getTracks: function () {
            if (!currentPlaylistId) return Promise.resolve([]);
            return apiFetch(apiBase + '/' + currentPlaylistId).then(function (data) {
                return data.tracks || [];
            });
        }
    };
})();