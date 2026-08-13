var vuMeter = (function () {
    var instances = {};
    var activeId = null;

    var SMOOTHING = 0.25;

    var METER_WIDTH = 155;
    var METER_HEIGHT = 105;
    var METER_GAP = 50;
    var CANVAS_WIDTH = METER_WIDTH * 2 + METER_GAP + 20;
    var CANVAS_HEIGHT = METER_HEIGHT + 10;

    var TICK_DB = [-60, -50, -40, -30, -20, -10, 0];
    var VALID_STYLES = ['classic', 'glow', 'blueglow', 'flatgold'];

    function clamp01(v) {
        return Math.max(0, Math.min(1, v));
    }

    function levelToDb(level) {
        if (level <= 0) return TICK_DB[0];
        var db = 20 * Math.log10(level);
        return Math.max(TICK_DB[0], Math.min(0, db));
    }

    function roundRect(c, x, y, w, h, r) {
        c.beginPath();
        c.moveTo(x + r, y);
        c.lineTo(x + w - r, y);
        c.quadraticCurveTo(x + w, y, x + w, y + r);
        c.lineTo(x + w, y + h - r);
        c.quadraticCurveTo(x + w, y + h, x + w - r, y + h);
        c.lineTo(x + r, y + h);
        c.quadraticCurveTo(x, y + h, x, y + h - r);
        c.lineTo(x, y + r);
        c.quadraticCurveTo(x, y, x + r, y);
        c.closePath();
    }

    // ---- arc-based styles (classic, glow) ----

    var ARC_START_ANGLE = Math.PI + 0.35;
    var ARC_END_ANGLE = 2 * Math.PI - 0.35;
    var ARC_RADIUS = 72;
    var NEEDLE_LENGTH = 64;

    function dbToAngle(db) {
        var t = (db - TICK_DB[0]) / (TICK_DB[TICK_DB.length - 1] - TICK_DB[0]);
        return ARC_START_ANGLE + t * (ARC_END_ANGLE - ARC_START_ANGLE);
    }

    function drawArcSegment(c, cx, cy, r, startDb, endDb, color) {
        c.beginPath();
        c.arc(cx, cy, r, dbToAngle(startDb), dbToAngle(endDb));
        c.strokeStyle = color;
        c.lineWidth = 8;
        c.stroke();
    }

    var ARC_PALETTES = {
        classic: {
            bg: '#0a0e1a',
            glowBorder: 'rgba(50, 120, 220, 0.25)',
            bgShadow: 'rgba(40, 100, 200, 0.4)',
            track: 'rgba(40, 80, 160, 0.15)',
            seg1: 'rgba(40, 140, 255, 0.35)',
            seg2: 'rgba(40, 180, 255, 0.5)',
            seg3: 'rgba(255, 80, 60, 0.6)',
            tick: 'rgba(160, 200, 255, 0.7)',
            minorTick: 'rgba(100, 160, 240, 0.3)',
            label: 'rgba(140, 190, 255, 0.8)',
            needleGlow: 'rgba(60, 140, 255, 0.3)',
            needle: '#c8ddf8',
            needleBlur: 3,
            pivot: '#8ab4f0',
            pivotGlow: false
        },
        glow: {
            bg: '#0a0e1a',
            glowBorder: 'rgba(255, 170, 50, 0.3)',
            bgShadow: 'rgba(255, 170, 40, 0.3)',
            track: 'rgba(120, 90, 20, 0.18)',
            seg1: 'rgba(255, 190, 60, 0.35)',
            seg2: 'rgba(255, 215, 90, 0.55)',
            seg3: 'rgba(255, 120, 40, 0.6)',
            tick: 'rgba(255, 220, 150, 0.7)',
            minorTick: 'rgba(200, 160, 80, 0.3)',
            label: 'rgba(255, 210, 130, 0.8)',
            needleGlow: 'rgba(255, 200, 80, 0.8)',
            needle: '#ffe9b0',
            needleBlur: 9,
            pivot: '#ffd27a',
            pivotGlow: true,
            glowCore: 'rgba(255, 190, 60, 0.58)',
            glowMid: 'rgba(255, 160, 40, 0.2)'
        },
        blueglow: {
            bg: '#0a0e1a',
            glowBorder: 'rgba(50, 120, 255, 0.3)',
            bgShadow: 'rgba(40, 100, 255, 0.3)',
            track: 'rgba(30, 60, 140, 0.18)',
            seg1: 'rgba(60, 140, 255, 0.35)',
            seg2: 'rgba(90, 180, 255, 0.55)',
            seg3: 'rgba(255, 90, 60, 0.6)',
            tick: 'rgba(160, 210, 255, 0.7)',
            minorTick: 'rgba(90, 150, 230, 0.3)',
            label: 'rgba(150, 200, 255, 0.8)',
            needleGlow: 'rgba(90, 170, 255, 0.8)',
            needle: '#d6e9ff',
            needleBlur: 9,
            pivot: '#8fbdf7',
            pivotGlow: true,
            glowCore: 'rgba(90, 170, 255, 0.58)',
            glowMid: 'rgba(60, 130, 255, 0.2)'
        }
    };

    function drawArcMeter(c, cx, level, palette) {
        var baseX = cx - METER_WIDTH / 2;
        var baseY = 5;

        c.save();

        c.fillStyle = palette.bg;
        c.shadowColor = palette.bgShadow;
        c.shadowBlur = 18;
        roundRect(c, baseX, baseY, METER_WIDTH, METER_HEIGHT, 5);
        c.fill();
        c.shadowBlur = 0;

        c.strokeStyle = palette.glowBorder;
        c.lineWidth = 1;
        roundRect(c, baseX + 1, baseY + 1, METER_WIDTH - 2, METER_HEIGHT - 2, 4);
        c.stroke();

        var pivotX = cx;
        var pivotY = baseY + METER_HEIGHT - 10;

        if (palette.pivotGlow) {
            var grad = c.createRadialGradient(pivotX, pivotY, 4, pivotX, pivotY, ARC_RADIUS + 23);
            grad.addColorStop(0, palette.glowCore);
            grad.addColorStop(0.45, palette.glowMid);
            grad.addColorStop(1, 'rgba(0, 0, 0, 0)');
            c.beginPath();
            c.arc(pivotX, pivotY, ARC_RADIUS + 23, 0, 2 * Math.PI);
            c.fillStyle = grad;
            c.fill();
        }

        c.beginPath();
        c.arc(pivotX, pivotY, ARC_RADIUS, ARC_START_ANGLE, ARC_END_ANGLE);
        c.strokeStyle = palette.track;
        c.lineWidth = 8;
        c.stroke();

        drawArcSegment(c, pivotX, pivotY, ARC_RADIUS, -60, -20, palette.seg1);
        drawArcSegment(c, pivotX, pivotY, ARC_RADIUS, -20, -6, palette.seg2);
        drawArcSegment(c, pivotX, pivotY, ARC_RADIUS, -6, 0, palette.seg3);

        c.textAlign = 'center';
        c.textBaseline = 'middle';
        for (var i = 0; i < TICK_DB.length; i++) {
            var angle = dbToAngle(TICK_DB[i]);
            var cos = Math.cos(angle);
            var sin = Math.sin(angle);

            var outerR = ARC_RADIUS + 6;
            var innerR = ARC_RADIUS - 6;
            var labelR = ARC_RADIUS - 18;

            c.beginPath();
            c.moveTo(pivotX + cos * innerR, pivotY + sin * innerR);
            c.lineTo(pivotX + cos * outerR, pivotY + sin * outerR);
            c.strokeStyle = palette.tick;
            c.lineWidth = TICK_DB[i] === 0 ? 2 : 1;
            c.stroke();

            if (i < TICK_DB.length - 1) {
                for (var m = 1; m < 5; m++) {
                    var minordb = TICK_DB[i] + m * 2.5;
                    var minorAngle = dbToAngle(minordb);
                    var mcos = Math.cos(minorAngle);
                    var msin = Math.sin(minorAngle);
                    c.beginPath();
                    c.moveTo(pivotX + mcos * (ARC_RADIUS - 3), pivotY + msin * (ARC_RADIUS - 3));
                    c.lineTo(pivotX + mcos * (ARC_RADIUS + 3), pivotY + msin * (ARC_RADIUS + 3));
                    c.strokeStyle = palette.minorTick;
                    c.lineWidth = 0.5;
                    c.stroke();
                }
            }

            var lbl = TICK_DB[i] === 0 ? '0' : TICK_DB[i].toString();
            c.font = '9px system-ui, sans-serif';
            c.fillStyle = palette.label;
            c.fillText(lbl, pivotX + cos * labelR, pivotY + sin * labelR);
        }

        var db = levelToDb(level);
        var needleAngle = dbToAngle(db);
        var nx = Math.cos(needleAngle);
        var ny = Math.sin(needleAngle);

        c.shadowColor = palette.needleGlow;
        c.shadowBlur = palette.needleBlur;
        c.lineWidth = 1.2;
        c.strokeStyle = palette.needle;
        c.beginPath();
        c.moveTo(pivotX, pivotY);
        c.lineTo(pivotX + nx * NEEDLE_LENGTH, pivotY + ny * NEEDLE_LENGTH);
        c.stroke();
        c.shadowBlur = 0;

        c.beginPath();
        c.arc(pivotX, pivotY, 3, 0, 2 * Math.PI);
        c.fillStyle = palette.pivot;
        c.fill();

        c.restore();
    }

    // ---- flat linear style (flatgold) ----

    var FLAT_ZONES = [
        { from: -60, to: -20, color: 'rgba(255, 190, 60, 0.10)' },
        { from: -20, to: -6, color: 'rgba(255, 215, 90, 0.16)' },
        { from: -6, to: 0, color: 'rgba(255, 120, 40, 0.18)' }
    ];

    function dbToFlatX(cx, db) {
        var baseX = cx - METER_WIDTH / 2;
        return baseX + 16 + (db - TICK_DB[0]) / (TICK_DB[TICK_DB.length - 1] - TICK_DB[0]) * (METER_WIDTH - 32);
    }

    function drawFlatMeter(c, cx, level) {
        var baseX = cx - METER_WIDTH / 2;
        var baseY = 5;
        var trackY = baseY + METER_HEIGHT - 34;
        var top = trackY - 14;
        var bottom = trackY + 14;

        c.save();

        c.fillStyle = '#0a0e1a';
        c.shadowColor = 'rgba(255, 170, 40, 0.3)';
        c.shadowBlur = 18;
        roundRect(c, baseX, baseY, METER_WIDTH, METER_HEIGHT, 5);
        c.fill();
        c.shadowBlur = 0;

        c.strokeStyle = 'rgba(255, 190, 60, 0.2)';
        c.lineWidth = 1;
        roundRect(c, baseX + 1, baseY + 1, METER_WIDTH - 2, METER_HEIGHT - 2, 4);
        c.stroke();

        for (var z = 0; z < FLAT_ZONES.length; z++) {
            var zone = FLAT_ZONES[z];
            var zx1 = dbToFlatX(cx, zone.from);
            var zx2 = dbToFlatX(cx, zone.to);
            c.fillStyle = zone.color;
            c.fillRect(zx1, top, zx2 - zx1, bottom - top);
        }

        var xStart = dbToFlatX(cx, TICK_DB[0]);
        var xEnd = dbToFlatX(cx, TICK_DB[TICK_DB.length - 1]);

        c.strokeStyle = 'rgba(255, 200, 80, 0.5)';
        c.lineWidth = 2;
        c.beginPath();
        c.moveTo(xStart, trackY);
        c.lineTo(xEnd, trackY);
        c.stroke();

        c.fillStyle = 'rgba(255, 220, 150, 0.8)';
        c.fillRect(xStart - 2, trackY - 1, 4, 2);
        c.fillRect(xEnd - 2, trackY - 1, 4, 2);

        c.textAlign = 'center';
        c.textBaseline = 'middle';
        for (var i = 0; i < TICK_DB.length; i++) {
            var x = dbToFlatX(cx, TICK_DB[i]);

            c.beginPath();
            c.moveTo(x, trackY - 8);
            c.lineTo(x, trackY + 8);
            c.strokeStyle = 'rgba(255, 220, 150, 0.7)';
            c.lineWidth = TICK_DB[i] === 0 ? 2 : 1;
            c.stroke();

            if (i < TICK_DB.length - 1) {
                for (var m = 1; m < 5; m++) {
                    var minordb = TICK_DB[i] + m * 2.5;
                    var mx = dbToFlatX(cx, minordb);
                    c.beginPath();
                    c.moveTo(mx, trackY - 4);
                    c.lineTo(mx, trackY + 4);
                    c.strokeStyle = 'rgba(200, 160, 80, 0.3)';
                    c.lineWidth = 0.5;
                    c.stroke();
                }
            }

            var lbl = TICK_DB[i] === 0 ? '0' : TICK_DB[i].toString();
            c.font = '9px system-ui, sans-serif';
            c.fillStyle = 'rgba(255, 210, 130, 0.8)';
            c.fillText(lbl, x, trackY + 24);
        }

        var db = levelToDb(level);
        var nx = dbToFlatX(cx, db);

        c.shadowColor = 'rgba(255, 200, 80, 0.9)';
        c.shadowBlur = 8;
        c.strokeStyle = '#ffe9b0';
        c.lineWidth = 2;
        c.beginPath();
        c.moveTo(nx, top);
        c.lineTo(nx, bottom);
        c.stroke();
        c.shadowBlur = 0;

        c.restore();
    }

    // ---- instances ----

    function createInstance(canvasId, style) {
        var canvas = document.getElementById(canvasId);
        if (!canvas) return null;
        var ctx = canvas.getContext('2d');
        canvas.width = CANVAS_WIDTH;
        canvas.height = CANVAS_HEIGHT;

        var state = {
            canvasId: canvasId,
            ctx: ctx,
            style: style,
            currentLeft: 0,
            currentRight: 0,
            targetLeft: 0,
            targetRight: 0,
            animFrame: null
        };

        instances[canvasId] = state;
        activeId = canvasId;

        function animate() {
            state.currentLeft += (state.targetLeft - state.currentLeft) * SMOOTHING;
            state.currentRight += (state.targetRight - state.currentRight) * SMOOTHING;
            if (Math.abs(state.currentLeft - state.targetLeft) < 0.0005) state.currentLeft = state.targetLeft;
            if (Math.abs(state.currentRight - state.targetRight) < 0.0005) state.currentRight = state.targetRight;

            var c = state.ctx;
            c.clearRect(0, 0, CANVAS_WIDTH, CANVAS_HEIGHT);

            if (state.style === 'flatgold') {
                drawFlatMeter(c, 10 + METER_WIDTH / 2, state.currentLeft);
                drawFlatMeter(c, 10 + METER_WIDTH + METER_GAP + METER_WIDTH / 2, state.currentRight);
            } else {
                var palette = ARC_PALETTES[state.style];
                drawArcMeter(c, 10 + METER_WIDTH / 2, state.currentLeft, palette);
                drawArcMeter(c, 10 + METER_WIDTH + METER_GAP + METER_WIDTH / 2, state.currentRight, palette);
            }

            state.animFrame = requestAnimationFrame(animate);
        }

        animate();

        return {
            setLevels: function (left, right) {
                state.targetLeft = clamp01(left);
                state.targetRight = clamp01(right);
            },
            destroy: function () {
                if (state.animFrame) cancelAnimationFrame(state.animFrame);
                delete instances[canvasId];
                if (activeId === canvasId) activeId = null;
            }
        };
    }

    function resolveStyle(style) {
        return VALID_STYLES.indexOf(style) >= 0 ? style : 'classic';
    }

    return {
        init: function (canvasId, style) {
            return createInstance(canvasId, resolveStyle(style));
        },

        setLevels: function (left, right) {
            var inst = instances[activeId];
            if (!inst) return;
            inst.targetLeft = clamp01(left);
            inst.targetRight = clamp01(right);
        },

        destroy: function () {
            if (activeId && instances[activeId]) {
                instances[activeId].destroy();
            }
        }
    };
})();