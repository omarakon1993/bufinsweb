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

    // ---------- unidad de las cifras: la entrada de dinero siempre es MILLONES de COP ----------
    // d = cuántos millones vale una unidad (valor en la unidad = millones / d). "auto" elige la unidad según el tamaño de la cifra.
    var UNITS = {
        auto: { d: 0 },
        pesos: { d: 1e-6, es: 'pesos COP', en: 'COP', sEs: '', sEn: '' },
        miles: { d: 1e-3, es: 'miles de COP', en: 'COP thousands', sEs: ' miles', sEn: ' K' },
        millones: { d: 1, es: 'millones COP', en: 'COP millions', sEs: ' M', sEn: ' M' },
        milmillones: { d: 1e3, es: 'mil millones COP', en: 'COP billions', sEs: ' mil M', sEn: ' B' },
        billones: { d: 1e6, es: 'billones COP', en: 'COP trillions', sEs: ' B', sEn: ' T' }
    };
    var UNIT = 'auto';
    try { var su = localStorage.getItem('obs_unit'); if (su && UNITS[su]) UNIT = su; } catch (e) { /* sin almacenamiento: queda en automático */ }
    OBS.unit = function () { return UNIT; };
    OBS.setUnit = function (u) {
        if (!UNITS[u]) return; UNIT = u;
        OBS.track('unidad', u);
        try { localStorage.setItem('obs_unit', u); } catch (e) { }
        OBS.syncUnitUi();
        document.dispatchEvent(new Event('obs:unit'));
    };
    // Texto de la unidad vigente ("Millones de COP" cuando es automática, porque las tablas completas van en millones)
    OBS.unitLabel = function () { var x = UNITS[UNIT === 'auto' ? 'millones' : UNIT]; return ES ? x.es : x.en; };
    OBS.syncUnitUi = function () {
        var sel = document.getElementById('obsUnit'); if (sel) sel.value = UNIT;
        Array.prototype.forEach.call(document.querySelectorAll('[data-unit-label]'), function (el) { el.textContent = OBS.unitLabel(); });
    };
    function digits(v) { var a = Math.abs(v); return a >= 1000 ? 0 : a >= 100 ? 1 : 2; }
    function nf(v) { return new Intl.NumberFormat(LOC, { maximumFractionDigits: digits(v) }).format(v); }

    OBS.money = function (m, compact) {
        if (m == null || isNaN(m)) return '—';
        var v, u = UNITS[UNIT];
        if (UNIT !== 'auto') {
            v = nf(m / u.d) + (ES ? u.sEs : u.sEn);
        } else {
            var a = Math.abs(m);
            if (a >= 1e6) v = new Intl.NumberFormat(LOC, { maximumFractionDigits: a >= 1e8 ? 0 : 2 }).format(m / 1e6) + (ES ? ' B' : ' T');
            else if (a >= 1e3) v = NUM1.format(m / 1e3) + (ES ? ' mil M' : ' B');
            else v = NUM1.format(m) + ' M';
        }
        return (compact ? '' : '$ ') + v;
    };
    OBS.full = function (m) { return m == null ? '—' : UNIT === 'auto' || UNIT === 'millones' ? INT.format(Math.round(m)) : nf(m / UNITS[UNIT].d); };      // cifra sin abreviar (tablas)
    OBS.pct = function (v) { return v == null || isNaN(v) ? '—' : NUM1.format(v) + '%'; };
    OBS.x = function (v) { return v == null || isNaN(v) ? '—' : NUM1.format(v) + 'x'; };
    OBS.days = function (v) { return v == null || isNaN(v) ? '—' : INT.format(Math.round(v)); };
    OBS.num = function (v) { return v == null || isNaN(v) ? '—' : NUM2.format(v); };
    OBS.int = function (v) { return INT.format(v); };
    OBS.unitFor = function (max) {
        if (UNIT !== 'auto') return { d: UNITS[UNIT].d, l: ES ? UNITS[UNIT].es : UNITS[UNIT].en };
        if (max >= 1e6) return { d: 1e6, l: ES ? 'billones COP' : 'COP trillions' };
        if (max >= 1e3) return { d: 1e3, l: ES ? 'mil millones COP' : 'COP billions' };
        return { d: 1, l: ES ? 'millones COP' : 'COP millions' };
    };
    OBS.delta = function (cur, prev) {
        if (prev == null || prev <= 0 || cur == null) return '';
        var v = (cur - prev) / prev * 100;
        return '<em class="' + (v >= 0 ? 'up' : 'down') + '">' + (v >= 0 ? '▲ ' : '▼ ') + NUM1.format(Math.abs(v)) + '% ' + OBS.L('vs. año anterior', 'vs. prior year') + '</em>';
    };
    // Analítica anónima: solo el tipo de evento y una clave corta (sin identificar a la persona); el servidor lleva contadores por día.
    // Parámetros opcionales: x = detalle corto (letras, números y , - _ . | /), extra = { ms, sc, tz, w } (permanencia, scroll, zona horaria, ancho).
    OBS.track = function (tipo, clave, x, extra) {
        try {
            var p = { t: tipo, k: String(clave || '').slice(0, 40) };
            if (x) p.x = String(x).slice(0, 100);
            if (extra) for (var n in extra) if (extra[n] != null && extra[n] !== '') p[n] = String(extra[n]);
            var body = new URLSearchParams(p);
            if (navigator.sendBeacon) navigator.sendBeacon('/observatorio/api/evento', body);
            else fetch('/observatorio/api/evento', { method: 'POST', body: body, keepalive: true });
        } catch (e) { /* la analítica nunca debe romper la página */ }
    };
    document.addEventListener('click', function (e) {
        var el = e.target.closest ? e.target.closest('[data-track]') : null;
        if (el) OBS.track(el.getAttribute('data-track'), el.getAttribute('data-key') || '', el.getAttribute('data-detail') || '');
        // Salidas a otros sitios: solo se guarda el dominio de destino
        var a = e.target.closest ? e.target.closest('a[href]') : null;
        if (a && /^https?:/i.test(a.href) && a.hostname !== location.hostname && !/(^|\.)bufins\.com$/i.test(a.hostname))
            OBS.track('outbound', a.hostname.replace(/^www\./, '').replace(/[^a-z0-9.\-]/gi, '').toLowerCase().slice(0, 40));
    });
    // Permanencia en la página y hasta dónde se desplazó (se envía una sola vez al salir o al pasar a segundo plano)
    (function () {
        var ini = Date.now(), acum = 0, visible = !document.hidden, max = 0, enviado = false;
        function scroll() {
            var h = document.documentElement, total = Math.max(1, h.scrollHeight - h.clientHeight);
            max = Math.max(max, Math.min(100, Math.round(((window.pageYOffset || h.scrollTop) / total) * 100)));
        }
        window.addEventListener('scroll', scroll, { passive: true });
        function corte() { if (visible) { acum += Date.now() - ini; } }
        function enviar() {
            if (enviado) return;
            corte();
            if (acum < 1500) return;               // visitas fantasma de menos de 1,5 s no cuentan
            enviado = true;
            var tz = ''; try { tz = Intl.DateTimeFormat().resolvedOptions().timeZone || ''; } catch (e) { }
            OBS.track('perm', (location.pathname.split('/')[2] || 'inicio').replace(/[^a-z0-9]/gi, '').toLowerCase().slice(0, 20), '', { ms: Math.min(acum, 3600000), sc: max, tz: tz, w: window.innerWidth });
        }
        document.addEventListener('visibilitychange', function () {
            if (document.hidden) { corte(); visible = false; enviar(); }
            else { visible = true; ini = Date.now(); enviado = false; acum = 0; }
        });
        window.addEventListener('pagehide', enviar);
    })();
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
            OBS.track('copiar_enlace', (location.pathname.split('/')[2] || 'inicio').slice(0, 20));
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
        r.unshift({ nit: c.nit, razon: c.razon, url: c.url });
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
            else { OBS.track('busqueda_sel', it.nit || '', '', null); location.href = it.url || ('/observatorio/empresa/' + it.nit); }
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
    var LBL = {};   // gráficos con etiquetas visibles (se recuerda la elección)
    try { LBL = JSON.parse(localStorage.getItem('obs_labels') || '{}') || {}; } catch (e) { LBL = {}; }
    var CMP = new Intl.NumberFormat(LOC, { notation: 'compact', maximumFractionDigits: 1 });
    function fmtLabel(v) { var a = Math.abs(v); return a >= 1e4 ? CMP.format(v) : new Intl.NumberFormat(LOC, { maximumFractionDigits: a >= 100 ? 0 : a >= 10 ? 1 : 2 }).format(v); }
    OBS.fmtTick = function (v) { return Math.abs(v) >= 1e4 ? CMP.format(v) : v; };

    // Plugin propio: escribe el valor sobre cada barra, punto o porción cuando el gráfico tiene las etiquetas activadas.
    // Un dataset puede traer obsFmt (formato del valor) u obsLabels (textos propios por posición, función o arreglo).
    if (typeof Chart !== 'undefined') Chart.register({
        id: 'obsLabels',
        afterDatasetsDraw: function (chart) {
            if (!chart.canvas || !LBL[chart.canvas.id]) return;
            var ctx = chart.ctx, horiz = chart.options.indexAxis === 'y';
            ctx.save(); ctx.font = '600 11px Inter, system-ui, sans-serif'; ctx.lineJoin = 'round'; ctx.lineWidth = 3; ctx.strokeStyle = 'rgba(255,255,255,.92)'; ctx.fillStyle = '#160933';
            chart.data.datasets.forEach(function (ds, di) {
                var meta = chart.getDatasetMeta(di); if (meta.hidden) return;
                meta.data.forEach(function (el, i) {
                    var raw = ds.data[i], txt;
                    if (ds.obsLabels) txt = typeof ds.obsLabels === 'function' ? ds.obsLabels(i) : ds.obsLabels[i];
                    else if (raw == null || typeof raw === 'object') return;
                    else txt = ds.obsFmt ? ds.obsFmt(raw) : fmtLabel(raw);
                    if (txt == null || txt === '') return;
                    var pos = meta.type === 'doughnut' ? el.tooltipPosition() : { x: el.x, y: el.y };
                    var x = pos.x, y = pos.y, neg = typeof raw === 'number' && raw < 0;
                    if (meta.type === 'bar') { if (horiz) { ctx.textAlign = 'left'; x += 4; y += 4; } else { ctx.textAlign = 'center'; y += neg ? 14 : -5; } }
                    else if (meta.type === 'line') { ctx.textAlign = 'center'; y -= 8; }
                    else if (meta.type === 'bubble') { ctx.textAlign = 'center'; y -= (el.options && el.options.radius || 6) + 3; }
                    else { ctx.textAlign = 'center'; y += 4; }
                    ctx.strokeText(txt, x, y); ctx.fillText(txt, x, y);
                });
            });
            ctx.restore();
        }
    });

    function labelBtn(id, el) {
        var host = el.parentNode;
        if (!host || !host.classList.contains('obs-chart')) return;
        var b = host.querySelector('.obs-lblbtn');
        function sync() {
            var on = !!LBL[id];
            b.classList.toggle('on', on); b.setAttribute('aria-pressed', on ? 'true' : 'false');
            b.title = on ? OBS.L('Ocultar etiquetas', 'Hide labels') : OBS.L('Ver etiquetas', 'Show labels'); b.setAttribute('aria-label', b.title);
        }
        if (!b) {
            b = document.createElement('button'); b.type = 'button'; b.className = 'obs-lblbtn'; b.innerHTML = '<i class="fas fa-tags" aria-hidden="true"></i>';
            b.addEventListener('click', function () {
                LBL[id] = !LBL[id];
                try { localStorage.setItem('obs_labels', JSON.stringify(LBL)); } catch (e) { }
                sync(); if (charts[id]) charts[id].update('none');
            });
            host.appendChild(b);
        }
        sync();
    }

    OBS.chart = function (id, cfg) {
        if (charts[id]) charts[id].destroy();
        var el = OBS.$(id); if (!el) return null;
        charts[id] = new Chart(el, cfg);
        labelBtn(id, el);
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
        if (linea) datasets.push({ type: 'line', obsFmt: linea.fmt || function (v) { return fmtLabel(v) + '%'; }, label: linea.label, data: linea.data, borderColor: '#FF5500', backgroundColor: '#FF5500', tension: .3, yAxisID: 'y1', spanGaps: true });
        var fmtLine = linea && linea.fmt ? linea.fmt : function (v) { return v + '%'; };
        return OBS.chart(id, {
            data: { labels: anios, datasets: datasets },
            options: Object.assign({}, OBS.COMMON, {
                plugins: {
                    legend: { position: 'bottom', labels: { boxWidth: 12 } },
                    tooltip: { callbacks: { label: function (c) { return c.dataset.yAxisID === 'y1' ? c.dataset.label + ': ' + fmtLine(NUM1.format(c.parsed.y)) : barra.label + ': ' + NUM2.format(c.parsed.y) + ' ' + u.l; } } }
                },
                scales: { y: { beginAtZero: true, ticks: { callback: OBS.fmtTick } }, y1: { position: 'right', suggestedMin: 0, grid: { drawOnChartArea: false }, ticks: { callback: function (v) { return fmtLine(v); } }, display: !!linea } }
            })
        });
    };

    // Pestañas de Bootstrap: redimensionar gráficos al mostrarse
    document.addEventListener('shown.bs.tab', function () { OBS.resizeCharts(); });

    // Selector de unidad del sub-menú: se aplica a todas las cifras en pesos y se recuerda entre páginas
    var us = OBS.$('obsUnit');
    if (us) us.addEventListener('change', function () { OBS.setUnit(us.value); });
    OBS.syncUnitUi();

    // Buscador global del sub-menú
    var g = OBS.$('obsGlobalSearch');
    if (g) OBS.search(g);
})();
