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
    var VALID_STYLES = ['classic', 'glow', 'blueglow', 'greenglow', 'flatgold', 'ledbar', 'amberglow', 'oldschool'];

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
        },
        greenglow: {
            bg: '#0a0e1a',
            glowBorder: 'rgba(60, 220, 120, 0.3)',
            bgShadow: 'rgba(40, 180, 90, 0.3)',
            track: 'rgba(30, 90, 50, 0.18)',
            seg1: 'rgba(60, 200, 120, 0.35)',
            seg2: 'rgba(90, 230, 150, 0.55)',
            seg3: 'rgba(255, 90, 60, 0.6)',
            tick: 'rgba(170, 255, 200, 0.7)',
            minorTick: 'rgba(90, 200, 130, 0.3)',
            label: 'rgba(150, 235, 180, 0.8)',
            needleGlow: 'rgba(90, 230, 150, 0.8)',
            needle: '#d9ffeb',
            needleBlur: 9,
            pivot: '#8ff0b4',
            pivotGlow: true,
            glowCore: 'rgba(90, 230, 150, 0.58)',
            glowMid: 'rgba(60, 190, 110, 0.2)'
        },
        amberglow: {
            bg: '#1a1208',
            glowBorder: 'rgba(220, 160, 50, 0.35)',
            bgShadow: 'rgba(200, 140, 30, 0.4)',
            track: 'rgba(160, 110, 20, 0.15)',
            seg1: 'rgba(220, 170, 60, 0.35)',
            seg2: 'rgba(240, 190, 70, 0.5)',
            seg3: 'rgba(230, 80, 30, 0.6)',
            tick: 'rgba(230, 190, 100, 0.75)',
            minorTick: 'rgba(180, 140, 60, 0.3)',
            label: 'rgba(230, 190, 100, 0.85)',
            needleGlow: 'rgba(230, 180, 60, 0.8)',
            needle: '#ffe0a0',
            needleBlur: 6,
            pivot: '#d4a030',
            pivotGlow: true,
            glowCore: 'rgba(220, 160, 50, 0.5)',
            glowMid: 'rgba(180, 120, 30, 0.2)'
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

    // ---- old school flat style (oldschool) ----

    function drawOldSchoolMeter(c, cx, level) {
        var baseX = cx - METER_WIDTH / 2;
        var baseY = 5;
        var pivotX = cx;
        var pivotY = baseY + METER_HEIGHT - 6;
        var tickY = baseY + 42;
        var tickLen = 12;
        var minorTickLen = 6;

        c.save();

        var bgGrad = c.createLinearGradient(baseX, baseY, baseX, baseY + METER_HEIGHT);
        bgGrad.addColorStop(0, '#f7f0d8');
        bgGrad.addColorStop(0.35, '#f3ead0');
        bgGrad.addColorStop(1, '#e8dfc5');
        c.fillStyle = bgGrad;
        roundRect(c, baseX, baseY, METER_WIDTH, METER_HEIGHT, 5);
        c.fill();

        c.strokeStyle = 'rgba(180, 140, 60, 0.5)';
        c.lineWidth = 1.5;
        roundRect(c, baseX + 0.5, baseY + 0.5, METER_WIDTH - 1, METER_HEIGHT - 1, 5);
        c.stroke();

        var topGlow = c.createLinearGradient(baseX, baseY, baseX, baseY + 40);
        topGlow.addColorStop(0, 'rgba(255, 220, 140, 0.25)');
        topGlow.addColorStop(1, 'rgba(255, 220, 140, 0)');
        c.fillStyle = topGlow;
        c.fillRect(baseX, baseY, METER_WIDTH, 40);

        c.textAlign = 'center';
        c.textBaseline = 'middle';

        for (var i = 0; i < TICK_DB.length; i++) {
            var db = TICK_DB[i];
            var t = (db - TICK_DB[0]) / (TICK_DB[TICK_DB.length - 1] - TICK_DB[0]);
            var x = baseX + 14 + t * (METER_WIDTH - 28);

            var isRed = (db === -10 || db === 0);
            var tickColor = isRed ? '#c03030' : '#1a1510';
            var labelColor = isRed ? '#c03030' : '#1a1510';

            var angleFromPivot = Math.atan2(tickY - pivotY, x - pivotX);
            var cos = Math.cos(angleFromPivot);
            var sin = Math.sin(angleFromPivot);

            c.beginPath();
            c.moveTo(x - cos * tickLen / 2, tickY - sin * tickLen / 2);
            c.lineTo(x + cos * tickLen / 2, tickY + sin * tickLen / 2);
            c.strokeStyle = tickColor;
            c.lineWidth = isRed ? 1.8 : 1.2;
            c.stroke();

            if (i < TICK_DB.length - 1) {
                for (var m = 1; m < 5; m++) {
                    var minordb = db + m * 2.5;
                    var mt = (minordb - TICK_DB[0]) / (TICK_DB[TICK_DB.length - 1] - TICK_DB[0]);
                    var mx = baseX + 14 + mt * (METER_WIDTH - 28);
                    var mAngle = Math.atan2(tickY - pivotY, mx - pivotX);
                    var mcos = Math.cos(mAngle);
                    var msin = Math.sin(mAngle);
                    c.beginPath();
                    c.moveTo(mx - mcos * minorTickLen / 2, tickY - msin * minorTickLen / 2);
                    c.lineTo(mx + mcos * minorTickLen / 2, tickY + msin * minorTickLen / 2);
                    c.strokeStyle = 'rgba(30, 25, 15, 0.25)';
                    c.lineWidth = 0.6;
                    c.stroke();
                }
            }

            var lbl = db === 0 ? '0' : db.toString();
            c.font = '9px system-ui, sans-serif';
            c.fillStyle = labelColor;
            c.fillText(lbl, x, tickY - 14);
        }

        var curDb = levelToDb(level);
        var curT = (curDb - TICK_DB[0]) / (TICK_DB[TICK_DB.length - 1] - TICK_DB[0]);
        var needleX = baseX + 14 + curT * (METER_WIDTH - 28);

        c.shadowColor = 'rgba(40, 30, 15, 0.3)';
        c.shadowBlur = 4;
        c.strokeStyle = '#2a2018';
        c.lineWidth = 1.3;
        c.beginPath();
        c.moveTo(pivotX, pivotY);
        c.lineTo(needleX, tickY);
        c.stroke();
        c.shadowBlur = 0;

        c.beginPath();
        c.arc(pivotX, pivotY, 3, 0, 2 * Math.PI);
        c.fillStyle = '#2a2018';
        c.fill();

        c.restore();
    }

    // ---- LED bar style (ledbar) ----

    var LED_SEGMENTS = 24;
    var LED_BAR_X = 10;
    var LED_BAR_W = 360;
    var LED_BAR_H = 30;
    var LED_GAP = 2;
    var LED_SEG_W = (LED_BAR_W - (LED_SEGMENTS - 1) * LED_GAP) / LED_SEGMENTS;
    var LED_BAR_Y1 = 26;
    var LED_BAR_Y2 = 26 + LED_BAR_H + 14;

    var LED_ZONES = [
        { end: 0.6, lit: '#2ecc40', glow: 'rgba(46, 204, 64, 0.55)' },
        { end: 0.85, lit: '#ffd700', glow: 'rgba(255, 215, 0, 0.55)' },
        { end: 1.01, lit: '#ff4136', glow: 'rgba(255, 65, 54, 0.55)' }
    ];

    function ledZoneColor(index) {
        var t = (index + 0.5) / LED_SEGMENTS;
        for (var z = 0; z < LED_ZONES.length; z++) {
            if (t < LED_ZONES[z].end) return LED_ZONES[z];
        }
        return LED_ZONES[LED_ZONES.length - 1];
    }

    function drawLedBar(c, y, level) {
        c.save();

        c.fillStyle = '#0a0e1a';
        c.shadowColor = 'rgba(50, 200, 120, 0.25)';
        c.shadowBlur = 14;
        roundRect(c, LED_BAR_X, y, LED_BAR_W, LED_BAR_H, 4);
        c.fill();
        c.shadowBlur = 0;

        c.strokeStyle = 'rgba(120, 220, 160, 0.2)';
        c.lineWidth = 1;
        roundRect(c, LED_BAR_X + 1, y + 1, LED_BAR_W - 2, LED_BAR_H - 2, 3);
        c.stroke();

        var litCount = Math.round(((levelToDb(level) - TICK_DB[0]) / (TICK_DB[TICK_DB.length - 1] - TICK_DB[0])) * LED_SEGMENTS);
        for (var i = 0; i < LED_SEGMENTS; i++) {
            var x = LED_BAR_X + 3 + i * (LED_SEG_W + LED_GAP);
            var segW = LED_SEG_W - 2;
            var lit = i < litCount;
            var zone = ledZoneColor(i);
            if (lit) {
                c.shadowColor = zone.glow;
                c.shadowBlur = 5;
                c.fillStyle = zone.lit;
            } else {
                c.fillStyle = 'rgba(255, 255, 255, 0.05)';
            }
            roundRect(c, x, y + 4, segW, LED_BAR_H - 8, 2);
            c.fill();
            c.shadowBlur = 0;
        }

        c.restore();
    }

    // ---- instances ----

    function createInstance(canvasId, style) {
        if (instances[canvasId]) {
            try {
                if (typeof instances[canvasId].destroy === 'function') {
                    instances[canvasId].destroy();
                } else if (instances[canvasId].animFrame) {
                    cancelAnimationFrame(instances[canvasId].animFrame);
                    delete instances[canvasId];
                }
            } catch (e) {}
        }
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

        function destroy() {
            if (state.animFrame) cancelAnimationFrame(state.animFrame);
            delete instances[canvasId];
            if (activeId === canvasId) activeId = null;
        }

        state.destroy = destroy;
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
            } else if (state.style === 'ledbar') {
                drawLedBar(c, LED_BAR_Y1, state.currentLeft);
                drawLedBar(c, LED_BAR_Y2, state.currentRight);
            } else if (state.style === 'oldschool') {
                drawOldSchoolMeter(c, 10 + METER_WIDTH / 2, state.currentLeft);
                drawOldSchoolMeter(c, 10 + METER_WIDTH + METER_GAP + METER_WIDTH / 2, state.currentRight);
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
            destroy: destroy
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

        destroy: function (canvasId) {
            var targetId = canvasId || activeId;
            if (targetId && instances[targetId]) {
                if (typeof instances[targetId].destroy === 'function') {
                    instances[targetId].destroy();
                } else {
                    if (instances[targetId].animFrame) cancelAnimationFrame(instances[targetId].animFrame);
                    delete instances[targetId];
                }
            }
            if (!canvasId || activeId === canvasId) {
                activeId = null;
            }
        }
    };
})();
window.vuMeter = vuMeter;