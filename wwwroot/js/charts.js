/* ============================================================
   CafeCharts — theme-aware Chart.js runtime (UI v3)
   Loaded right after the Chart.js CDN script.

   Colors come from the CSS custom properties in tokens.css, so
   charts follow the active [data-theme] and tenant branding.
   Views register charts with a builder function:

       CafeCharts.make('canvasId', C => ({ type, data, options }))

   The server-rendered data consts stay exactly where they are —
   the builder closure captures them. On the `themechange` event
   every registered chart is destroyed and rebuilt from its
   builder so it repaints with the new theme's palette.
   ============================================================ */
window.CafeCharts = (function () {
    'use strict';

    var registry = new Map();   // canvasId -> builder
    var charts = new Map();     // canvasId -> Chart instance
    var rebuilding = false;

    function css(name) {
        return getComputedStyle(document.documentElement).getPropertyValue(name).trim();
    }
    function color(i) { return css('--chart-' + i); }                 // 1-based series slot
    function palette(n) {
        var out = [];
        for (var i = 1; i <= n; i++) out.push(color(((i - 1) % 8) + 1));
        return out;
    }
    function ink() {
        return {
            text: css('--text-secondary'),
            grid: css('--chart-grid'),
            axis: css('--chart-axis'),
            tick: css('--chart-tick'),
            tooltipBg: css('--chart-tooltip-bg'),
            tooltipText: css('--chart-tooltip-text')
        };
    }
    function emphasis() { return css('--chart-emphasis'); }
    function compare() { return css('--chart-compare'); }
    function accent() { return css('--accent'); }
    function status(name) { return css('--' + name); }                // success | warning | danger | info
    function surface() { return css('--bg-surface'); }
    function alpha(hex, a) {
        // #rrggbb -> rgba(); passes through non-hex (rgba/var results)
        var m = /^#([0-9a-f]{6})$/i.exec(hex);
        if (!m) return hex;
        var n = parseInt(m[1], 16);
        return 'rgba(' + (n >> 16 & 255) + ',' + (n >> 8 & 255) + ',' + (n & 255) + ',' + a + ')';
    }
    function reducedMotion() {
        return window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches;
    }

    function applyDefaults() {
        if (typeof Chart === 'undefined') return;
        var i = ink();
        var d = Chart.defaults;
        d.font.family = "'Inter', -apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif";
        d.font.size = 11;
        d.color = i.tick;
        d.borderColor = i.grid;
        d.animation = rebuilding || reducedMotion() ? false : { duration: 600, easing: 'easeOutQuart' };

        d.elements.line.borderWidth = 2;
        d.elements.line.tension = 0.35;
        d.elements.point.radius = 0;
        d.elements.point.hitRadius = 12;
        d.elements.point.hoverRadius = 4;
        d.elements.bar.borderRadius = 5;
        d.elements.bar.borderSkipped = 'bottom';
        d.elements.arc.borderWidth = 2;
        d.elements.arc.borderColor = surface();

        d.plugins.legend.labels.usePointStyle = true;
        d.plugins.legend.labels.boxWidth = 8;
        d.plugins.legend.labels.boxHeight = 8;
        d.plugins.legend.labels.padding = 14;

        d.plugins.tooltip.backgroundColor = i.tooltipBg;
        d.plugins.tooltip.titleColor = i.tooltipText;
        d.plugins.tooltip.bodyColor = i.tooltipText;
        d.plugins.tooltip.cornerRadius = 10;
        d.plugins.tooltip.padding = 10;
        d.plugins.tooltip.boxPadding = 4;
        d.plugins.tooltip.titleFont = { weight: '600' };
    }

    /* Shared scale fragments builders can spread in. Y-grid only —
       x gridlines add noise (charts keep their own ticks config). */
    function scales(opts) {
        var i = ink();
        var o = opts || {};
        return {
            x: {
                grid: { display: false },
                border: { color: i.axis },
                ticks: { color: i.tick, maxRotation: o.maxRotation != null ? o.maxRotation : 0, autoSkip: true }
            },
            y: {
                beginAtZero: true,
                grid: { color: i.grid },
                border: { display: false },
                ticks: { color: i.tick, precision: o.precision, callback: o.yTicks }
            }
        };
    }

    var api;

    function make(canvasId, builder) {
        var el = document.getElementById(canvasId);
        if (!el || typeof Chart === 'undefined') return null;
        var cfg = builder(api);
        if (!cfg) return null;
        var chart = new Chart(el, cfg);
        registry.set(canvasId, builder);
        charts.set(canvasId, chart);
        return chart;
    }

    function refresh() {
        rebuilding = true;
        applyDefaults();
        registry.forEach(function (builder, canvasId) {
            var old = charts.get(canvasId);
            if (old) old.destroy();
            var el = document.getElementById(canvasId);
            if (!el) { charts.delete(canvasId); registry.delete(canvasId); return; }
            charts.set(canvasId, new Chart(el, builder(api)));
        });
        rebuilding = false;
        applyDefaults();
    }

    api = {
        css: css, color: color, palette: palette, ink: ink,
        emphasis: emphasis, compare: compare, accent: accent,
        status: status, surface: surface, alpha: alpha,
        scales: scales, make: make, refresh: refresh, applyDefaults: applyDefaults
    };

    applyDefaults();
    window.addEventListener('themechange', refresh);
    return api;
})();
