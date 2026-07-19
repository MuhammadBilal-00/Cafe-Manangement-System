/* ============================================================
   UI runtime (UI v3) — theme engine + shared micro-interactions.
   Loaded on every page after the DOM libraries; no dependencies.
   ============================================================ */
window.UI = (function () {
    'use strict';

    var THEMES = ['light', 'coffee', 'slate', 'dark', 'midnight'];
    var THEME_META = {
        light:    { label: 'Light',    icon: 'fa-sun' },
        coffee:   { label: 'Coffee',   icon: 'fa-mug-hot' },
        slate:    { label: 'Slate',    icon: 'fa-briefcase' },
        dark:     { label: 'Dark',     icon: 'fa-moon' },
        midnight: { label: 'Midnight', icon: 'fa-star' }
    };

    function currentTheme() {
        var t = document.documentElement.getAttribute('data-theme');
        return THEMES.indexOf(t) >= 0 ? t : 'light';
    }

    function setTheme(name) {
        if (THEMES.indexOf(name) < 0) name = 'light';
        document.documentElement.setAttribute('data-theme', name);
        try { localStorage.setItem('cafetheme', name); } catch (e) { /* private mode */ }
        window.dispatchEvent(new CustomEvent('themechange', { detail: { theme: name } }));
        syncPicker();
    }

    /* ── Theme picker popover (anchored to #themeToggle) ── */
    function syncPicker() {
        var t = currentTheme();
        var icon = document.getElementById('themeIcon');
        if (icon) icon.className = 'fas ' + THEME_META[t].icon;
        var pop = document.getElementById('themePopover');
        if (!pop) return;
        pop.querySelectorAll('[data-theme-option]').forEach(function (btn) {
            btn.classList.toggle('is-active', btn.getAttribute('data-theme-option') === t);
        });
    }

    function buildPicker() {
        var trigger = document.getElementById('themeToggle');
        if (!trigger) return;
        var wrap = trigger.parentElement;
        var pop = document.createElement('div');
        pop.id = 'themePopover';
        pop.className = 'theme-popover';
        pop.setAttribute('role', 'menu');
        pop.innerHTML = THEMES.map(function (t) {
            return '<button type="button" class="theme-option" role="menuitemradio" data-theme-option="' + t + '">' +
                '<span class="theme-swatch theme-swatch-' + t + '"><i></i><i></i><i></i></span>' +
                '<span class="theme-option-label">' + THEME_META[t].label + '</span>' +
                '<i class="fas fa-check theme-option-check"></i>' +
                '</button>';
        }).join('');
        wrap.appendChild(pop);

        pop.addEventListener('click', function (e) {
            var btn = e.target.closest('[data-theme-option]');
            if (!btn) return;
            setTheme(btn.getAttribute('data-theme-option'));
            close();
        });

        function open()  { pop.classList.add('is-open'); trigger.setAttribute('aria-expanded', 'true'); syncPicker(); }
        function close() { pop.classList.remove('is-open'); trigger.setAttribute('aria-expanded', 'false'); }

        trigger.addEventListener('click', function (e) {
            e.stopPropagation();
            pop.classList.contains('is-open') ? close() : open();
        });
        document.addEventListener('click', function (e) {
            if (pop.classList.contains('is-open') && !pop.contains(e.target)) close();
        });
        document.addEventListener('keydown', function (e) {
            if (e.key === 'Escape') close();
        });
        syncPicker();
    }

    /* ── Animated count-up for KPI values ([data-countup]) ──
       Progressive enhancement: the server-rendered text is already
       correct, so any parse failure or reduced-motion preference
       simply leaves it untouched. The final frame always restores
       the exact original string (no formatting drift). */
    function countUp() {
        if (window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches) return;
        document.querySelectorAll('[data-countup]').forEach(function (el) {
            var original = el.textContent;
            var m = original.match(/-?[\d][\d,]*(?:\.\d+)?/);
            if (!m) return;
            var numStr = m[0];
            var target = parseFloat(numStr.replace(/,/g, ''));
            if (!isFinite(target)) return;
            var decimals = (numStr.split('.')[1] || '').length;
            var grouped = numStr.indexOf(',') >= 0;
            var prefix = original.slice(0, m.index);
            var suffix = original.slice(m.index + numStr.length);
            var start = Math.abs(target) > 100 ? target * 0.6 : 0;
            var t0 = null, DURATION = 700;

            function fmt(v) {
                var s = v.toFixed(decimals);
                if (grouped) {
                    var parts = s.split('.');
                    parts[0] = parts[0].replace(/\B(?=(\d{3})+(?!\d))/g, ',');
                    s = parts.join('.');
                }
                return s;
            }
            function frame(ts) {
                if (t0 === null) t0 = ts;
                var p = Math.min((ts - t0) / DURATION, 1);
                var eased = 1 - Math.pow(1 - p, 3);
                if (p < 1) {
                    el.textContent = prefix + fmt(start + (target - start) * eased) + suffix;
                    requestAnimationFrame(frame);
                } else {
                    el.textContent = original; /* exact server string */
                }
            }
            requestAnimationFrame(frame);
        });
    }

    /* ── Modal helper for .modal-overlay shells (Phase 6+) ── */
    var modal = {
        open: function (id) {
            var m = document.getElementById(id);
            if (!m) return;
            m.classList.add('is-open');
            modal._last = document.activeElement;
            var f = m.querySelector('input, select, textarea, button:not(.modal-close)');
            if (f) f.focus();
        },
        close: function (id) {
            var m = document.getElementById(id);
            if (!m) return;
            m.classList.remove('is-open');
            if (modal._last && modal._last.focus) modal._last.focus();
        }
    };
    document.addEventListener('keydown', function (e) {
        if (e.key !== 'Escape') return;
        var open = document.querySelector('.modal-overlay.is-open');
        if (open && open.id) modal.close(open.id);
    });
    document.addEventListener('click', function (e) {
        if (e.target.classList && e.target.classList.contains('modal-overlay') && e.target.id) {
            modal.close(e.target.id);
        }
    });

    document.addEventListener('DOMContentLoaded', function () {
        buildPicker();
        countUp();
    });

    return { setTheme: setTheme, currentTheme: currentTheme, themes: THEMES, modal: modal };
})();
