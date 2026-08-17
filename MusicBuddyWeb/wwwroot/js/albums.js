var albums = (function () {
    var albumPlayer = (function () {
        var tracks = [];
        var currentIndex = -1;
        var playing = false;
        var audioCtx = null;
        var tickInterval = null;
        var onTrackEnded = null;

        var audio = new Audio();
        var sources = [];

        function formatTime(sec) {
            if (isNaN(sec)) return '0:00';
            var m = Math.floor(sec / 60);
            var s = Math.floor(sec % 60);
            return m + ':' + (s < 10 ? '0' : '') + s;
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
                if (ts.endpos <= 0 && ts.audio.duration) {
                    ts.endpos = ts.audio.duration * 1000;
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
                    if (ts.endpos <= 0) {
                        ts.endpos = buf.duration * 1000;
                    }
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
            ts.gainNode.connect(ctx.destination);

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
                ts.gainNode.connect(ctx.destination);
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
            document.getElementById('alb-now-title').textContent = ts.name;
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

                document.getElementById('alb-play').addEventListener('click', function () {
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
                });
                document.getElementById('alb-pause').addEventListener('click', function () {
                    if (currentIndex >= 0 && currentIndex < sources.length) {
                        var ts = sources[currentIndex];
                        stopTrackSource(ts, false);
                        showPauseState();
                    }
                });
                document.getElementById('alb-prev').addEventListener('click', function () {
                    var prev = currentIndex - 1;
                    if (prev < 0) prev = tracks.length - 1;
                    loadTrack(prev);
                });
                document.getElementById('alb-next').addEventListener('click', function () {
                    var next = currentIndex + 1;
                    if (next >= tracks.length) next = 0;
                    loadTrack(next);
                });
                document.getElementById('alb-seek-bar').addEventListener('click', function (e) {
                    if (currentIndex < 0 || currentIndex >= sources.length) return;
                    var ts = sources[currentIndex];
                    if (!ts.endpos) return;
                    var rect = this.getBoundingClientRect();
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
                });
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
                document.getElementById('alb-now-title').textContent = '';
                document.getElementById('alb-time').textContent = '0:00';
                document.getElementById('alb-duration').textContent = '0:00';
                document.getElementById('alb-seek-fill').style.width = '0%';
            },

            isInitialized: function () {
                return tracks.length > 0;
            }
        };
    })();

    var artistArtMap = {};

    function loadArtistArtMap() {
        return fetch('/api/artistart')
            .then(function (res) { return res.json(); })
            .then(function (map) { artistArtMap = map || {}; })
            .catch(function () { artistArtMap = {}; });
    }

    function saveArtistArt(artistPath, albumPath) {
        return fetch('/api/artistart?artistPath=' + encodeURIComponent(artistPath) +
            '&albumPath=' + encodeURIComponent(albumPath), { method: 'PUT' })
            .then(function (res) {
                if (!res.ok) throw new Error('Save failed');
            });
    }

    function refreshArtistArtButtons(grid, useBtn) {
        var buttons = grid.querySelectorAll('.album-art-btn');
        for (var i = 0; i < buttons.length; i++) {
            var btn = buttons[i];
            if (btn === useBtn) {
                btn.className = 'btn btn-success btn-sm mt-2 album-art-btn';
                btn.innerHTML = '<i class="bi bi-check me-1"></i>Selected';
            } else if (btn.classList.contains('btn-success')) {
                btn.className = 'btn btn-outline-secondary btn-sm mt-2 album-art-btn';
                btn.innerHTML = '<i class="bi bi-image me-1"></i>Use as artist image';
            }
        }
    }

    function fetchAlbumArt(path, imgEl, placeholderEl, onResult) {
        if (!path) { if (onResult) onResult(false); return; }
        fetch('/api/albumart?path=' + encodeURIComponent(path))
            .then(function (res) {
                if (!res.ok || res.status === 204) return null;
                return res.blob();
            })
            .then(function (blob) {
                if (!blob) { if (onResult) onResult(false); return; }
                var url = URL.createObjectURL(blob);
                imgEl.onload = function () {
                    if (placeholderEl) placeholderEl.style.display = 'none';
                    imgEl.classList.remove('d-none');
                    if (onResult) onResult(true);
                };
                imgEl.onerror = function () {
                    if (onResult) onResult(false);
                };
                imgEl.src = url;
            })
            .catch(function () { if (onResult) onResult(false); });
    }

    function renderStats(artists) {
        var el = document.getElementById('albums-stats');
        if (!el) return;
        var artistCount = artists.length;
        var albumCount = 0;
        var songCount = 0;
        for (var i = 0; i < artists.length; i++) {
            var a = artists[i].albums || [];
            albumCount += a.length;
            for (var j = 0; j < a.length; j++) {
                songCount += a[j].trackCount || 0;
            }
        }
        el.innerHTML = '<i class="bi bi-people me-1"></i>' + artistCount + ' Artists'
            + ' &middot; <i class="bi bi-disc me-1"></i>' + albumCount + ' Albums'
            + ' &middot; <i class="bi bi-music-note me-1"></i>' + songCount + ' Songs';
        el.classList.remove('d-none');
    }

    function renderArtistGrid(artists) {
        var grid = document.getElementById('albums-grid');
        var artMap = artistArtMap;

        artists.forEach(function (artist) {
            var col = document.createElement('div');
            col.className = 'col-6 col-md-4 col-lg-3 mb-3';

            var card = document.createElement('div');
            card.className = 'card h-100 album-card';
            card.style.cursor = 'pointer';

            card.addEventListener('click', function () {
                window.location.href = '/Albums?artist=' + encodeURIComponent(artist.name);
            });

            var artWrap = document.createElement('div');
            artWrap.className = 'album-art-wrap';

            var img = document.createElement('img');
            img.className = 'album-art d-none';
            img.alt = artist.name;

            var placeholder = document.createElement('div');
            placeholder.className = 'album-art-placeholder';
            var icon = document.createElement('i');
            icon.className = 'bi bi-disc';
            icon.style.fontSize = '2.5rem';
            placeholder.appendChild(icon);

            var artPath = null;
            if (artMap[artist.path] && artist.albums) {
                var chosen = artist.albums.find(function (a) { return a.path === artMap[artist.path]; });
                if (chosen) artPath = chosen.firstTrackPath;
            }
            if (!artPath && artist.albums && artist.albums.length > 0) {
                artPath = artist.albums[0].firstTrackPath;
            }

            if (!artPath) {
                var noArtText = document.createElement('small');
                noArtText.className = 'text-muted d-block mt-1';
                noArtText.style.fontSize = '0.7rem';
                noArtText.textContent = 'No image set';
                placeholder.appendChild(noArtText);
            }

            artWrap.appendChild(img);
            artWrap.appendChild(placeholder);

            var body = document.createElement('div');
            body.className = 'card-body';

            var title = document.createElement('h6');
            title.className = 'card-title mb-1 text-truncate';
            title.textContent = artist.name;

            var subtitle = document.createElement('small');
            subtitle.className = 'text-muted';
            subtitle.textContent = artist.albumCount + ' album' + (artist.albumCount !== 1 ? 's' : '');

            body.appendChild(title);
            body.appendChild(subtitle);
            card.appendChild(artWrap);
            card.appendChild(body);
            col.appendChild(card);
            grid.appendChild(col);

            if (artPath) fetchAlbumArt(artPath, img, placeholder);
        });
    }

    function renderAlbumGrid(artist, albums) {
        var header = document.getElementById('albums-header');
        var nameEl = document.getElementById('albums-artist-name');
        header.classList.remove('d-none');
        nameEl.textContent = artist.name;

        var grid = document.getElementById('albums-grid');
        var artMap = artistArtMap;

        albums.forEach(function (album) {
            var col = document.createElement('div');
            col.className = 'col-6 col-md-4 col-lg-3 mb-3';

            var card = document.createElement('div');
            card.className = 'card h-100 album-card';
            card.style.cursor = 'pointer';

            card.addEventListener('click', function () {
                window.location.href = '/Albums?artist=' + encodeURIComponent(artist.name) + '&album=' + encodeURIComponent(album.path);
            });

            var artWrap = document.createElement('div');
            artWrap.className = 'album-art-wrap';

            var img = document.createElement('img');
            img.className = 'album-art d-none';
            img.alt = album.name;

            var placeholder = document.createElement('div');
            placeholder.className = 'album-art-placeholder';
            var icon = document.createElement('i');
            icon.className = 'bi bi-disc';
            icon.style.fontSize = '2.5rem';
            placeholder.appendChild(icon);

            artWrap.appendChild(img);
            artWrap.appendChild(placeholder);

            var body = document.createElement('div');
            body.className = 'card-body';

            var title = document.createElement('h6');
            title.className = 'card-title mb-1 text-truncate';
            title.textContent = album.name;

            var subtitle = document.createElement('small');
            subtitle.className = 'text-muted d-block';
            subtitle.textContent = album.trackCount + ' track' + (album.trackCount !== 1 ? 's' : '');

            var useBtn = document.createElement('button');
            useBtn.className = 'btn btn-outline-secondary btn-sm mt-2 d-none album-art-btn';
            useBtn.innerHTML = '<i class="bi bi-image me-1"></i>Use as artist image';
            useBtn.addEventListener('click', function (e) {
                e.stopPropagation();
                var prev = artMap[artist.path];
                artMap[artist.path] = album.path;
                refreshArtistArtButtons(grid, useBtn);
                saveArtistArt(artist.path, album.path).catch(function () {
                    artMap[artist.path] = prev;
                    refreshArtistArtButtons(grid, useBtn);
                });
            });

            if (artMap[artist.path] === album.path) {
                useBtn.className = 'btn btn-success btn-sm mt-2 album-art-btn';
                useBtn.innerHTML = '<i class="bi bi-check me-1"></i>Selected';
            }

            body.appendChild(title);
            body.appendChild(subtitle);
            body.appendChild(useBtn);
            card.appendChild(artWrap);
            card.appendChild(body);
            col.appendChild(card);
            grid.appendChild(col);

            if (album.firstTrackPath) fetchAlbumArt(album.firstTrackPath, img, placeholder, function (hasArt) {
                if (hasArt || artMap[artist.path] === album.path) useBtn.classList.remove('d-none');
            });
        });
    }

    function formatDuration(seconds) {
        if (!seconds || seconds <= 0) return '--:--';
        var h = Math.floor(seconds / 3600);
        var m = Math.floor((seconds % 3600) / 60);
        var s = seconds % 60;
        var pad = function (n) { return n < 10 ? '0' + n : '' + n; };
        return h > 0 ? h + ':' + pad(m) + ':' + pad(s) : m + ':' + pad(s);
    }

    function renderTrackView(artist, album, detail) {
        var header = document.getElementById('albums-header');
        var nameEl = document.getElementById('albums-artist-name');
        var backBtn = document.getElementById('albums-back-btn');
        var grid = document.getElementById('albums-grid');
        var trackView = document.getElementById('albums-track-view');
        var playerBar = document.getElementById('album-player-bar');

        header.classList.remove('d-none');
        backBtn.href = '/Albums?artist=' + encodeURIComponent(artist.name);
        backBtn.innerHTML = '<i class="bi bi-arrow-left me-1"></i>' + artist.name;
        nameEl.textContent = album.name;

        grid.classList.add('d-none');
        trackView.classList.remove('d-none');

        albumPlayer.stop();

        var infoCard = document.getElementById('album-info-card');
        if (!artist.isUncatalogued && detail && detail.tracks.length > 0) {
            var metaParts = [];
            if (detail.albumArtist) metaParts.push(detail.albumArtist);
            if (detail.year) metaParts.push(detail.year);
            if (detail.genre) metaParts.push(detail.genre);

            if (metaParts.length > 0) {
                document.getElementById('album-info-name').textContent = detail.name;
                document.getElementById('album-info-meta').textContent = metaParts.join(' • ');
                fetchAlbumArt(detail.tracks[0].path,
                    document.getElementById('album-info-art'),
                    document.getElementById('album-info-art-ph'));
                infoCard.classList.remove('d-none');
            }
        }

        if (detail.tracks.length > 0) {
            playerBar.classList.remove('d-none');
            albumPlayer.init(detail.tracks);
        }

        var list = document.getElementById('track-list');
        detail.tracks.forEach(function (track, index) {
            var item = document.createElement('button');
            item.type = 'button';
            item.className = 'list-group-item list-group-item-action d-flex justify-content-between align-items-center';

            var name = document.createElement('span');
            name.className = 'text-truncate';
            name.textContent = track.name;

            var dur = document.createElement('span');
            dur.className = 'text-muted small ms-2 flex-shrink-0';
            dur.textContent = formatDuration(track.durationSeconds);

            item.appendChild(name);
            item.appendChild(dur);

            item.addEventListener('click', function () {
                albumPlayer.playTrack(index);
            });

            list.appendChild(item);
        });
    }

    function init(selectedArtist, selectedAlbum) {
        var loading = document.getElementById('albums-loading');
        var content = document.getElementById('albums-content');
        var empty = document.getElementById('albums-empty');
        var grid = document.getElementById('albums-grid');

        var refreshBtn = document.getElementById('albums-refresh-btn');
        if (refreshBtn) {
            refreshBtn.addEventListener('click', function () {
                refreshBtn.disabled = true;
                refreshBtn.innerHTML = '<span class="spinner-border spinner-border-sm me-1"></span>Refreshing...';
                fetch('/api/albums/refresh', { method: 'POST' })
                    .then(function (res) {
                        if (!res.ok) throw new Error('Refresh failed');
                        window.location.reload();
                    })
                    .catch(function () {
                        refreshBtn.disabled = false;
                        refreshBtn.innerHTML = '<i class="bi bi-arrow-clockwise me-1"></i>Refresh';
                    });
            });
        }

        Promise.all([
            fetch('/api/albums').then(function (res) { return res.json(); }),
            loadArtistArtMap()
        ]).then(function (parts) {
            var data = parts[0];
            loading.classList.add('d-none');
            var artists = data.artists || [];

            if (artists.length === 0) {
                empty.classList.remove('d-none');
                return;
            }

            content.classList.remove('d-none');

            var stats = document.getElementById('albums-stats');

            if (selectedArtist) {
                if (stats) stats.classList.add('d-none');
                var artist = artists.find(function (a) { return a.name === selectedArtist; });
                if (!artist) {
                    empty.classList.remove('d-none');
                    content.classList.add('d-none');
                    return;
                }
                if (selectedAlbum !== null && selectedAlbum !== undefined) {
                    var album = artist.albums.find(function (a) { return a.path === selectedAlbum; });
                    if (!album) {
                        empty.classList.remove('d-none');
                        content.classList.add('d-none');
                        return;
                    }
                    fetch('/api/albums/album?path=' + encodeURIComponent(album.path))
                        .then(function (res) { return res.json(); })
                        .then(function (detail) {
                            renderTrackView(artist, album, detail);
                        })
                        .catch(function () {
                            empty.classList.remove('d-none');
                            content.classList.add('d-none');
                        });
                } else {
                    renderAlbumGrid(artist, artist.albums);
                }
            } else {
                renderArtistGrid(artists);
                renderStats(artists);
            }
        })
        .catch(function () {
            loading.classList.add('d-none');
            empty.classList.remove('d-none');
        });
    }

    return {
        init: init,
        stopPlayer: function () { albumPlayer.stop(); }
    };
})();
