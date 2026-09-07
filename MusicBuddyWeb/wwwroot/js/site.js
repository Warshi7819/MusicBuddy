(function () {
    var SCROLL_KEY = 'artists-scroll';

    function isArtistsPage() {
        return !!document.getElementById('artists-container');
    }

    document.addEventListener('click', function (e) {
        if (!isArtistsPage()) return;
        var link = e.target.closest('a[href*="/Albums?artist="]');
        if (link) {
            sessionStorage.setItem(SCROLL_KEY, window.scrollY.toString());
        }
    });

    document.addEventListener('htmx:afterSwap', function (e) {
        if (e.detail.target && e.detail.target.id === 'artists-container') {
            var saved = sessionStorage.getItem(SCROLL_KEY);
            if (saved) {
                sessionStorage.removeItem(SCROLL_KEY);
                requestAnimationFrame(function () {
                    window.scrollTo(0, parseInt(saved, 10));
                });
            }
        }
    });
})();
