var albums = (function () {
    var ARTIST_ART_COOKIE = 'MusicBuddyArtistArt';

    function getArtistArtMap() {
        try {
            var val = document.cookie.split('; ').find(function (c) { return c.startsWith(ARTIST_ART_COOKIE + '='); });
            if (!val) return {};
            return JSON.parse(decodeURIComponent(val.split('=').slice(1).join('=')));
        } catch { return {}; }
    }

    function setArtistArtMap(map) {
        document.cookie = ARTIST_ART_COOKIE + '=' + encodeURIComponent(JSON.stringify(map)) + ';path=/;max-age=31536000';
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

    function renderArtistGrid(artists) {
        var grid = document.getElementById('albums-grid');
        var artMap = getArtistArtMap();

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
        var artMap = getArtistArtMap();

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
                artMap[artist.path] = album.path;
                setArtistArtMap(artMap);

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

        header.classList.remove('d-none');
        backBtn.href = '/Albums?artist=' + encodeURIComponent(artist.name);
        backBtn.innerHTML = '<i class="bi bi-arrow-left me-1"></i>' + artist.name;
        nameEl.textContent = album.name;

        grid.classList.add('d-none');
        trackView.classList.remove('d-none');

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

        var list = document.getElementById('track-list');
        detail.tracks.forEach(function (track) {
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

        fetch('/api/albums')
            .then(function (res) { return res.json(); })
            .then(function (data) {
                loading.classList.add('d-none');
                var artists = data.artists || [];

                if (artists.length === 0) {
                    empty.classList.remove('d-none');
                    return;
                }

                content.classList.remove('d-none');

                if (selectedArtist) {
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
                }
            })
            .catch(function () {
                loading.classList.add('d-none');
                empty.classList.remove('d-none');
            });
    }

    return { init: init };
})();
