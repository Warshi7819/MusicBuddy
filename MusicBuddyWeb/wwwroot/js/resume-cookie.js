var resumeCookie = (function () {
    var COOKIE_NAME = 'MusicBuddyResume';
    var MAX_AGE = 365 * 24 * 60 * 60;

    function readAll() {
        var prefix = COOKIE_NAME + '=';
        var pairs = document.cookie.split(';');
        for (var i = 0; i < pairs.length; i++) {
            var pair = pairs[i].trim();
            if (pair.indexOf(prefix) === 0) {
                try {
                    return JSON.parse(decodeURIComponent(pair.substring(prefix.length))) || {};
                } catch (e) {
                    return {};
                }
            }
        }
        return {};
    }

    function writeAll(all) {
        document.cookie = COOKIE_NAME + '=' + encodeURIComponent(JSON.stringify(all)) +
            '; max-age=' + MAX_AGE + '; path=/; SameSite=Lax';
    }

    function saveResume(fileType, playlistId, trackPath) {
        var all = readAll();
        var entry = all[fileType] || {};
        if (playlistId !== undefined) entry.playlistId = playlistId;
        if (trackPath !== undefined) entry.track = trackPath;
        all[fileType] = entry;
        writeAll(all);
    }

    function loadResume(fileType) {
        return readAll()[fileType] || null;
    }

    function clearResume(fileType) {
        var all = readAll();
        delete all[fileType];
        writeAll(all);
    }

    return {
        saveResume: saveResume,
        loadResume: loadResume,
        clearResume: clearResume
    };
})();