/* Observatorio Bufins – utilidades compartidas (formato, API, buscador de empresas, gráficos) */
(function () {
    'use strict';
    var ES = document.documentElement.lang !== 'en';
    var LOC = ES ? 'es-CO' : 'en-US';
    var NUM1 = new Intl.NumberFormat(LOC, { maximumFractionDigits: 1 });
    var NUM2 = new Intl.NumberFormat(LOC, { maximumFractionDigits: 2 });
    var INT = new Intl.NumberFormat(LOC, { maximumFractionDigits: 0 });

    var OBS = window.OBS = {};
    OBS.ES = ES;
    OBS.PALETTE = ['#583AFF', '#01E1D7', '#FF5500', '#160933', '#8F7BFF', '#14B8A6', '#FF9A66', '#4B3A7A', '#A78BFA', '#0EA5E9', '#F59E0B', '#94A3B8'];
    OBS.CMP = ['#583AFF', '#FF5500', '#01B3AB', '#160933'];   // colores fijos para comparar hasta 4 empresas

    OBS.L = function (es, en) { return ES ? es : en; };
    OBS.$ = function (id) { return document.getElementById(id); };
    OBS.esc = function (s) { return String(s == null ? '' : s).replace(/[&<>"']/g, function (c) { return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]; }); };

    // ---------- formato: la entrada de dinero siempre es MILLONES de COP ----------
    OBS.money = function (m, compact) {
        if (m == null || isNaN(m)) return '—';
        var a = Math.abs(m), v;
        if (a >= 1e6) v = new Intl.NumberFormat(LOC, { maximumFractionDigits: a >= 1e8 ? 0 : 2 }).format(m / 1e6) + (ES ? ' B' : ' T');
        else if (a >= 1e3) v = NUM1.format(m / 1e3) + (ES ? ' mil M' : ' B');
        else v = NUM1.format(m) + ' M';
        return (compact ? '' : '$ ') + v;
    };
    OBS.full = function (m) { return m == null ? '—' : INT.format(Math.round(m)); };      // millones sin abreviar (tablas)
    OBS.pct = function (v) { return v == null || isNaN(v) ? '—' : NUM1.format(v) + '%'; };
    OBS.x = function (v) { return v == null || isNaN(v) ? '—' : NUM1.format(v) + 'x'; };
    OBS.days = function (v) { return v == null || isNaN(v) ? '—' : INT.format(Math.round(v)); };
    OBS.num = function (v) { return v == null || isNaN(v) ? '—' : NUM2.format(v); };
    OBS.int = function (v) { return INT.format(v); };
    OBS.unitFor = function (max) {
        if (max >= 1e6) return { d: 1e6, l: ES ? 'billones COP' : 'COP trillions' };
        if (max >= 1e3) return { d: 1e3, l: ES ? 'mil millones COP' : 'COP billions' };
        return { d: 1, l: ES ? 'millones COP' : 'COP millions' };
    };
    OBS.delta = function (cur, prev) {
        if (prev == null || prev <= 0 || cur == null) return '';
        var v = (cur - prev) / prev * 100;
        return '<em class="' + (v >= 0 ? 'up' : 'down') + '">' + (v >= 0 ? '▲ ' : '▼ ') + NUM1.format(Math.abs(v)) + '% ' + OBS.L('vs. año anterior', 'vs. prior year') + '</em>';
    };
    OBS.debounce = function (fn, ms) { var t; return function () { var a = arguments, c = this; clearTimeout(t); t = setTimeout(function () { fn.apply(c, a); }, ms); }; };

    // La versión de los datos (v) va en cada llamada: las respuestas se cachean en el navegador y, al publicar datos nuevos, la URL cambia sola.
    var META = document.getElementById('obsMeta');
    var VER = META ? META.getAttribute('data-v') : '';
    OBS.api = function (path, qs, opts) {
        var q = (qs ? qs + '&' : '') + 'v=' + encodeURIComponent(VER);
        return fetch('/observatorio/api/' + path + '?' + q, opts || {}).then(function (r) { if (!r.ok) throw new Error(r.status); return r.json(); });
    };


    // Botón «Copiar enlace»: copia la URL actual (con los filtros) y confirma con un cambio de texto temporal
    OBS.share = function (btn) {
        if (!btn) return;
        var html = btn.innerHTML;
        btn.addEventListener('click', function (e) {
            e.preventDefault();
            var url = location.href;
            (navigator.clipboard ? navigator.clipboard.writeText(url) : Promise.reject()).then(function () {
                btn.innerHTML = '<i class="fas fa-check me-2"></i>' + OBS.L('¡Copiado!', 'Copied!');
                setTimeout(function () { btn.innerHTML = html; }, 1800);
            }).catch(function () { window.prompt(OBS.L('Copia el enlace:', 'Copy the link:'), url); });
        });
    };

    // Tarjeta de indicador (KPI)
    OBS.kpi = function (label, value, sub, cls) {
        return '<div class="obs-kpi ' + (cls || '') + '"><span>' + label + '</span><strong>' + value + '</strong>' + (sub || '') + '</div>';
    };

    // ---------- almacenamiento local seguro ----------
    function store(k, v) { try { if (v === undefined) { return JSON.parse(localStorage.getItem(k) || 'null'); } localStorage.setItem(k, JSON.stringify(v)); } catch (e) { return null; } }
    OBS.recent = function () { return store('obs_recent') || []; };
    OBS.addRecent = function (c) {
        var r = OBS.recent().filter(function (x) { return x.nit !== c.nit; });
        r.unshift({ nit: c.nit, razon: c.razon });
        store('obs_recent', r.slice(0, 6));
    };

    // ---------- buscador de empresas con autocompletado ----------
    // OBS.search(input, { onSelect: function(item){...} })  -> sin onSelect navega a la ficha de la empresa
    OBS.search = function (input, opts) {
        opts = opts || {};
        var box = document.createElement('div');
        box.className = 'obs-suggest'; box.hidden = true; box.setAttribute('role', 'listbox');
        input.parentNode.style.position = 'relative';
        input.parentNode.appendChild(box);
        input.setAttribute('autocomplete', 'off'); input.setAttribute('role', 'combobox'); input.setAttribute('aria-expanded', 'false');
        var items = [], sel = -1, seq = 0;

        function close() { box.hidden = true; input.setAttribute('aria-expanded', 'false'); sel = -1; }
        function choose(i) {
            var it = items[i]; if (!it) return;
            OBS.addRecent(it); close();
            if (opts.onSelect) { input.value = opts.clear === false ? it.razon : ''; opts.onSelect(it); }
            else location.href = '/observatorio/empresa/' + it.nit;
        }
        function paint() {
            box.innerHTML = items.map(function (it, i) {
                var meta = [it.nit ? 'NIT ' + it.nit : '', it.actividad, it.ciudad].filter(Boolean).map(OBS.esc).join(' · ');
                return '<div class="obs-sg-item' + (i === sel ? ' on' : '') + '" role="option" data-i="' + i + '">' +
                    '<strong>' + OBS.esc(it.razon) + '</strong><small>' + meta + '</small>' +
                    (it.ing != null ? '<span class="obs-sg-ing">' + OBS.money(it.ing) + '<i>' + (it.tam || '') + '</i></span>' : '') + '</div>';
            }).join('') || '<div class="obs-sg-empty">' + OBS.L('Sin resultados. Prueba con el NIT o parte del nombre.', 'No results. Try the tax ID or part of the name.') + '</div>';
            box.hidden = false; input.setAttribute('aria-expanded', 'true');
        }
        function showRecent() {
            var r = OBS.recent(); if (!r.length) { close(); return; }
            items = r; sel = -1;
            box.innerHTML = '<div class="obs-sg-head">' + OBS.L('Consultadas recientemente', 'Recently viewed') + '</div>' + r.map(function (it, i) {
                return '<div class="obs-sg-item" role="option" data-i="' + i + '"><strong>' + OBS.esc(it.razon) + '</strong><small>NIT ' + OBS.esc(it.nit) + '</small></div>';
            }).join('');
            box.hidden = false;
        }
        var run = OBS.debounce(function () {
            var q = input.value.trim(), my = ++seq;
            if (q.length < 2) { showRecent(); return; }
            OBS.api('buscar', 'q=' + encodeURIComponent(q) + '&n=8').then(function (r) { if (my !== seq) return; items = r; sel = r.length ? 0 : -1; paint(); }).catch(close);
        }, 180);

        input.addEventListener('input', run);
        input.addEventListener('focus', function () { if (input.value.trim().length < 2) showRecent(); });
        input.addEventListener('keydown', function (e) {
            if (box.hidden && e.key === 'ArrowDown') { run(); return; }
            if (e.key === 'ArrowDown') { e.preventDefault(); sel = Math.min(items.length - 1, sel + 1); paint(); }
            else if (e.key === 'ArrowUp') { e.preventDefault(); sel = Math.max(0, sel - 1); paint(); }
            else if (e.key === 'Enter') { if (sel >= 0) { e.preventDefault(); choose(sel); } }
            else if (e.key === 'Escape') close();
        });
        box.addEventListener('mousedown', function (e) { var d = e.target.closest('.obs-sg-item'); if (d) { e.preventDefault(); choose(parseInt(d.getAttribute('data-i'), 10)); } });
        document.addEventListener('click', function (e) { if (!box.contains(e.target) && e.target !== input) close(); });
        return { close: close };
    };

    // ---------- gráficos ----------
    var charts = {};
    OBS.chart = function (id, cfg) {
        if (charts[id]) charts[id].destroy();
        var el = OBS.$(id); if (!el) return null;
        charts[id] = new Chart(el, cfg);
        return charts[id];
    };
    OBS.resizeCharts = function () { Object.keys(charts).forEach(function (k) { charts[k].resize(); }); };
    OBS.COMMON = { responsive: true, maintainAspectRatio: false };

    /**
     * Gráfico combinado como en los tableros financieros: barras (valor) + línea (indicador %).
     * anios: etiquetas; barra: {label,data}; linea: {label,data,fmt}; resaltar: año a destacar; color: color base de la barra.
     */
    OBS.combo = function (id, anios, barra, linea, resaltar, color) {
        color = color || '#583AFF';
        var max = Math.max.apply(null, barra.data.map(function (v) { return v == null ? 0 : Math.abs(v); }).concat([1]));
        var u = OBS.unitFor(max);
        var hex = color;
        var datasets = [{
            type: 'bar', label: barra.label + ' (' + u.l + ')', data: barra.data.map(function (v) { return v == null ? null : v / u.d; }), yAxisID: 'y', borderRadius: 6,
            backgroundColor: anios.map(function (a) { return resaltar && a !== resaltar ? hex + '59' : hex; })
        }];
        if (linea) datasets.push({ type: 'line', label: linea.label, data: linea.data, borderColor: '#FF5500', backgroundColor: '#FF5500', tension: .3, yAxisID: 'y1', spanGaps: true });
        var fmtLine = linea && linea.fmt ? linea.fmt : function (v) { return v + '%'; };
        return OBS.chart(id, {
            data: { labels: anios, datasets: datasets },
            options: Object.assign({}, OBS.COMMON, {
                plugins: {
                    legend: { position: 'bottom', labels: { boxWidth: 12 } },
                    tooltip: { callbacks: { label: function (c) { return c.dataset.yAxisID === 'y1' ? c.dataset.label + ': ' + fmtLine(NUM1.format(c.parsed.y)) : barra.label + ': ' + NUM2.format(c.parsed.y) + ' ' + u.l; } } }
                },
                scales: { y: { beginAtZero: true }, y1: { position: 'right', suggestedMin: 0, grid: { drawOnChartArea: false }, ticks: { callback: function (v) { return fmtLine(v); } }, display: !!linea } }
            })
        });
    };

    // Pestañas de Bootstrap: redimensionar gráficos al mostrarse
    document.addEventListener('shown.bs.tab', function () { OBS.resizeCharts(); });

    // Buscador global del sub-menú
    var g = OBS.$('obsGlobalSearch');
    if (g) OBS.search(g);
})();
