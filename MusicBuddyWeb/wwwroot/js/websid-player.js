var websidPlayer = (function () {
    var playlist = [];
    var currentIndex = -1;
    var playing = false;
    var initialized = false;
    var player = null;
    var backend = null;
    var preferredModel = null;
    var updateInterval = null;
    var onTrackEnded = null;
    var onTrackChanged = null;

    function formatTime(sec) {
        var m = Math.floor(sec / 60);
        var s = Math.floor(sec % 60);
        return m + ':' + (s < 10 ? '0' : '') + s;
    }

    function renderPlaylist() {
        var el = document.getElementById('websid-playlist');
        if (!el) return;
        el.innerHTML = '';
        document.getElementById('websid-count').textContent = playlist.length;
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

    function updateSubtuneUI(info) {
        if (!info) return;
        var total = info.maxSubsong || 1;
        var current = (info.actualSubsong || 0) + 1;
        document.getElementById('websid-subtune').textContent = current + '/' + total;
        document.getElementById('websid-sub-down').disabled = current <= 1;
        document.getElementById('websid-sub-up').disabled = current >= total;
    }

    function setAutoButtonState() {
        var btn = document.getElementById('websid-model-auto');
        if (!btn) return;
        btn.className = 'btn btn-sm ' + (preferredModel === null ? 'btn-primary' : 'btn-outline-secondary');
    }

    function applyModel() {
        if (!backend || preferredModel === null) return;
        backend.setSID6581(preferredModel === 6581 ? 1 : 0);
    }

    function syncModelUI() {
        if (!backend) return;
        applyModel();
        var is6581 = backend.isSID6581();
        document.getElementById('websid-model-6581').checked = !!is6581;
        document.getElementById('websid-model-8580').checked = !is6581;
        setAutoButtonState();
    }

    function showPlayState() {
        document.getElementById('websid-play').style.display = 'none';
        document.getElementById('websid-pause').style.display = '';
        playing = true;
    }

    function showPauseState() {
        document.getElementById('websid-play').style.display = '';
        document.getElementById('websid-pause').style.display = 'none';
        playing = false;
    }

    function normalizeTrack(track) {
        return {
            name: track.name || track.fileName || '',
            path: track.path || track.filePath || '',
            size: track.size || track.fileSize || 0
        };
    }

    function initBackend() {
        return new Promise(function (resolve) {
            if (initialized) { resolve(); return; }

            backend = new SIDBackendAdapter();
            ScriptNodePlayer.initialize(backend, function () {
                if (onTrackEnded && onTrackEnded()) return;
                var next = currentIndex + 1;
                if (next >= playlist.length) next = 0;
                loadTrack(next);
            }, undefined, true).then(function () {
                initialized = true;
                player = ScriptNodePlayer.getInstance();
                resolve();
            });
        });
    }

    function loadTrack(index) {
        if (index < 0 || index >= playlist.length) return;
        currentIndex = index;
        renderPlaylist();
        playFile(playlist[index]);
    }

    function playFile(file) {
        if (onTrackChanged) onTrackChanged(file);
        document.getElementById('websid-title').textContent = file.name;
        document.getElementById('websid-author').textContent = '';
        document.getElementById('websid-info').textContent = 'Loading...';
        document.getElementById('websid-time').textContent = '0:00';
        document.getElementById('websid-duration').textContent = '';
        document.getElementById('websid-seek-fill').style.width = '0%';

        var options = {};
        options.track = -1;
        options.timeout = -1;
        options.traceSID = false;

        function doLoad() {
            ScriptNodePlayer.loadMusicFromURL(file.path, options,
                function () { document.getElementById('websid-info').textContent = 'Error loading file'; },
                function () {}
            ).then(function () {
                var info = player.getSongInfo();
                document.getElementById('websid-title').textContent = info.songName || file.name;
                document.getElementById('websid-author').textContent = info.songAuthor || '';
                document.getElementById('websid-info').textContent = info.songReleased || '';
                updateSubtuneUI(info);
                syncModelUI();
                showPlayState();
            });
        }

        if (!initialized) {
            initBackend().then(doLoad);
        } else {
            doLoad();
        }
    }

    return {
        init: function (files) {
            playlist = files;
            renderPlaylist();

            document.getElementById('websid-play').addEventListener('click', function () {
                if (currentIndex < 0 && playlist.length > 0) {
                    loadTrack(0);
                } else if (player) {
                    player.resume();
                    showPlayState();
                } else {
                    loadTrack(currentIndex >= 0 ? currentIndex : 0);
                }
            });

            document.getElementById('websid-pause').addEventListener('click', function () {
                if (player) { player.pause(); showPauseState(); }
            });

            document.getElementById('websid-stop').addEventListener('click', function () {
                if (player) { player.pause(); showPauseState(); }
            });

            document.getElementById('websid-prev').addEventListener('click', function () {
                var prev = currentIndex - 1;
                if (prev < 0) prev = playlist.length - 1;
                loadTrack(prev);
            });

            document.getElementById('websid-next').addEventListener('click', function () {
                var next = currentIndex + 1;
                if (next >= playlist.length) next = 0;
                loadTrack(next);
            });

            document.getElementById('websid-volume').addEventListener('input', function () {
                if (player) player.setVolume(this.value / 100);
            });

            document.getElementById('websid-sub-down').addEventListener('click', function () {
                if (!player) return;
                var info = player.getSongInfo();
                if (!info) return;
                var current = info.actualSubsong || 0;
                if (current > 0) {
                    ScriptNodePlayer.loadMusicFromURL(playlist[currentIndex].path,
                        { track: current - 1, timeout: -1, traceSID: false },
                        function () {},
                        function () {}
                    ).then(function () {
                        var newInfo = player.getSongInfo();
                        updateSubtuneUI(newInfo);
                        syncModelUI();
                        showPlayState();
                    });
                }
            });

            document.getElementById('websid-sub-up').addEventListener('click', function () {
                if (!player) return;
                var info = player.getSongInfo();
                if (!info) return;
                var current = info.actualSubsong || 0;
        var total = info.maxSubsong || 1;
                if (current < total - 1) {
                    ScriptNodePlayer.loadMusicFromURL(playlist[currentIndex].path,
                        { track: current + 1, timeout: -1, traceSID: false },
                        function () {},
                        function () {}
                    ).then(function () {
                        var newInfo = player.getSongInfo();
                        updateSubtuneUI(newInfo);
                        syncModelUI();
                        showPlayState();
                    });
                }
            });

            document.querySelectorAll('input[name="websid-model"]').forEach(function (el) {
                el.addEventListener('change', function () {
                    preferredModel = parseFloat(this.value);
                    applyModel();
                    setAutoButtonState();
                });
            });
            document.getElementById('websid-model-auto').addEventListener('click', function () {
                preferredModel = null;
                setAutoButtonState();
                if (!player || currentIndex < 0) return;
                var info = player.getSongInfo();
                var track = (info && info.actualSubsong) ? info.actualSubsong : 0;
                ScriptNodePlayer.loadMusicFromURL(playlist[currentIndex].path,
                    { track: track, timeout: -1, traceSID: false },
                    function () {},
                    function () {}
                ).then(function () {
                    var newInfo = player.getSongInfo();
                    updateSubtuneUI(newInfo);
                    syncModelUI();
                    showPlayState();
                });
            });

            document.getElementById('websid-volume').dispatchEvent(new Event('input'));

            var rmsData = new Float32Array(1024);
            function getRmsLevel() {
                if (!player || !player._analyzerNode) return 0;
                player._analyzerNode.getFloatTimeDomainData(rmsData);
                var sum = 0;
                for (var i = 0; i < rmsData.length; i++) sum += rmsData[i] * rmsData[i];
                var rms = Math.sqrt(sum / rmsData.length);
                return Math.min(1, rms);
            }

            updateInterval = setInterval(function () {
                if (playing && player) {
                    var time = player.getCurrentPlaytime();
                    document.getElementById('websid-time').textContent = formatTime(time);
                    vuMeter.setLevels(getRmsLevel(), getRmsLevel());
                } else {
                    vuMeter.setLevels(0, 0);
                }
            }, 16);
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

        selectByPath: function (path) {
            for (var i = 0; i < playlist.length; i++) {
                if (playlist[i].path === path) {
                    currentIndex = i;
                    renderPlaylist();
                    document.getElementById('websid-title').textContent = playlist[i].name;
                    document.getElementById('websid-author').textContent = '';
                    document.getElementById('websid-info').textContent = '';
                    document.getElementById('websid-time').textContent = '0:00';
                    document.getElementById('websid-duration').textContent = '';
                    document.getElementById('websid-seek-fill').style.width = '0%';
                    return;
                }
            }
        },

        setOnTrackEnded: function (fn) {
            onTrackEnded = fn;
        },

        setOnTrackChanged: function (fn) {
            onTrackChanged = fn;
        },

        getTransportState: function () {
            var t = document.getElementById('websid-time');
            var d = document.getElementById('websid-duration');
            return {
                title: (currentIndex >= 0 && playlist[currentIndex]) ? playlist[currentIndex].name : '',
                duration: d ? d.textContent : '',
                currentTime: t ? t.textContent : '',
                playing: playing,
                seekable: false
            };
        },

        playPause: function () {
            if (!player) {
                if (playlist.length > 0) loadTrack(currentIndex >= 0 ? currentIndex : 0);
                return;
            }
            if (playing) { player.pause(); showPauseState(); }
            else { player.resume(); showPlayState(); }
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
