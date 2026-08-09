var mp3Player = (function () {
    var audio = new Audio();
    var playlist = [];
    var currentIndex = -1;
    var playing = false;
    var audioCtx = null;
    var analyser = null;
    var rmsData = null;
    var vuInterval = null;

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
        var file = playlist[index];
        document.getElementById('mp3-title').textContent = file.name;
        document.getElementById('mp3-artist').textContent = '';
        document.getElementById('mp3-time').textContent = '0:00';
        document.getElementById('mp3-duration').textContent = '0:00';
        document.getElementById('mp3-seek-fill').style.width = '0%';
        if (audioCtx && audioCtx.state === 'suspended') audioCtx.resume();
        audio.src = file.path;
        audio.load();
        audio.play().then(showPlayState).catch(function () {});
        renderPlaylist();
    }

    function normalizeTrack(track) {
        return {
            name: track.name || track.fileName || '',
            path: track.path || track.filePath || '',
            size: track.size || track.fileSize || 0
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

            function setupAudioGraph() {
                if (audioCtx) return;
                audioCtx = new (window.AudioContext || window.webkitAudioContext)();
                var source = audioCtx.createMediaElementSource(audio);
                analyser = audioCtx.createAnalyser();
                analyser.fftSize = 2048;
                rmsData = new Float32Array(analyser.frequencyBinCount);
                source.connect(analyser);
                analyser.connect(audioCtx.destination);
            }

            function getRmsLevel() {
                if (!analyser) return 0;
                analyser.getFloatTimeDomainData(rmsData);
                var sum = 0;
                for (var i = 0; i < rmsData.length; i++) sum += rmsData[i] * rmsData[i];
                var rms = Math.sqrt(sum / rmsData.length);
                return Math.min(1, rms);
            }

            function startVu() {
                if (vuInterval) return;
                vuInterval = setInterval(function () {
                    if (playing) {
                        vuMeter.setLevels(getRmsLevel(), getRmsLevel());
                    } else {
                        vuMeter.setLevels(0, 0);
                    }
                }, 50);
            }

            document.getElementById('mp3-play').addEventListener('click', function () {
                setupAudioGraph();
                if (audioCtx && audioCtx.state === 'suspended') audioCtx.resume();
                startVu();
                if (currentIndex < 0 && playlist.length > 0) {
                    loadTrack(0);
                } else {
                    audio.play().catch(function () {});
                }
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
        }
    };
})();
