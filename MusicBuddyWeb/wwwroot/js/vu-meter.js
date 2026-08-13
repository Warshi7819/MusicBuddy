var vuMeter = (function () {
    var canvas, ctx;
    var animFrame = null;
    var currentLeft = 0;
    var currentRight = 0;
    var targetLeft = 0;
    var targetRight = 0;

    var SMOOTHING = 0.25;

    var METER_WIDTH = 155;
    var METER_HEIGHT = 105;
    var METER_GAP = 50;
    var CANVAS_WIDTH = METER_WIDTH * 2 + METER_GAP + 20;
    var CANVAS_HEIGHT = METER_HEIGHT + 10;

    var ARC_START_ANGLE = Math.PI + 0.35;
    var ARC_END_ANGLE = 2 * Math.PI - 0.35;
    var ARC_RADIUS = 72;
    var PIVOT_X, PIVOT_Y;
    var NEEDLE_LENGTH = 64;

    var TICK_DB = [-60, -50, -40, -30, -20, -10, 0];

    function dbToAngle(db) {
        var t = (db - TICK_DB[0]) / (TICK_DB[TICK_DB.length - 1] - TICK_DB[0]);
        return ARC_START_ANGLE + t * (ARC_END_ANGLE - ARC_START_ANGLE);
    }

    function levelToDb(level) {
        if (level <= 0) return TICK_DB[0];
        var db = 20 * Math.log10(level);
        return Math.max(TICK_DB[0], Math.min(0, db));
    }

    function drawMeter(cx, level) {
        var baseX = cx - METER_WIDTH / 2;
        var baseY = 5;

        ctx.save();

        // background
        ctx.fillStyle = '#0a0e1a';
        ctx.shadowColor = 'rgba(40, 100, 200, 0.4)';
        ctx.shadowBlur = 18;
        roundRect(ctx, baseX, baseY, METER_WIDTH, METER_HEIGHT, 5);
        ctx.fill();
        ctx.shadowBlur = 0;

        // inner glow border
        ctx.strokeStyle = 'rgba(50, 120, 220, 0.25)';
        ctx.lineWidth = 1;
        roundRect(ctx, baseX + 1, baseY + 1, METER_WIDTH - 2, METER_HEIGHT - 2, 4);
        ctx.stroke();

        PIVOT_X = cx;
        PIVOT_Y = baseY + METER_HEIGHT - 10;

        // arc background (dark track)
        ctx.beginPath();
        ctx.arc(PIVOT_X, PIVOT_Y, ARC_RADIUS, ARC_START_ANGLE, ARC_END_ANGLE);
        ctx.strokeStyle = 'rgba(40, 80, 160, 0.15)';
        ctx.lineWidth = 8;
        ctx.stroke();

        // colored arc segments
        drawArcSegment(PIVOT_X, PIVOT_Y, ARC_RADIUS, -60, -20, 'rgba(40, 140, 255, 0.35)');
        drawArcSegment(PIVOT_X, PIVOT_Y, ARC_RADIUS, -20, -6, 'rgba(40, 180, 255, 0.5)');
        drawArcSegment(PIVOT_X, PIVOT_Y, ARC_RADIUS, -6, 0, 'rgba(255, 80, 60, 0.6)');

        // tick marks and labels
        ctx.textAlign = 'center';
        ctx.textBaseline = 'middle';
        for (var i = 0; i < TICK_DB.length; i++) {
            var angle = dbToAngle(TICK_DB[i]);
            var cos = Math.cos(angle);
            var sin = Math.sin(angle);

            var outerR = ARC_RADIUS + 6;
            var innerR = ARC_RADIUS - 6;
            var labelR = ARC_RADIUS - 18;

            // tick line
            ctx.beginPath();
            ctx.moveTo(PIVOT_X + cos * innerR, PIVOT_Y + sin * innerR);
            ctx.lineTo(PIVOT_X + cos * outerR, PIVOT_Y + sin * outerR);
            ctx.strokeStyle = 'rgba(160, 200, 255, 0.7)';
            ctx.lineWidth = TICK_DB[i] === 0 ? 2 : 1;
            ctx.stroke();

            // minor ticks between major ticks
            if (i < TICK_DB.length - 1) {
                for (var m = 1; m < 5; m++) {
                    var minordb = TICK_DB[i] + m * 2.5;
                    var minorAngle = dbToAngle(minordb);
                    var mcos = Math.cos(minorAngle);
                    var msin = Math.sin(minorAngle);
                    ctx.beginPath();
                    ctx.moveTo(PIVOT_X + mcos * (ARC_RADIUS - 3), PIVOT_Y + msin * (ARC_RADIUS - 3));
                    ctx.lineTo(PIVOT_X + mcos * (ARC_RADIUS + 3), PIVOT_Y + msin * (ARC_RADIUS + 3));
                    ctx.strokeStyle = 'rgba(100, 160, 240, 0.3)';
                    ctx.lineWidth = 0.5;
                    ctx.stroke();
                }
            }

            // label
            var lbl = TICK_DB[i] === 0 ? '0' : TICK_DB[i].toString();
            ctx.font = '9px system-ui, sans-serif';
            ctx.fillStyle = 'rgba(140, 190, 255, 0.8)';
            ctx.fillText(lbl, PIVOT_X + cos * labelR, PIVOT_Y + sin * labelR);
        }

        // needle
        var db = levelToDb(level);
        var needleAngle = dbToAngle(db);
        var nx = Math.cos(needleAngle);
        var ny = Math.sin(needleAngle);

        // needle glow
        ctx.beginPath();
        ctx.moveTo(PIVOT_X, PIVOT_Y);
        ctx.lineTo(PIVOT_X + nx * NEEDLE_LENGTH, PIVOT_Y + ny * NEEDLE_LENGTH);
        ctx.strokeStyle = 'rgba(60, 140, 255, 0.3)';
        ctx.lineWidth = 3;
        ctx.stroke();

        // needle body
        ctx.beginPath();
        ctx.moveTo(PIVOT_X, PIVOT_Y);
        ctx.lineTo(PIVOT_X + nx * NEEDLE_LENGTH, PIVOT_Y + ny * NEEDLE_LENGTH);
        ctx.strokeStyle = '#c8ddf8';
        ctx.lineWidth = 1.2;
        ctx.stroke();

        // pivot dot
        ctx.beginPath();
        ctx.arc(PIVOT_X, PIVOT_Y, 3, 0, 2 * Math.PI);
        ctx.fillStyle = '#8ab4f0';
        ctx.fill();

        ctx.restore();
    }

    function drawArcSegment(cx, cy, r, startDb, endDb, color) {
        ctx.beginPath();
        ctx.arc(cx, cy, r, dbToAngle(startDb), dbToAngle(endDb));
        ctx.strokeStyle = color;
        ctx.lineWidth = 8;
        ctx.stroke();
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

    function animate() {
        currentLeft += (targetLeft - currentLeft) * SMOOTHING;
        currentRight += (targetRight - currentRight) * SMOOTHING;

        if (Math.abs(currentLeft - targetLeft) < 0.0005) currentLeft = targetLeft;
        if (Math.abs(currentRight - targetRight) < 0.0005) currentRight = targetRight;

        ctx.clearRect(0, 0, CANVAS_WIDTH, CANVAS_HEIGHT);

        drawMeter(10 + METER_WIDTH / 2, currentLeft);
        drawMeter(10 + METER_WIDTH + METER_GAP + METER_WIDTH / 2, currentRight);

        animFrame = requestAnimationFrame(animate);
    }

    return {
        init: function (canvasId) {
            canvas = document.getElementById(canvasId);
            if (!canvas) return;
            ctx = canvas.getContext('2d');
            canvas.width = CANVAS_WIDTH;
            canvas.height = CANVAS_HEIGHT;
            animate();
        },

        setLevels: function (left, right) {
            targetLeft = Math.max(0, Math.min(1, left));
            targetRight = Math.max(0, Math.min(1, right));
        },

        destroy: function () {
            if (animFrame) cancelAnimationFrame(animFrame);
        }
    };
})();
