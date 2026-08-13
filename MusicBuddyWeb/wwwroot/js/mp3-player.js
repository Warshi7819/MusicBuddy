var mp3Player = (function () {
    var audio = new Audio();
    var playlist = [];
    var currentIndex = -1;
    var playing = false;
    var audioCtx = null;
    var analyserL = null;
    var analyserR = null;
    var rmsDataL = null;
    var rmsDataR = null;
    var vuInterval = null;
    var source = null;
    var currentTrack = null;

    function formatTime(sec) {
        if (isNaN(sec)) return '0:00';
        var m = Math.floor(sec / 60);
        var s = Math.floor(sec % 60);
        return m + ':' + (s < 10 ? '0' : '') + s;
    }

    function renderPlaylist() {
        var el = document.getElementById('mp3-playlist');
        if (!el) return;
        el.innerHTML = '';
        document.getElementById('mp3-count').textContent = playlist.length;
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

    function loadTrack(index) {
        if (index < 0 || index >= playlist.length) return;
        currentIndex = index;
        renderPlaylist();
        playFile(playlist[index]);
    }

    function playFile(file) {
        currentTrack = file;
        document.getElementById('mp3-title').textContent = file.name;
        document.getElementById('mp3-artist').textContent = '';
        document.getElementById('mp3-time').textContent = '0:00';
        document.getElementById('mp3-duration').textContent = '0:00';
        document.getElementById('mp3-seek-fill').style.width = '0%';
        loadAlbumArt(file.path);
        var p = audioCtx.state === 'suspended' ? audioCtx.resume() : Promise.resolve();
        p.then(function () {
            audio.src = file.path;
            audio.load();
            audio.play().then(showPlayState).catch(function () {});
        });
    }

    function loadAlbumArt(filePath) {
        var img = document.getElementById('mp3-art-img');
        var placeholder = document.getElementById('mp3-art-placeholder');
        if (!img || !placeholder) return;

        img.style.display = 'none';
        placeholder.style.display = '';

        fetch('/api/albumart?path=' + encodeURIComponent(filePath))
            .then(function (res) {
                if (!res.ok || res.status === 204) return null;
                return res.blob();
            })
            .then(function (blob) {
                if (!blob) return;
                var url = URL.createObjectURL(blob);
                img.onload = function () {
                    placeholder.style.display = 'none';
                    img.style.display = '';
                };
                img.src = url;
            })
            .catch(function () {});
    }

    function normalizeTrack(track) {
        return {
            name: track.name || track.fileName || '',
            path: track.path || track.filePath || '',
            size: track.size || track.fileSize || 0,
            channelCount: track.channelCount || 2
        };
    }

    function showPlayState() {
        document.getElementById('mp3-play').style.display = 'none';
        document.getElementById('mp3-pause').style.display = '';
        playing = true;
    }

    function showPauseState() {
        document.getElementById('mp3-play').style.display = '';
        document.getElementById('mp3-pause').style.display = 'none';
        playing = false;
    }

    return {
        init: function (files) {
            playlist = files;

            audio.addEventListener('timeupdate', function () {
                document.getElementById('mp3-time').textContent = formatTime(audio.currentTime);
                var dur = audio.duration;
                var pct = dur ? (audio.currentTime / dur * 100) : 0;
                document.getElementById('mp3-seek-fill').style.width = pct + '%';
            });
            audio.addEventListener('loadedmetadata', function () {
                document.getElementById('mp3-duration').textContent = formatTime(audio.duration);
            });
            audio.addEventListener('ended', function () {
                var next = currentIndex + 1;
                if (next >= playlist.length) next = 0;
                loadTrack(next);
            });
            audio.addEventListener('play', showPlayState);
            audio.addEventListener('pause', showPauseState);

            renderPlaylist();

            audioCtx = new (window.AudioContext || window.webkitAudioContext)();
            source = audioCtx.createMediaElementSource(audio);
            var splitter = audioCtx.createChannelSplitter(2);
            analyserL = audioCtx.createAnalyser();
            analyserR = audioCtx.createAnalyser();
            analyserL.fftSize = 2048;
            analyserR.fftSize = 2048;
            rmsDataL = new Float32Array(analyserL.frequencyBinCount);
            rmsDataR = new Float32Array(analyserR.frequencyBinCount);
            source.connect(splitter);
            splitter.connect(analyserL, 0);
            splitter.connect(analyserR, 1);
            source.connect(audioCtx.destination);

            function getRmsLevel(channel) {
                var a = channel === 0 ? analyserL : analyserR;
                var d = channel === 0 ? rmsDataL : rmsDataR;
                if (!a) return 0;
                a.getFloatTimeDomainData(d);
                var sum = 0;
                for (var i = 0; i < d.length; i++) sum += d[i] * d[i];
                var rms = Math.sqrt(sum / d.length);
                return Math.min(1, rms);
            }

            function startVu() {
                if (vuInterval) return;
                vuInterval = setInterval(function () {
                    if (playing) {
                        var leftLevel = getRmsLevel(0);
                        var mono = currentTrack && currentTrack.channelCount === 1;
                        vuMeter.setLevels(leftLevel, mono ? leftLevel : getRmsLevel(1));
                    } else {
                        vuMeter.setLevels(0, 0);
                    }
                }, 50);
            }

            startVu();

            document.getElementById('mp3-play').addEventListener('click', function () {
                var p = audioCtx.state === 'suspended' ? audioCtx.resume() : Promise.resolve();
                p.then(function () {
                    if (currentIndex < 0 && playlist.length > 0) {
                        loadTrack(0);
                    } else {
                        audio.play().then(showPlayState).catch(function () {});
                    }
                });
            });
            document.getElementById('mp3-pause').addEventListener('click', function () {
                audio.pause();
            });
            document.getElementById('mp3-stop').addEventListener('click', function () {
                audio.pause();
                audio.currentTime = 0;
                showPauseState();
            });
            document.getElementById('mp3-prev').addEventListener('click', function () {
                var prev = currentIndex - 1;
                if (prev < 0) prev = playlist.length - 1;
                loadTrack(prev);
            });
            document.getElementById('mp3-next').addEventListener('click', function () {
                var next = currentIndex + 1;
                if (next >= playlist.length) next = 0;
                loadTrack(next);
            });
            document.getElementById('mp3-volume').addEventListener('input', function () {
                audio.volume = this.value / 100;
            });
            document.getElementById('mp3-seek-bar').addEventListener('click', function (e) {
                if (!audio.duration) return;
                var rect = this.getBoundingClientRect();
                var pct = (e.clientX - rect.left) / rect.width;
                audio.currentTime = pct * audio.duration;
            });

            document.getElementById('mp3-volume').dispatchEvent(new Event('input'));
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
            playFile({ name: path.split('/').pop(), path: path, size: 0, channelCount: 2 });
        },

        getTransportState: function () {
            return {
                title: currentTrack ? currentTrack.name : '',
                duration: audio.duration || 0,
                currentTime: audio.currentTime || 0,
                playing: !audio.paused
            };
        },

        seekTo: function (seconds) {
            if (!audio.duration || isNaN(seconds)) return;
            audio.currentTime = Math.max(0, Math.min(seconds, audio.duration));
        },

        playPause: function () {
            if (!audio.paused) { audio.pause(); return; }
            var p = audioCtx.state === 'suspended' ? audioCtx.resume() : Promise.resolve();
            p.then(function () {
                if (currentIndex < 0 && playlist.length > 0) {
                    loadTrack(0);
                } else {
                    audio.play().then(showPlayState).catch(function () {});
                }
            });
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
