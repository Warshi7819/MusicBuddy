var mp3Player = (function () {
    var playlist = [];
    var currentIndex = -1;
    var playing = false;
    var audioCtx = null;
    var analyserL = null;
    var analyserR = null;
    var rmsDataL = null;
    var rmsDataR = null;
    var vuInterval = null;
    var tickInterval = null;
    var volumeGain = null;
    var splitter = null;
    var sources = [];
    var onTrackEnded = null;
    var onTrackChangedCallbacks = [];

    function formatTime(sec) {
        if (isNaN(sec)) return '0:00';
        var m = Math.floor(sec / 60);
        var s = Math.floor(sec % 60);
        return m + ':' + (s < 10 ? '0' : '') + s;
    }

    function escapeHtml(s) {
        var d = document.createElement('div');
        d.appendChild(document.createTextNode(s));
        return d.innerHTML;
    }

    function normalizeTrack(track) {
        return {
            name: track.name || track.fileName || '',
            path: track.path || track.filePath || '',
            channelCount: track.channelCount || 2
        };
    }

    function renderPlaylist() {
        var el = document.getElementById('mp3-playlist');
        if (!el) return;
        el.innerHTML = '';
        document.getElementById('mp3-count').textContent = playlist.length;
        playlist.forEach(function (f, i) {
            var item = document.createElement('button');
            item.className = 'list-group-item list-group-item-action' + (i === currentIndex ? ' active' : '');
            item.innerHTML = '<span class="text-truncate">' + escapeHtml(f.name) + '</span>';
            item.addEventListener('click', function () { loadTrack(i); });
            el.appendChild(item);
        });
    }

    function updateActiveRow() {
        var el = document.getElementById('mp3-playlist');
        if (!el) return;
        var items = el.querySelectorAll('.list-group-item');
        for (var i = 0; i < items.length; i++) {
            if (i === currentIndex) {
                items[i].classList.add('active');
            } else {
                items[i].classList.remove('active');
            }
        }
    }

    function resetSeekBar() {
        var fill = document.getElementById('mp3-seek-fill');
        if (!fill) return;
        fill.style.transition = 'none';
        fill.style.width = '0%';
        void fill.offsetWidth;
        fill.style.transition = '';
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
                    if (img.naturalWidth === 1 && img.naturalHeight === 1) {
                        placeholder.style.display = '';
                        img.style.display = 'none';
                    } else {
                        placeholder.style.display = 'none';
                        img.style.display = '';
                    }
                    URL.revokeObjectURL(url);
                };
                img.src = url;
            })
            .catch(function () {});
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

    function resetNowPlayingUI() {
        document.getElementById('mp3-title').textContent = 'No track loaded';
        document.getElementById('mp3-artist').textContent = '';
        document.getElementById('mp3-time').textContent = '0:00';
        document.getElementById('mp3-duration').textContent = '0:00';
        resetSeekBar();
    }

    function ensureAudioContext() {
        if (!audioCtx) {
            audioCtx = new (window.AudioContext || window.webkitAudioContext)();
            analyserL = audioCtx.createAnalyser();
            analyserR = audioCtx.createAnalyser();
            analyserL.fftSize = 2048;
            analyserR.fftSize = 2048;
            rmsDataL = new Float32Array(analyserL.frequencyBinCount);
            rmsDataR = new Float32Array(analyserR.frequencyBinCount);
            splitter = audioCtx.createChannelSplitter(2);
            splitter.connect(analyserL, 0);
            splitter.connect(analyserR, 1);
            volumeGain = audioCtx.createGain();
            volumeGain.gain.value = 0.8;
            volumeGain.connect(audioCtx.destination);
        }
        if (audioCtx.state === 'suspended') {
            audioCtx.resume();
        }
        return audioCtx;
    }

    function createTrackSource(track) {
        var ts = {
            path: track.path,
            name: track.name,
            channelCount: track.channelCount || 2,
            audio: new Audio(),
            mediaSource: null,
            buffer: null,
            source: null,
            gainNode: null,
            state: 'none',
            position: 0,
            endpos: 0,
            lastTick: 0,
            endedTimer: null,
            loadedHTML5: false,
            loadedWebAudio: false
        };

        ts.audio.preload = 'auto';
        ts.audio.addEventListener('canplaythrough', function () {
            if (ts.loadedHTML5) return;
            ts.loadedHTML5 = true;
            if (ts.endpos <= 0 && ts.audio.duration) {
                ts.endpos = ts.audio.duration * 1000;
                refreshDurationDisplay(ts);
            }
        });
        ts.audio.addEventListener('error', function () {});

        return ts;
    }

    function ensureMediaSource(ts) {
        if (ts.mediaSource || !audioCtx) return;
        ts.mediaSource = audioCtx.createMediaElementSource(ts.audio);
        ts.mediaSource.connect(volumeGain);
        if (splitter) ts.mediaSource.connect(splitter);
    }

    function refreshDurationDisplay(ts) {
        if (!ts || ts !== sources[currentIndex]) return;
        var el = document.getElementById('mp3-duration');
        if (el) el.textContent = formatTime(ts.endpos / 1000);
    }

    function startWebAudioDecode(ts) {
        if (ts.loadedWebAudio || ts.state === 'none') return;
        var ctx = ensureAudioContext();
        fetch(ts.path)
            .then(function (r) { return r.arrayBuffer(); })
            .then(function (data) {
                return ctx.decodeAudioData(data);
            })
            .then(function (buf) {
                if (ts.state === 'none') return;
                ts.buffer = buf;
                ts.loadedWebAudio = true;
                ts.endpos = buf.duration * 1000;
                refreshDurationDisplay(ts);
                if (ts.state === 'playing' && !ts.source) {
                    switchToWebAudio(ts);
                }
            })
            .catch(function () {});
    }

    function switchToWebAudio(ts) {
        if (!ts.buffer || ts.source !== null) return;
        var ctx = ensureAudioContext();
        var pos = ts.audio.currentTime || 0;
        ts.audio.pause();

        ts.gainNode = ctx.createGain();
        ts.gainNode.gain.value = 1;
        ts.source = ctx.createBufferSource();
        ts.source.buffer = ts.buffer;
        ts.source.channelCount = 2;
        ts.source.connect(ts.gainNode);
        ts.gainNode.connect(volumeGain);
        if (splitter) ts.gainNode.connect(splitter);

        ts.source.start(0, pos);
        ts.position = pos * 1000;
        if (ts.position >= ts.endpos) ts.position = ts.endpos - 1;
        ts.lastTick = performance.now();
        restartEndedTimer(ts);
    }

    function restartEndedTimer(ts) {
        if (ts.endedTimer) {
            clearTimeout(ts.endedTimer);
            ts.endedTimer = null;
        }
        var remaining = ts.endpos - ts.position;
        if (remaining > 0 && ts.state === 'playing') {
            ts.endedTimer = setTimeout(function () {
                onTrackEndedHandler();
            }, remaining);
        }
    }

    function stopTrackSource(ts, resetPosition) {
        if (ts.endedTimer) {
            clearTimeout(ts.endedTimer);
            ts.endedTimer = null;
        }
        if (ts.source) {
            try { ts.source.stop(); } catch (e) {}
            ts.source.disconnect();
            ts.source = null;
        }
        if (ts.gainNode) {
            ts.gainNode.disconnect();
            ts.gainNode = null;
        }
        ts.audio.pause();
        if (resetPosition) {
            ts.position = 0;
            ts.audio.currentTime = 0;
        }
        ts.state = 'stopped';
    }

    function playTrackSource(ts) {
        if (ts.state === 'playing') return;
        var ctx = ensureAudioContext();
        ts.state = 'playing';
        ts.lastTick = performance.now();

        if (ts.buffer) {
            ts.gainNode = ctx.createGain();
            ts.gainNode.gain.value = 1;
            ts.source = ctx.createBufferSource();
            ts.source.buffer = ts.buffer;
            ts.source.channelCount = 2;
            ts.source.connect(ts.gainNode);
            ts.gainNode.connect(volumeGain);
            if (splitter) ts.gainNode.connect(splitter);
            ts.source.start(0, ts.position / 1000);
            restartEndedTimer(ts);
        } else if (ts.loadedHTML5) {
            ensureMediaSource(ts);
            ts.audio.currentTime = ts.position / 1000;
            ts.audio.play().then(function () {
                if (ts.state === 'playing') {
                    restartEndedTimer(ts);
                } else {
                    ts.audio.pause();
                }
            }).catch(function () {});
        } else {
            if (!ts.audio.getAttribute('src')) {
                ts.state = 'none';
                loadTrackSource(ts);
            }
            ts.state = 'playing';
            ensureMediaSource(ts);
            ts.audio.currentTime = ts.position / 1000;
            ts.audio.play().then(function () {
                if (ts.state === 'playing') {
                    restartEndedTimer(ts);
                } else {
                    ts.audio.pause();
                }
            }).catch(function () {});
        }
    }

    function loadTrackSource(ts) {
        if (ts.state !== 'none') return;
        ts.state = 'loading';
        ts.audio.src = ts.path;
        ts.audio.load();
        startWebAudioDecode(ts);
    }

    function preloadTrack(index) {
        var ts = getTrackSource(index);
        if (!ts) return;
        if (ts.state !== 'none') return;
        ts.state = 'loading';
        ts.audio.src = ts.path;
        ts.audio.load();
        startWebAudioDecode(ts);
    }

    function getTrackSource(index) {
        if (index < 0 || index >= playlist.length) return null;
        if (!sources[index]) sources[index] = createTrackSource(playlist[index]);
        return sources[index];
    }

    function disposeTrackSource(ts) {
        stopTrackSource(ts, true);
        if (ts.mediaSource) {
            try { ts.mediaSource.disconnect(); } catch (e) {}
            ts.mediaSource = null;
        }
        ts.audio.removeAttribute('src');
        ts.audio.load();
        ts.buffer = null;
        ts.loadedHTML5 = false;
        ts.loadedWebAudio = false;
        ts.state = 'none';
    }

    function pruneSources(keepIndex) {
        for (var i = 0; i < sources.length; i++) {
            if (sources[i] && i !== keepIndex && i !== keepIndex + 1) {
                disposeTrackSource(sources[i]);
                sources[i] = undefined;
            }
        }
    }

    function tick() {
        if (!playing) return;
        var now = performance.now();
        var ts = sources[currentIndex];
        if (ts && ts.state === 'playing') {
            var delta = now - ts.lastTick;
            ts.position += delta;
            ts.lastTick = now;
            if (ts.position > ts.endpos) ts.position = ts.endpos;
            document.getElementById('mp3-time').textContent = formatTime(ts.position / 1000);
            var pct = ts.endpos ? (ts.position / ts.endpos * 100) : 0;
            document.getElementById('mp3-seek-fill').style.width = pct + '%';
        }
    }

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
            if (typeof vuMeter === 'undefined') return;
            var ts = sources[currentIndex];
            if (playing && ts && ts.state === 'playing') {
                var leftLevel = getRmsLevel(0);
                var mono = ts.channelCount === 1;
                vuMeter.setLevels(leftLevel, mono ? leftLevel : getRmsLevel(1));
            } else {
                vuMeter.setLevels(0, 0);
            }
        }, 16);
    }

    function updateUIForTrack(ts) {
        document.getElementById('mp3-title').textContent = ts.name;
        document.getElementById('mp3-artist').textContent = '';
        document.getElementById('mp3-time').textContent = '0:00';
        document.getElementById('mp3-duration').textContent = formatTime(ts.endpos / 1000);
        resetSeekBar();
        loadAlbumArt(ts.path);
        updateActiveRow();
    }

    function onTrackEndedHandler() {
        if (onTrackEnded && onTrackEnded()) return;
        var next = currentIndex + 1;
        if (next >= playlist.length) next = 0;
        loadTrack(next);
    }

    function loadTrack(index) {
        if (index < 0 || index >= playlist.length) return;
        var old = sources[currentIndex];
        if (old) stopTrackSource(old, true);
        currentIndex = index;
        var ts = getTrackSource(index);
        pruneSources(index);
        loadTrackSource(ts);
        playTrackSource(ts);
        showPlayState();
        preloadTrack(index + 1 < playlist.length ? index + 1 : 0);
        updateUIForTrack(ts);
        if (onTrackChangedCallbacks.length) {
            for (var c = 0; c < onTrackChangedCallbacks.length; c++) onTrackChangedCallbacks[c](playlist[index], index);
        }
    }

    function seekToPosition(ts, newPos, resume) {
        if (!ts.endpos) return;
        newPos = Math.max(0, Math.min(newPos, ts.endpos));
        var wasPlaying = resume === true ? ts.state === 'playing' : false;
        if (wasPlaying) {
            stopTrackSource(ts, false);
        } else if (ts.audio && !ts.audio.paused) {
            stopTrackSource(ts, false);
        }
        ts.position = newPos;
        if (ts.audio) ts.audio.currentTime = newPos / 1000;
        if (wasPlaying) {
            ts.state = 'stopped';
            playTrackSource(ts);
        }
        document.getElementById('mp3-time').textContent = formatTime(newPos / 1000);
        document.getElementById('mp3-seek-fill').style.width = ts.endpos ? (newPos / ts.endpos * 100) + '%' : '0%';
    }

    function handleStop() {
        var ts = sources[currentIndex];
        if (ts) {
            stopTrackSource(ts, true);
            refreshDurationDisplay(ts);
        }
        showPauseState();
        document.getElementById('mp3-time').textContent = '0:00';
        resetSeekBar();
    }

    function handlePlay() {
        var p = audioCtx.state === 'suspended' ? audioCtx.resume() : Promise.resolve();
        p.then(function () {
            if (currentIndex < 0) {
                if (playlist.length > 0) loadTrack(0);
                return;
            }
            var ts = getTrackSource(currentIndex);
            if (ts.state === 'none') {
                loadTrack(currentIndex);
            } else if (ts.state === 'stopped') {
                playTrackSource(ts);
                showPlayState();
            }
        });
    }

    function isAudible(ts) {
        return !!(ts && (ts.state === 'playing' || ts.state === 'loading'
            || ts.source || (ts.audio && !ts.audio.paused)));
    }

    function handlePause() {
        var ts = sources[currentIndex];
        if (isAudible(ts)) stopTrackSource(ts, false);
        showPauseState();
    }

    function syncPlaylist(tracks) {
        var normalized = tracks.map(normalizeTrack);
        var currentPath = (currentIndex >= 0 && playlist[currentIndex]) ? playlist[currentIndex].path : null;
        var keptIndex = -1;
        if (currentPath) {
            for (var i = 0; i < normalized.length; i++) {
                if (normalized[i].path === currentPath) { keptIndex = i; break; }
            }
        }
        var kept = (keptIndex >= 0 && sources[currentIndex]) ? sources[currentIndex] : null;
        var newSources = [];
        if (kept) newSources[keptIndex] = kept;
        for (var j = 0; j < sources.length; j++) {
            if (sources[j] && sources[j] !== kept) {
                disposeTrackSource(sources[j]);
            }
        }
        sources = newSources;
        playlist = normalized;
        if (kept) {
            currentIndex = keptIndex;
        } else {
            currentIndex = -1;
            showPauseState();
            resetNowPlayingUI();
        }
        renderPlaylist();
    }

    return {
        init: function (files) {
            playlist = (files || []).map(normalizeTrack);
            currentIndex = -1;
            playing = false;
            sources = [];
            ensureAudioContext();

            if (tickInterval) clearInterval(tickInterval);
            tickInterval = setInterval(tick, 16);

            startVu();

            renderPlaylist();

            document.getElementById('mp3-play').addEventListener('click', handlePlay);
            document.getElementById('mp3-pause').addEventListener('click', handlePause);
            document.getElementById('mp3-stop').addEventListener('click', handleStop);
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
                if (volumeGain) volumeGain.gain.value = this.value / 100;
            });
            document.getElementById('mp3-seek-bar').addEventListener('click', function (e) {
                var ts = sources[currentIndex];
                if (!ts || !ts.endpos) return;
                var rect = this.getBoundingClientRect();
                var pct = (e.clientX - rect.left) / rect.width;
                seekToPosition(ts, pct * ts.endpos, true);
            });

            document.getElementById('mp3-volume').dispatchEvent(new Event('input'));
        },

        loadPlaylist: function (tracks) {
            syncPlaylist(tracks || []);
        },

        syncPlaylist: syncPlaylist,

        playByPath: function (path) {
            for (var i = 0; i < playlist.length; i++) {
                if (playlist[i].path === path) { loadTrack(i); return; }
            }
            syncPlaylist([{ name: path.split('/').pop(), path: path, size: 0, channelCount: 2 }]);
            loadTrack(0);
        },

        selectByPath: function (path) {
            for (var i = 0; i < playlist.length; i++) {
                if (playlist[i].path === path) {
                    currentIndex = i;
                    updateUIForTrack(getTrackSource(i));
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
            var ts = sources[currentIndex];
            var track = (currentIndex >= 0 && playlist[currentIndex]) ? playlist[currentIndex] : null;
            return {
                title: track ? track.name : '',
                duration: ts && ts.endpos ? ts.endpos / 1000 : 0,
                currentTime: ts ? ts.position / 1000 : 0,
                playing: !!(ts && ts.state === 'playing')
            };
        },

        seekTo: function (seconds) {
            var ts = sources[currentIndex];
            if (!ts || !ts.endpos || isNaN(seconds)) return;
            seekToPosition(ts, seconds * 1000, true);
        },

        play: handlePlay,
        pause: handlePause,

        prev: function () {
            var prev = currentIndex - 1;
            if (prev < 0) prev = playlist.length - 1;
            loadTrack(prev);
        },

        next: function () {
            var next = currentIndex + 1;
            if (next >= playlist.length) next = 0;
            loadTrack(next);
        },

        stop: handleStop
    };
})();
