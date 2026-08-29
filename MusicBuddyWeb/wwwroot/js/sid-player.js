var sidPlayer = (function () {
    var player = null;
    var playlist = [];
    var currentIndex = -1;
    var playing = false;
    var subtune = 0;
    var updateInterval = null;
    var preferredModel = null;
    var onTrackEnded = null;
    var onTrackChangedCallbacks = [];
    var tuneLoaded = false;

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
            item.innerHTML = '<span class="text-truncate">' + escapeHtml(f.name) + '</span>';
            item.addEventListener('click', function () { loadTrack(i); });
            el.appendChild(item);
        });
    }

    function escapeHtml(s) {
        var d = document.createElement('div');
        d.appendChild(document.createTextNode(s));
        return d.innerHTML;
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
        playFile(playlist[index], index);
    }

    function playFile(file, index) {
        subtune = 0;
        tuneLoaded = true;
        if (onTrackChangedCallbacks.length) {
            for (var c = 0; c < onTrackChangedCallbacks.length; c++) onTrackChangedCallbacks[c](file, index);
        }
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

    function resumePlayback() {
        if (!player) return;
        player.playcont();
        showPlayState();
    }

    function normalizeTrack(track) {
        return {
            name: track.name || track.fileName || '',
            path: track.path || track.filePath || '',
            channelCount: track.channelCount || 2
        };
    }

    function createPlayer() {
        var p = new jsSID(16384, 0.0005);
        p.setloadcallback(function () {
            var author = sanitizePetSCII(p.getauthor());
            var info = sanitizePetSCII(p.getinfo());
            document.getElementById('sid-author').textContent =
                author ? author + ' — ' + info : info;
            document.getElementById('sid-info').textContent = '';
            updateSubtuneUI();
            syncModelUI();
        });
        p.setstartcallback(function () {
            showPlayState();
            if (p.getplaytime() > 0) {
                document.getElementById('sid-duration').textContent = formatTime(p.getplaytime());
            }
        });
        p.setendcallback(function () {
            if (onTrackEnded && onTrackEnded()) return;
            var next = currentIndex + 1;
            if (next >= playlist.length) next = 0;
            loadTrack(next);
        }, 0);
        return p;
    }

    return {
        init: function (files) {
            playlist = files;
            player = createPlayer();

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
            }, 16);

            document.getElementById('sid-play').addEventListener('click', function () {
                if (currentIndex >= 0 && !tuneLoaded) { loadTrack(currentIndex); return; }
                if (currentIndex < 0 && playlist.length > 0) {
                    loadTrack(0);
                } else if (player) {
                    resumePlayback();
                }
            });
            document.getElementById('sid-pause').addEventListener('click', function () {
                if (player) { player.pause(); showPauseState(); }
            });
            document.getElementById('sid-stop').addEventListener('click', function () {
                if (!player) { showPauseState(); return; }
                if (currentIndex < 0 && !tuneLoaded) { showPauseState(); return; }
                if (player.aCtx && typeof player.aCtx.close === 'function') {
                    try { player.aCtx.close(); } catch (e) {}
                }
                player = createPlayer();
                tuneLoaded = false;
                subtune = 0;
                showPauseState();
                document.getElementById('sid-time').textContent = '0:00';
                document.getElementById('sid-duration').textContent = '';
                document.getElementById('sid-seek-fill').style.width = '0%';
                document.getElementById('sid-subtune').textContent = '-/-';
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
            playFile({ name: path.split('/').pop(), path: path });
        },

        selectByPath: function (path) {
            for (var i = 0; i < playlist.length; i++) {
                if (playlist[i].path === path) {
                    currentIndex = i;
                    tuneLoaded = false;
                    renderPlaylist();
                    document.getElementById('sid-title').textContent = playlist[i].name;
                    document.getElementById('sid-author').textContent = '';
                    document.getElementById('sid-info').textContent = '';
                    document.getElementById('sid-time').textContent = '0:00';
                    document.getElementById('sid-duration').textContent = '';
                    document.getElementById('sid-seek-fill').style.width = '0%';
                    return;
                }
            }
        },

        setOnTrackEnded: function (fn) {
            onTrackEnded = fn;
        },

        setOnTrackChanged: function (fn) {
            if (onTrackChangedCallbacks.indexOf(fn) === -1) onTrackChangedCallbacks.push(fn);
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
            if (currentIndex >= 0 && !tuneLoaded) { loadTrack(currentIndex); return; }
            if (playing) { player.pause(); showPauseState(); }
            else { resumePlayback(); }
        },

        play: function () {
            if (!player) return;
            if (currentIndex < 0 && playlist.length > 0) { loadTrack(0); return; }
            if (currentIndex >= 0 && !tuneLoaded) { loadTrack(currentIndex); return; }
            if (!playing) { resumePlayback(); }
        },

        pause: function () {
            if (!player) return;
            if (playing) { player.pause(); showPauseState(); }
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
