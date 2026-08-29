if (window.albumPlayer && typeof window.albumPlayer.dispose === 'function') {
    try { window.albumPlayer.dispose(); } catch (e) {}
} else if (window.albumPlayer && typeof window.albumPlayer.stop === 'function') {
    try { window.albumPlayer.stop(); } catch (e) {}
}

var albumPlayer = (function () {
    var tracks = [];
    var currentIndex = -1;
    var playing = false;
    var audioCtx = null;
    var volumeGain = null;
    var tickInterval = null;
    var onTrackEnded = null;
    var _randomParams = null;

    var audio = new Audio();
    var sources = [];

    function formatTime(sec) {
        if (isNaN(sec)) return '0:00';
        var m = Math.floor(sec / 60);
        var s = Math.floor(sec % 60);
        return m + ':' + (s < 10 ? '0' : '') + s;
    }

    function refreshDurationDisplay(ts) {
        if (!ts || ts !== sources[currentIndex]) return;
        var el = document.getElementById('alb-duration');
        if (el) el.textContent = formatTime(ts.endpos / 1000);
    }

    function showPlayState() {
        var playBtn = document.getElementById('alb-play');
        var pauseBtn = document.getElementById('alb-pause');
        if (playBtn) playBtn.style.display = 'none';
        if (pauseBtn) pauseBtn.style.display = '';
        playing = true;
    }

    function showPauseState() {
        var playBtn = document.getElementById('alb-play');
        var pauseBtn = document.getElementById('alb-pause');
        if (playBtn) playBtn.style.display = '';
        if (pauseBtn) pauseBtn.style.display = 'none';
        playing = false;
    }

    function highlightTrack(index) {
        var list = document.getElementById('track-list');
        if (!list) return;
        var items = list.querySelectorAll('.list-group-item');
        for (var i = 0; i < items.length; i++) {
            if (i === index) {
                items[i].classList.add('active');
            } else {
                items[i].classList.remove('active');
            }
        }
    }

    function ensureAudioContext() {
        if (!audioCtx) {
            audioCtx = new (window.AudioContext || window.webkitAudioContext)();
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
            title: track.title || track.displayTitle || track.name,
            artist: track.artist || '',
            duration: track.durationSeconds,
            audio: new Audio(),
            buffer: null,
            source: null,
            gainNode: null,
            state: 'none',
            position: 0,
            endpos: (track.durationSeconds || 0) * 1000,
            lastTick: 0,
            endedTimer: null,
            loadedHTML5: false,
            loadedWebAudio: false
        };

        ts.audio.preload = 'auto';
        ts.audio.addEventListener('canplaythrough', function () {
            if (ts.loadedHTML5) return;
            ts.loadedHTML5 = true;
            if (ts.audio.duration && isFinite(ts.audio.duration)) {
                ts.endpos = ts.audio.duration * 1000;
                refreshDurationDisplay(ts);
            }
        });
        ts.audio.addEventListener('error', function () {});

        return ts;
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
        ts.source.connect(ts.gainNode);
        ts.gainNode.connect(volumeGain);

        ts.source.start(0, pos);
        ts.position = pos * 1000;
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
            ts.source.connect(ts.gainNode);
            ts.gainNode.connect(volumeGain);
            var startOffset = ts.position / 1000;
            ts.source.start(0, startOffset);
            restartEndedTimer(ts);
        } else if (ts.loadedHTML5) {
            ts.audio.currentTime = ts.position / 1000;
            ts.audio.play().then(function () {
                if (ts.state === 'playing') {
                    restartEndedTimer(ts);
                }
            }).catch(function () {});
        } else {
            ts.state = 'loading';
            ts.audio.addEventListener('canplaythrough', function handler() {
                ts.audio.removeEventListener('canplaythrough', handler);
                if (ts.state === 'loading') {
                    ts.state = 'playing';
                    ts.lastTick = performance.now();
                    ts.audio.currentTime = ts.position / 1000;
                    ts.audio.play().then(function () {
                        if (ts.state === 'playing') {
                            restartEndedTimer(ts);
                        }
                    }).catch(function () {});
                }
            });
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
        if (index < 0 || index >= sources.length) return;
        var ts = sources[index];
        if (ts.state !== 'none') return;
        ts.state = 'loading';
        ts.audio.src = ts.path;
        ts.audio.load();
        startWebAudioDecode(ts);
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
            document.getElementById('alb-time').textContent = formatTime(ts.position / 1000);
            var pct = ts.endpos ? (ts.position / ts.endpos * 100) : 0;
            document.getElementById('alb-seek-fill').style.width = pct + '%';
        }
    }

    function onTrackEndedHandler() {
        if (onTrackEnded && onTrackEnded()) return;
        var next = currentIndex + 1;
        if (next >= tracks.length) next = 0;
        loadTrack(next);
    }

    function updateUIForTrack(ts) {
        document.getElementById('alb-now-title').textContent = ts.title || ts.name;
        var artistEl = document.getElementById('alb-now-artist');
        if (ts.artist) {
            artistEl.textContent = '\u2014 ' + ts.artist;
            artistEl.style.display = '';
        } else {
            artistEl.textContent = '';
            artistEl.style.display = 'none';
        }
        document.getElementById('alb-time').textContent = '0:00';
        document.getElementById('alb-duration').textContent = formatTime(ts.duration);
        document.getElementById('alb-seek-fill').style.width = '0%';
        highlightTrack(currentIndex);
    }

    function loadTrack(index) {
        if (index < 0 || index >= tracks.length) return;
        if (currentIndex >= 0 && currentIndex < sources.length) {
            stopTrackSource(sources[currentIndex], true);
        }
        currentIndex = index;
        var ts = sources[index];
        updateUIForTrack(ts);
        loadTrackSource(ts);
        playTrackSource(ts);
        showPlayState();
        preloadTrack(index + 1);
    }

    function handlePlayClick() {
        if (currentIndex >= 0 && currentIndex < sources.length) {
            var ts = sources[currentIndex];
            if (ts.state === 'stopped' || ts.state === 'loading') {
                playTrackSource(ts);
                showPlayState();
            } else if (ts.state === 'playing') {
                ts.position = ts.position || 0;
                stopTrackSource(ts, false);
                showPauseState();
            }
        } else if (tracks.length > 0) {
            loadTrack(0);
        }
    }

    function handlePauseClick() {
        if (currentIndex >= 0 && currentIndex < sources.length) {
            var ts = sources[currentIndex];
            stopTrackSource(ts, false);
            showPauseState();
            onTrackEnded = null;
            _randomParams = null;
            setRandomIndicator(false);
        }
    }

    function handlePrevClick() {
        onTrackEnded = null;
        _randomParams = null;
        setRandomIndicator(false);
        var prev = currentIndex - 1;
        if (prev < 0) prev = tracks.length - 1;
        loadTrack(prev);
    }

    function handleNextClick() {
        onTrackEnded = null;
        _randomParams = null;
        setRandomIndicator(false);
        var next = currentIndex + 1;
        if (next >= tracks.length) next = 0;
        loadTrack(next);
    }

    function handleSeekClick(e, seekEl) {
        if (currentIndex < 0 || currentIndex >= sources.length) return;
        var ts = sources[currentIndex];
        if (!ts.endpos) return;
        var rect = seekEl.getBoundingClientRect();
        var pct = (e.clientX - rect.left) / rect.width;
        var newPos = pct * ts.endpos;
        var wasPlaying = ts.state === 'playing';
        if (wasPlaying) stopTrackSource(ts, false);
        ts.position = newPos;
        if (ts.audio) ts.audio.currentTime = newPos / 1000;
        if (wasPlaying) {
            ts.state = 'stopped';
            playTrackSource(ts);
        }
    }

    var controlScope = document.getElementById('albums-container') || document;

    function findControl(node, id) {
        while (node && node !== controlScope) {
            if (node.id === id) return node;
            node = node.parentNode;
        }
        return null;
    }

    controlScope.addEventListener('click', function (e) {
        var el;
        if ((el = findControl(e.target, 'alb-play'))) { handlePlayClick(); return; }
        if ((el = findControl(e.target, 'alb-pause'))) { handlePauseClick(); return; }
        if ((el = findControl(e.target, 'alb-prev'))) { handlePrevClick(); return; }
        if ((el = findControl(e.target, 'alb-next'))) { handleNextClick(); return; }
        if ((el = findControl(e.target, 'alb-seek-bar'))) { handleSeekClick(e, el); }
    });

    controlScope.addEventListener('input', function (e) {
        if (e.target && e.target.id === 'alb-volume' && volumeGain) {
            volumeGain.gain.value = e.target.value / 100;
        }
    }, true);

    function setRandomIndicator(active, label) {
        var dot = document.getElementById('random-indicator-dot');
        if (!dot) return;
        dot.style.background = active ? '#2CFF05' : '#6c757d';
        var text = document.getElementById('random-indicator-text');
        if (text) text.textContent = label || 'Random';
        var indicator = document.getElementById('random-indicator');
        if (indicator) {
            indicator.classList.toggle('d-none', false);
        }
    }

    return {
        init: function (trackList) {
            tracks = trackList || [];
            currentIndex = -1;
            playing = false;
            sources = [];
            ensureAudioContext();

            for (var i = 0; i < tracks.length; i++) {
                sources.push(createTrackSource(tracks[i]));
            }

            if (tickInterval) clearInterval(tickInterval);
            tickInterval = setInterval(tick, 16);

            var volEl = document.getElementById('alb-volume');
            if (volEl && volumeGain) volEl.value = Math.round(volumeGain.gain.value * 100);
        },

        playTrack: function (index) {
            loadTrack(index);
        },

        stop: function () {
            if (currentIndex >= 0 && currentIndex < sources.length) {
                stopTrackSource(sources[currentIndex], true);
            }
            for (var i = 0; i < sources.length; i++) {
                sources[i].state = 'none';
                sources[i].audio.src = '';
                sources[i].buffer = null;
                sources[i].loadedHTML5 = false;
                sources[i].loadedWebAudio = false;
            }
            playing = false;
            currentIndex = -1;
            showPauseState();
            highlightTrack(-1);
            var nowTitle = document.getElementById('alb-now-title');
            if (nowTitle) nowTitle.textContent = '';
            var timeEl = document.getElementById('alb-time');
            if (timeEl) timeEl.textContent = '0:00';
            var durEl = document.getElementById('alb-duration');
            if (durEl) durEl.textContent = '0:00';
            var fillEl = document.getElementById('alb-seek-fill');
            if (fillEl) fillEl.style.width = '0%';
        },

        dispose: function () {
            this.stop();
            clearInterval(tickInterval);
            tickInterval = null;
            if (audioCtx && audioCtx.state !== 'closed') {
                audioCtx.close().catch(function () {});
            }
            audioCtx = null;
            volumeGain = null;
        },

        isInitialized: function () {
            return tracks.length > 0;
        },

        setOnTrackEnded: function (fn) {
            onTrackEnded = fn;
        },

        getRandomParams: function () {
            return _randomParams;
        },

        setRandomParams: function (params) {
            _randomParams = params;
        },

        setRandomIndicator: setRandomIndicator,

        getTransportState: function () {
            var ts = sources[currentIndex];
            var track = (currentIndex >= 0 && tracks[currentIndex]) ? tracks[currentIndex] : null;
            return {
                title: track ? (track.title || track.name) : '',
                duration: ts && ts.endpos ? ts.endpos / 1000 : 0,
                currentTime: ts ? ts.position / 1000 : 0,
                playing: !!(ts && ts.state === 'playing')
            };
        }
    };
})();
