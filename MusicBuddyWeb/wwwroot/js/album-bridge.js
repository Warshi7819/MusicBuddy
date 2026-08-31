window.albumBridge = (function () {
    function initPlayerFromTrackView() {
        var trackListEl = document.getElementById('track-list');
        if (!trackListEl) return;

        var tracks;
        try {
            tracks = JSON.parse(trackListEl.dataset.tracks || '[]');
        } catch (e) { return; }

        if (tracks.length === 0) return;

        albumPlayer.init(tracks);

        var randomIndex = parseInt(trackListEl.dataset.randomIndex || '-1');
        var randomParams = trackListEl.dataset.randomParams || '';

        if (randomIndex >= 0 && randomIndex < tracks.length) {
            albumPlayer.playTrack(randomIndex);
            if (randomParams) {
                albumPlayer.setRandomParams(randomParams);
                albumPlayer.setRandomIndicator(true, getRandomLabel(randomParams));
            }
            albumPlayer.setOnTrackEnded(function () {
                playRandomFromServer(randomParams);
                return true;
            });
        }
    }

    function getRandomLabel(params) {
        if (!params) return 'Random';
        if (params.indexOf('artist=') !== -1) {
            var val = decodeURIComponent(params.split('artist=')[1].split('&')[0]);
            return 'Random - Artist: ' + val;
        }
        if (params.indexOf('genre=') !== -1) {
            var val = decodeURIComponent(params.split('genre=')[1].split('&')[0]);
            return 'Random - Genre: ' + val;
        }
        return 'Random';
    }

    function playRandomFromServer(params) {
        var qs = params ? '&' + params : '';
        htmx.ajax('GET', '/Albums?handler=RandomTrackView' + qs, {
            target: '#albums-container',
            swap: 'innerHTML',
            pushUrl: 'true'
        });
    }

    document.body.addEventListener('htmx:afterSwap', function (e) {
        if (e.detail.target.id === 'albums-container') {
            requestAnimationFrame(initPlayerFromTrackView);
        }
    });

    return {
        playTrack: function (index) {
            albumPlayer.playTrack(index);
        },
        init: initPlayerFromTrackView
    };
})();
