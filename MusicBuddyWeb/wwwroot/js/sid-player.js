var sidPlayer = (function () {
    var player = null;
    var playlist = [];
    var currentIndex = -1;
    var playing = false;
    var subtune = 0;
    var updateInterval = null;
    var preferredModel = null;

    function formatTime(sec) {
        var m = Math.floor(sec / 60);
        var s = Math.floor(sec % 60);
        return m + ':' + (s < 10 ? '0' : '') + s;
    }

    function renderPlaylist() {
        var el = document.getElementById('sid-playlist');
        if (!el) return;
        el.innerHTML = '';
        document.getElementById('sid-count').textContent = playlist.length;
        playlist.forEach(function (f, i) {
            var item = document.createElement('button');
            item.className = 'list-group-item list-group-item-action' + (i === currentIndex ? ' active' : '');
            item.innerHTML = '<div class="d-flex justify-content-between align-items-center">' +
                '<span class="text-truncate me-2">' + escapeHtml(f.name) + '</span>' +
                '<small class="text-nowrap text-muted">' + formatSize(f.size) + '</small>' +
                '</div>';
            item.addEventListener('click', function () { loadTrack(i); });
            el.appendChild(item);
        });
    }

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

    function sanitizePetSCII(str) {
        var out = '';
        for (var i = 0; i < str.length; i++) {
            var c = str.charCodeAt(i);
            if (c >= 32 && c <= 126) out += str[i];
        }
        return out.trim();
    }

    function loadTrack(index) {
        if (index < 0 || index >= playlist.length) return;
        currentIndex = index;
        renderPlaylist();
        playFile(playlist[index]);
    }

    function playFile(file) {
        subtune = 0;
        document.getElementById('sid-title').textContent = file.name;
        document.getElementById('sid-author').textContent = '';
        document.getElementById('sid-info').textContent = 'Loading...';
        document.getElementById('sid-time').textContent = '0:00';
        document.getElementById('sid-duration').textContent = '';
        document.getElementById('sid-seek-fill').style.width = '0%';
        updateSubtuneUI();
        player.loadstart(file.path, subtune);
    }

    function updateSubtuneUI() {
        if (!player) return;
        var total = player.getsubtunes();
        document.getElementById('sid-subtune').textContent = (subtune + 1) + '/' + total;
        document.getElementById('sid-sub-down').disabled = subtune <= 0;
        document.getElementById('sid-sub-up').disabled = subtune >= total - 1;
    }

    function setAutoButtonState() {
        var btn = document.getElementById('sid-model-auto');
        if (!btn) return;
        btn.className = 'btn btn-sm ' + (preferredModel === null ? 'btn-primary' : 'btn-outline-secondary');
    }

    function syncModelUI() {
        if (!player) return;
        if (preferredModel === null) {
            player.setmodel(player.getprefmodel());
        }
        var active = player.getmodel();
        document.getElementById('sid-model-6581').checked = active === 6581;
        document.getElementById('sid-model-8580').checked = active !== 6581;
        setAutoButtonState();
    }

    function showPlayState() {
        document.getElementById('sid-play').style.display = 'none';
        document.getElementById('sid-pause').style.display = '';
        playing = true;
    }

    function showPauseState() {
        document.getElementById('sid-play').style.display = '';
        document.getElementById('sid-pause').style.display = 'none';
        playing = false;
    }

    function normalizeTrack(track) {
        return {
            name: track.name || track.fileName || '',
            path: track.path || track.filePath || '',
            size: track.size || track.fileSize || 0
        };
    }

    return {
        init: function (files) {
            playlist = files;
            player = new jsSID(16384, 0.0005);
            player.setloadcallback(function () {
                var author = sanitizePetSCII(player.getauthor());
                var info = sanitizePetSCII(player.getinfo());
                document.getElementById('sid-author').textContent =
                    author ? author + ' — ' + info : info;
                document.getElementById('sid-info').textContent = '';
                updateSubtuneUI();
                syncModelUI();
            });
            player.setstartcallback(function () {
                showPlayState();
                if (player.getplaytime() > 0) {
                    document.getElementById('sid-duration').textContent = formatTime(player.getplaytime());
                }
            });
            player.setendcallback(function () {
                var next = currentIndex + 1;
                if (next >= playlist.length) next = 0;
                loadTrack(next);
            }, 0);

            renderPlaylist();

            var rmsData = new Float32Array(1024);
            function getRmsLevel() {
                if (!player || !player.analyser) return 0;
                player.analyser.getFloatTimeDomainData(rmsData);
                var sum = 0;
                for (var i = 0; i < rmsData.length; i++) sum += rmsData[i] * rmsData[i];
                var rms = Math.sqrt(sum / rmsData.length);
                return Math.min(1, rms);
            }

            updateInterval = setInterval(function () {
                if (playing && player) {
                    document.getElementById('sid-time').textContent = formatTime(player.getplaytime());
                    var len = player.getplaytime();
                    var progress = len > 0 ? (player.getplaytime() / len * 100) : 0;
                    document.getElementById('sid-seek-fill').style.width = Math.min(progress, 100) + '%';
                    var level = getRmsLevel();
                    vuMeter.setLevels(level, level);
                } else {
                    vuMeter.setLevels(0, 0);
                }
            }, 50);

            document.getElementById('sid-play').addEventListener('click', function () {
                if (currentIndex < 0 && playlist.length > 0) {
                    loadTrack(0);
                } else if (player) {
                    player.playcont();
                    showPlayState();
                }
            });
            document.getElementById('sid-pause').addEventListener('click', function () {
                if (player) { player.pause(); showPauseState(); }
            });
            document.getElementById('sid-stop').addEventListener('click', function () {
                if (player) { player.stop(); showPauseState(); }
            });
            document.getElementById('sid-prev').addEventListener('click', function () {
                var prev = currentIndex - 1;
                if (prev < 0) prev = playlist.length - 1;
                loadTrack(prev);
            });
            document.getElementById('sid-next').addEventListener('click', function () {
                var next = currentIndex + 1;
                if (next >= playlist.length) next = 0;
                loadTrack(next);
            });
            document.getElementById('sid-volume').addEventListener('input', function () {
                if (player) player.setvolume(this.value / 100);
            });
            document.getElementById('sid-sub-down').addEventListener('click', function () {
                if (subtune > 0) {
                    subtune--;
                    player.start(subtune);
                    showPlayState();
                    updateSubtuneUI();
                }
            });
            document.getElementById('sid-sub-up').addEventListener('click', function () {
                if (player && subtune < player.getsubtunes() - 1) {
                    subtune++;
                    player.start(subtune);
                    showPlayState();
                    updateSubtuneUI();
                }
            });
            document.querySelectorAll('input[name="sid-model"]').forEach(function (el) {
                el.addEventListener('change', function () {
                    preferredModel = parseFloat(this.value);
                    if (player) player.setmodel(preferredModel);
                    setAutoButtonState();
                });
            });
            document.getElementById('sid-model-auto').addEventListener('click', function () {
                preferredModel = null;
                syncModelUI();
            });

            document.getElementById('sid-volume').dispatchEvent(new Event('input'));
        },

        loadPlaylist: function (tracks) {
            playlist = tracks.map(normalizeTrack);
            currentIndex = -1;
            renderPlaylist();
        },

        syncPlaylist: function (tracks) {
            var currentPath = (currentIndex >= 0 && playlist[currentIndex]) ? playlist[currentIndex].path : null;
            var normalized = tracks.map(normalizeTrack);
            var newIndex = -1;
            if (currentPath) {
                for (var i = 0; i < normalized.length; i++) {
                    if (normalized[i].path === currentPath) { newIndex = i; break; }
                }
            }
            playlist = normalized;
            currentIndex = newIndex;
            renderPlaylist();
        },

        playByPath: function (path) {
            for (var i = 0; i < playlist.length; i++) {
                if (playlist[i].path === path) { loadTrack(i); return; }
            }
            playFile({ name: path.split('/').pop(), path: path, size: 0 });
        },

        getTransportState: function () {
            var t = document.getElementById('sid-time');
            var d = document.getElementById('sid-duration');
            return {
                title: (currentIndex >= 0 && playlist[currentIndex]) ? playlist[currentIndex].name : '',
                duration: d ? d.textContent : '',
                currentTime: t ? t.textContent : '',
                playing: playing,
                seekable: false
            };
        },

        playPause: function () {
            if (!player) return;
            if (playing) { player.pause(); showPauseState(); }
            else { player.playcont(); showPlayState(); }
        },

        prev: function () {
            var prev = currentIndex - 1;
            if (prev < 0) prev = playlist.length - 1;
            loadTrack(prev);
        },

        next: function () {
            var next = currentIndex + 1;
            if (next >= playlist.length) next = 0;
            loadTrack(next);
        }
    };
})();
