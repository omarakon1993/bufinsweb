/* Pulso Bufins – análisis por sector */
(function () {
    'use strict';
    var root = document.getElementById('obs-app');
    if (!root || typeof Chart === 'undefined' || !window.OBS) return;
    var O = OBS, L = O.L, $ = O.$, esc = O.esc, EMP = root.getAttribute('data-empresa-url');

    var DEF = { anio: '', k: [], t: [], d: '', e: '', q: '', imin: '', imax: '', res: '', sn: [], pot: 0, o: 'ing', asc: false, p: 1, c: [] };
    var st = JSON.parse(JSON.stringify(DEF)), opc = null, ctrl = null, LAST = null;
    document.addEventListener('obs:unit', function () { if (LAST) render(LAST); });

    // ---------- estado <-> URL ----------
    function fromUrl() {
        var u = new URLSearchParams(location.search);
        function list(k) { return (u.get(k) || '').split('|').filter(Boolean); }
        st.anio = u.get('anio') || ''; st.k = list('k'); st.t = list('t'); st.c = list('c').slice(0, 4);
        st.d = u.get('d') || ''; st.e = u.get('e') || ''; st.q = u.get('q') || '';
        st.imin = u.get('imin') || ''; st.imax = u.get('imax') || ''; st.res = u.get('res') || '';
        st.sn = list('sn'); st.pot = Math.max(0, Math.min(100, parseInt(u.get('pot') || '0', 10) || 0));
        st.o = u.get('o') || 'ing'; st.asc = u.get('asc') === '1'; st.p = parseInt(u.get('p') || '1', 10) || 1;
    }
    function query(paging, cmp) {
        var u = new URLSearchParams();
        if (st.anio) u.set('anio', st.anio);
        if (st.k.length) u.set('k', st.k.join('|'));
        if (st.t.length) u.set('t', st.t.join('|'));
        if (st.d) u.set('d', st.d);
        if (st.e) u.set('e', st.e);
        if (st.q) u.set('q', st.q);
        if (st.imin !== '') u.set('imin', st.imin);
        if (st.imax !== '') u.set('imax', st.imax);
        if (st.res) u.set('res', st.res);
        if (st.sn.length) u.set('sn', st.sn.join('|'));
        if (st.pot > 0) u.set('pot', st.pot);
        if (st.o !== 'ing') u.set('o', st.o);
        if (st.asc) u.set('asc', '1');
        if (paging && st.p > 1) u.set('p', st.p);
        if (cmp && st.c.length) u.set('c', st.c.join('|'));
        return u.toString();
    }
    function activeCount() {
        return (st.k.length ? 1 : 0) + (st.t.length ? 1 : 0) + (st.d ? 1 : 0) + (st.e ? 1 : 0) + (st.q ? 1 : 0) + (st.imin !== '' || st.imax !== '' ? 1 : 0) + (st.res ? 1 : 0) + (st.sn.length || st.pot > 0 ? 1 : 0);
    }

    // ---------- árbol CIIU ----------
    var NODE = {};        // código -> {c,n,k,h,parent}
    var CIIU_OPEN = {};
    function indexTree(nodes, parent) { nodes.forEach(function (n) { n.parent = parent; NODE[n.c] = n; if (n.h) indexTree(n.h, n); }); }
    function covered(n) { for (var p = n.parent; p; p = p.parent) if (st.k.indexOf(p.c) >= 0) return true; return false; }
    function hasSelDesc(n) { return (n.h || []).some(function (c) { return st.k.indexOf(c.c) >= 0 || hasSelDesc(c); }); }
    function label(n) { return (n.c.length === 1 ? n.c + ' · ' : n.c + ' · ') + n.n; }
    function renderTree() {
        var q = O.$('fCiiuQ').value.trim().toLowerCase();
        function match(n) {
            if (!q) return true;
            if ((n.c + ' ' + n.n).toLowerCase().indexOf(q) >= 0) return true;
            return (n.h || []).some(match);
        }
        function li(n, depth) {
            if (!match(n)) return '';
            var kids = n.h && n.h.length, open = !!q || CIIU_OPEN[n.c];
            var cov = covered(n), on = st.k.indexOf(n.c) >= 0 || cov;
            return '<li role="treeitem" class="d' + depth + '"><div class="obs-tn' + (hasSelDesc(n) ? ' part' : '') + '">' +
                (kids ? '<button type="button" class="obs-caret' + (open ? ' open' : '') + '" data-tg="' + n.c + '" aria-label="+"><i class="fas fa-chevron-right"></i></button>' : '<span class="obs-caret none"></span>') +
                '<label><input type="checkbox" data-c="' + n.c + '"' + (on ? ' checked' : '') + (cov ? ' disabled' : '') + '><span>' + esc(label(n)) + '</span><small>' + O.int(n.k) + '</small></label></div>' +
                (kids && open ? '<ul>' + n.h.map(function (c) { return li(c, depth + 1); }).join('') + '</ul>' : '') + '</li>';
        }
        $('fCiiuTree').innerHTML = '<ul>' + opc.arbol.map(function (n) { return li(n, 0); }).join('') + '</ul>';
        $('fCiiuSel').innerHTML = st.k.map(function (c) { return '<button type="button" class="obs-chip" data-rm="' + c + '">' + esc(c + ' · ' + (NODE[c] ? NODE[c].n : '')) + ' <i class="fas fa-times"></i></button>'; }).join('');
    }
    $('fCiiuTree').addEventListener('click', function (e) {
        var tg = e.target.closest('[data-tg]');
        if (tg) { var c = tg.getAttribute('data-tg'); CIIU_OPEN[c] = !CIIU_OPEN[c]; renderTree(); return; }
    });
    $('fCiiuTree').addEventListener('change', function (e) {
        var cb = e.target.closest('input[data-c]'); if (!cb) return;
        var c = cb.getAttribute('data-c');
        if (cb.checked) {
            st.k = st.k.filter(function (x) { return x === c || !isDesc(NODE[x], c); });   // el nodo marcado reemplaza a sus descendientes
            if (st.k.indexOf(c) < 0) st.k.push(c);
        } else st.k = st.k.filter(function (x) { return x !== c; });
        st.p = 1; refresh();
    });
    function isDesc(n, anc) { for (var p = n && n.parent; p; p = p.parent) if (p.c === anc) return true; return false; }
    $('fCiiuSel').addEventListener('click', function (e) { var b = e.target.closest('[data-rm]'); if (!b) return; st.k = st.k.filter(function (x) { return x !== b.getAttribute('data-rm'); }); st.p = 1; refresh(); });
    $('fCiiuQ').addEventListener('input', O.debounce(renderTree, 200));

    // ---------- filtros ----------
    function opt(v, t, sel) { return '<option value="' + esc(v) + '"' + (sel ? ' selected' : '') + '>' + esc(t) + '</option>'; }
    function buildFilters() {
        $('fAnio').innerHTML = opc.anios.map(function (a) { return opt(a, a, String(a) === String(st.anio || opc.anioActual)); }).join('');
        $('fDepto').innerHTML = opt('', L('Todos', 'All'), !st.d) + opc.departamentos.map(function (x) { return opt(x.valor, x.valor + ' (' + O.int(x.n) + ')', x.valor === st.d); }).join('');
        $('fEstado').innerHTML = opt('', L('Todos', 'All'), !st.e) + opc.estados.map(function (x) { return opt(x.valor, x.valor + ' (' + O.int(x.n) + ')', x.valor === st.e); }).join('');
        indexTree(opc.arbol, null);
        // abrir los nodos con selección
        st.k.forEach(function (c) { for (var p = NODE[c] && NODE[c].parent; p; p = p.parent) CIIU_OPEN[p.c] = true; });
        renderTree(); syncControls();

        $('fAnio').onchange = function () { st.anio = this.value; st.p = 1; refresh(); };
        $('fDepto').onchange = function () { st.d = this.value; st.p = 1; refresh(); };
        $('fEstado').onchange = function () { st.e = this.value; st.p = 1; refresh(); };
        $('fQ').oninput = O.debounce(function () { st.q = $('fQ').value.trim(); st.p = 1; refresh(); }, 350);
        var debR = O.debounce(function () { st.imin = $('fIMin').value; st.imax = $('fIMax').value; st.p = 1; refresh(); }, 450);
        $('fIMin').oninput = debR; $('fIMax').oninput = debR;
        $('fRes').onclick = function (e) { var b = e.target.closest('button'); if (!b) return; st.res = b.getAttribute('data-v'); st.p = 1; refresh(); };
        $('fTam').onclick = function (e) {
            var b = e.target.closest('button'); if (!b) return; var t = b.getAttribute('data-t'), i = st.t.indexOf(t);
            if (i >= 0) st.t.splice(i, 1); else st.t.push(t); st.p = 1; refresh();
        };
        $('obsReset').onclick = $('obsResetMobile').onclick = reset;
        $('fSig').onclick = function (e) {
            var b = e.target.closest('[data-s]'); if (!b) return; var c = b.getAttribute('data-s'), i = st.sn.indexOf(c);
            if (i >= 0) st.sn.splice(i, 1); else st.sn.push(c); st.p = 1; refresh();
        };
        var debP = O.debounce(function () { st.pot = parseInt($('fPot').value, 10) || 0; st.p = 1; refresh(); }, 300);
        $('fPot').oninput = function () { $('fPotOut').textContent = this.value; debP(); };
    }
    function renderSignals() {
        if (!opc || !opc.senales) return;
        function grp(tipo, titulo) {
            var l = opc.senales.filter(function (s) { return s.tipo === tipo; });
            return l.length ? '<div class="obs-sig-g"><small>' + titulo + '</small>' + l.map(function (s) {
                var on = st.sn.indexOf(s.clave) >= 0;
                return '<button type="button" class="obs-sig' + (on ? ' on' : '') + '" data-s="' + esc(s.clave) + '" title="' + esc(s.regla) + '" aria-pressed="' + on + '">' + esc(L(s.es, s.en)) + '</button>';
            }).join('') + '</div>' : '';
        }
        $('fSig').innerHTML = grp('presion', L('Presión financiera', 'Financial pressure')) + grp('oportunidad', L('Oportunidad', 'Opportunity'));
        $('fPot').value = st.pot; $('fPotOut').textContent = st.pot;
        var n = st.sn.length + (st.pot > 0 ? 1 : 0), badge = $('fSigCount'); badge.hidden = n === 0; badge.textContent = n;
        if (n) $('fSigBox').open = true;
    }
    function syncControls() {
        $('fQ').value = st.q; $('fIMin').value = st.imin; $('fIMax').value = st.imax;
        Array.prototype.forEach.call($('fRes').querySelectorAll('button'), function (b) { b.classList.toggle('active', b.getAttribute('data-v') === st.res); });
        Array.prototype.forEach.call($('fTam').querySelectorAll('button'), function (b) { b.classList.toggle('active', st.t.indexOf(b.getAttribute('data-t')) >= 0); });
        if (opc) { $('fAnio').value = st.anio || opc.anioActual; $('fDepto').value = st.d; $('fEstado').value = st.e; renderTree(); renderSignals(); }
    }
    function reset() { var keep = st.c; st = JSON.parse(JSON.stringify(DEF)); st.c = keep; syncControls(); refresh(); }

    // ---------- atajos de prospectos ----------
    var PRESETS = {
        pierden: { t: ['M', 'L', 'XL'], sn: ['vendepierde'] },
        recurrente: { t: ['M', 'L', 'XL'], sn: ['perdida2'] },
        presion: { t: ['M', 'L', 'XL'], sn: ['perdida', 'liquidez', 'deuda'] },
        reorg: { t: ['M', 'L', 'XL'], sn: ['reorg'] }
    };
    $('obsPresets').addEventListener('click', function (e) {
        var b = e.target.closest('button[data-p]'); if (!b) return;
        var p = PRESETS[b.getAttribute('data-p')]; if (!p) return;
        var keep = st.c, anio = st.anio; st = JSON.parse(JSON.stringify(DEF)); st.c = keep; st.anio = anio;
        st.t = p.t.slice(); st.sn = p.sn.slice(); st.o = 'ing'; st.asc = false;
        O.track('filtro_sector', 'atajo-' + b.getAttribute('data-p'));
        syncControls(); refresh();
        var tab = document.querySelector('[data-bs-target="#tabEmpresas"]'); if (tab) new bootstrap.Tab(tab).show();
    });

    // ---------- chips ----------
    function renderChips() {
        var chips = [];
        function add(label, fn) { chips.push({ label: label, fn: fn }); }
        st.k.forEach(function (c) { add(c + ' · ' + (NODE[c] ? NODE[c].n : ''), function () { st.k = st.k.filter(function (x) { return x !== c; }); }); });
        st.t.forEach(function (t) { add(L('Tamaño ', 'Size ') + t, function () { st.t = st.t.filter(function (x) { return x !== t; }); }); });
        if (st.d) add(st.d, function () { st.d = ''; });
        if (st.e) add(st.e, function () { st.e = ''; });
        if (st.q) add('“' + st.q + '”', function () { st.q = ''; });
        if (st.imin !== '' || st.imax !== '') add(L('Ingresos ', 'Revenue ') + (st.imin || '0') + '–' + (st.imax || '∞'), function () { st.imin = ''; st.imax = ''; });
        st.sn.forEach(function (c) { var s = (opc.senales || []).filter(function (x) { return x.clave === c; })[0]; add(s ? L(s.es, s.en) : c, function () { st.sn = st.sn.filter(function (x) { return x !== c; }); }); });
        if (st.pot > 0) add(L('Potencial ≥ ', 'Potential ≥ ') + st.pot, function () { st.pot = 0; });
        if (st.res) add(st.res === 'utilidad' ? L('Con utilidad', 'Profitable') : L('Con pérdida', 'Loss-making'), function () { st.res = ''; });
        var el = $('obsChips');
        el.innerHTML = chips.map(function (c, i) { return '<button type="button" class="obs-chip" data-i="' + i + '">' + esc(c.label) + ' <i class="fas fa-times"></i></button>'; }).join('') +
            (chips.length ? '<button type="button" class="obs-link" id="obsClearChips">' + L('Limpiar todo', 'Clear all') + '</button>' : '');
        el.onclick = function (e) {
            if (e.target.closest('#obsClearChips')) { reset(); return; }
            var b = e.target.closest('.obs-chip'); if (!b) return; chips[b.getAttribute('data-i')].fn(); st.p = 1; syncControls(); refresh();
        };
        var n = activeCount(), badge = $('obsActiveCount'); badge.hidden = n === 0; badge.textContent = n;
    }

    // ---------- carga ----------
    function refresh() {
        var qs = query(true, true);
        history.replaceState(null, '', location.pathname + (qs ? '?' + qs : ''));
        O.track('filtro_sector', st.sn.length ? st.sn[0] : st.k.length ? st.k[0] : st.t.length ? 'tam' : 'otros');
        renderChips(); renderSignals(); load();
    }
    function load() {
        if (ctrl) ctrl.abort();
        ctrl = typeof AbortController !== 'undefined' ? new AbortController() : null;
        root.classList.add('obs-loading');
        O.api('analisis', query(true, false), ctrl ? { signal: ctrl.signal } : {})
            .then(function (d) { LAST = d; render(d); root.classList.remove('obs-loading'); })
            .catch(function (e) { if (e && e.name === 'AbortError') return; root.classList.remove('obs-loading'); $('obsKpis').innerHTML = '<div class="obs-empty w-100"><i class="fas fa-exclamation-triangle"></i><p>' + L('No se pudieron cargar los datos. Intenta de nuevo.', 'Data could not be loaded. Please try again.') + '</p></div>'; });
    }

    // ---------- render ----------
    var kpi = O.kpi;
    function render(d) {
        renderKpis(d); renderMedians(d); renderCharts(d); renderDist(d); renderTable(d);
        $('obsExport').href = '/pulso-bufins/api/exportar?' + query(false, false);
        $('obsExportX').href = '/pulso-bufins/api/exportarxlsx?' + query(false, false);
        $('obsTotal').textContent = O.int(d.total);
    }
    function renderKpis(d) {
        var k = d.kpis, t = d.tendencia, i = t.length - 1, idx = t.map(function (x) { return x.anio; }).indexOf(d.anio), p = idx > 0 ? t[idx - 1] : null;
        if (!k.n) { $('obsKpis').innerHTML = '<div class="obs-empty w-100"><i class="fas fa-search"></i><p>' + L('Ninguna empresa cumple estos filtros. Prueba ampliarlos.', 'No company matches these filters. Try widening them.') + '</p></div>'; return; }
        $('obsKpis').innerHTML =
            kpi(L('Empresas', 'Companies'), O.int(k.n), '<em>' + L('Año ', 'Year ') + d.anio + '</em>') +
            kpi(L('Ingresos', 'Revenue'), O.money(k.ing), O.delta(k.ing, p && p.ing)) +
            kpi(L('Utilidad bruta', 'Gross profit'), O.money(k.ub), '<em>' + L('Margen', 'Margin') + ' ' + O.pct(k.mbruto) + '</em>') +
            kpi('EBITDA', O.money(k.ebitda), '<em>' + L('Margen', 'Margin') + ' ' + O.pct(k.mebitda) + '</em>', k.ebitda < 0 ? 'neg' : '') +
            kpi(L('Capital de trabajo neto', 'Net working capital'), O.money(k.ktn), '<em>' + O.pct(k.ktingresos) + ' ' + L('de ingresos', 'of revenue') + '</em>', k.ktn < 0 ? 'neg' : '') +
            kpi(L('Deuda financiera', 'Financial debt'), O.money(k.deuda), '<em>' + L('Deuda/EBITDA', 'Debt/EBITDA') + ' ' + O.x(k.deudaebitda) + '</em>');
    }
    function renderMedians(d) {
        var s = d.estadisticas || {}, items = [['mebitda', L('Margen EBITDA', 'EBITDA margin'), O.pct], ['mneto', L('Margen neto', 'Net margin'), O.pct], ['endeud', L('Endeudamiento', 'Leverage'), O.pct], ['deudaebitda', 'Deuda/EBITDA', O.x], ['liquidez', L('Liquidez corriente', 'Current ratio'), O.x], ['ciclo', L('Ciclo de caja (días)', 'Cash cycle (days)'), O.days]];
        $('obsMedians').innerHTML = '<div class="obs-medians-t">' + L('Empresa mediana del filtro', 'Median company in this filter') + '</div>' + items.filter(function (x) { return s[x[0]]; }).map(function (x) {
            var r = s[x[0]];
            return '<div class="obs-med"><span>' + x[1] + '</span><strong>' + x[2](r.med) + '</strong><small>P25–P75: ' + x[2](r.p25) + ' – ' + x[2](r.p75) + '</small></div>';
        }).join('');
    }
    function renderCharts(d) {
        var t = d.tendencia, a = t.map(function (x) { return x.anio; });
        var crec = t.map(function (x, i) { return i > 0 && t[i - 1].ing > 0 ? Math.round((x.ing / t[i - 1].ing - 1) * 1000) / 10 : null; });
        var gastos = t.map(function (x) { return x.ing > 0 ? Math.round(x.gop / x.ing * 1000) / 10 : null; });
        O.combo('chIng', a, { label: L('Ingresos', 'Revenue'), data: t.map(function (x) { return x.ing; }) }, { label: L('Crecimiento %', 'Growth %'), data: crec }, d.anio, '#583AFF');
        O.combo('chUb', a, { label: L('Utilidad bruta', 'Gross profit'), data: t.map(function (x) { return x.ub; }) }, { label: L('Margen bruto %', 'Gross margin %'), data: t.map(function (x) { return x.mbruto; }) }, d.anio, '#14B8A6');
        O.combo('chGop', a, { label: L('Gastos operacionales', 'Operating expenses'), data: t.map(function (x) { return x.gop; }) }, { label: L('% de ingresos', '% of revenue'), data: gastos }, d.anio, '#8F7BFF');
        O.combo('chEbitda', a, { label: 'EBITDA', data: t.map(function (x) { return x.ebitda; }) }, { label: L('Margen EBITDA %', 'EBITDA margin %'), data: t.map(function (x) { return x.mebitda; }) }, d.anio, '#160933');
        O.combo('chKtn', a, { label: L('Capital de trabajo neto', 'Net working capital'), data: t.map(function (x) { return x.ktn; }) }, { label: L('% de ingresos', '% of revenue'), data: t.map(function (x) { return x.ktingresos; }) }, d.anio, '#0EA5E9');
        O.combo('chDeuda', a, { label: L('Deuda', 'Debt'), data: t.map(function (x) { return x.deuda; }) }, { label: 'Deuda/EBITDA', data: t.map(function (x) { return x.deudaebitda; }), fmt: function (v) { return v + 'x'; } }, d.anio, '#FF9A66');
    }

    // ---------- distribución: una vista a la vez ----------
    var DIST = 'tam', LASTD = null, BOXM = 'mebitda', MAPM = 'ing';
    var BOXU = { mebitda: '%', mneto: '%', endeud: '%', liquidez: 'x', score: 'pts' };
    function fmtU(m, v) { return m === 'liquidez' ? O.x(v) : m === 'score' ? O.int(v) : O.pct(v); }

    function renderBox(d) {
        var rows = (d.cajas && d.cajas[BOXM]) || [];
        if (!rows.length) { O.chart('chBox', { type: 'bar', data: { labels: [], datasets: [] }, options: O.COMMON }); return; }
        var col = { S: '#8F7BFF', M: '#01B3AB', L: '#583AFF', XL: '#160933' };
        var plugin = {
            id: 'boxwhisk',
            afterDatasetsDraw: function (chart) {
                var ctx = chart.ctx, ys = chart.scales.y, meta = chart.getDatasetMeta(0);
                rows.forEach(function (r, i) {
                    var bar = meta.data[i]; if (!bar) return; var x = bar.x, w = bar.width / 2;
                    ctx.save(); ctx.strokeStyle = '#160933'; ctx.lineWidth = 1.5; ctx.beginPath();
                    ctx.moveTo(x, ys.getPixelForValue(r.p10)); ctx.lineTo(x, ys.getPixelForValue(r.p25));
                    ctx.moveTo(x, ys.getPixelForValue(r.p75)); ctx.lineTo(x, ys.getPixelForValue(r.p90));
                    [r.p10, r.p90].forEach(function (v) { var py = ys.getPixelForValue(v); ctx.moveTo(x - 8, py); ctx.lineTo(x + 8, py); });
                    ctx.stroke();
                    var ym = ys.getPixelForValue(r.med); ctx.strokeStyle = '#fff'; ctx.lineWidth = 3; ctx.beginPath(); ctx.moveTo(x - w, ym); ctx.lineTo(x + w, ym); ctx.stroke(); ctx.restore();
                });
            }
        };
        var lo = Math.min.apply(null, rows.map(function (r) { return r.p10; })), hi = Math.max.apply(null, rows.map(function (r) { return r.p90; }));
        O.chart('chBox', {
            type: 'bar', plugins: [plugin],
            data: { labels: rows.map(function (r) { return r.t + ' (' + O.int(r.n) + ')'; }), datasets: [{ obsLabels: rows.map(function (r) { return fmtU(BOXM, r.med); }), data: rows.map(function (r) { return [r.p25, r.p75]; }), backgroundColor: rows.map(function (r) { return col[r.t]; }), borderRadius: 4, barPercentage: .55 }] },
            options: Object.assign({}, O.COMMON, {
                plugins: { legend: { display: false }, tooltip: { callbacks: { label: function (c) { var r = rows[c.dataIndex]; return [L('Mediana: ', 'Median: ') + fmtU(BOXM, r.med), 'P25–P75: ' + fmtU(BOXM, r.p25) + ' – ' + fmtU(BOXM, r.p75), 'P10–P90: ' + fmtU(BOXM, r.p10) + ' – ' + fmtU(BOXM, r.p90)]; } } } },
                scales: { y: { min: Math.floor(lo - (hi - lo) * .05), max: Math.ceil(hi + (hi - lo) * .05) } }
            })
        });
    }

    // mapa esquemático (cuadrícula) de departamentos: posición aproximada de norte a sur y de occidente a oriente
    var TILES = [['SAN ANDRES', 'SAP', 0, 0], ['ATLANTICO', 'ATL', 4, 0], ['GUAJIRA', 'GUA', 6, 0], ['CORDOBA', 'COR', 2, 1], ['SUCRE', 'SUC', 3, 1], ['BOLIVAR', 'BOL', 4, 1], ['MAGDALENA', 'MAG', 5, 1],
        ['CESAR', 'CES', 6, 1], ['CHOCO', 'CHO', 1, 2], ['ANTIOQUIA', 'ANT', 2, 2], ['NORTE DE SANTANDER', 'NSA', 6, 2], ['SANTANDER', 'SAN', 5, 2], ['ARAUCA', 'ARA', 7, 2], ['RISARALDA', 'RIS', 2, 3],
        ['CALDAS', 'CAL', 3, 3], ['CUNDINAMARCA', 'CUN', 4, 3], ['BOYACA', 'BOY', 5, 3], ['CASANARE', 'CAS', 6, 3], ['VALLE', 'VAL', 1, 4], ['QUINDIO', 'QUI', 2, 4], ['TOLIMA', 'TOL', 3, 4], ['BOGOTA', 'BOG', 4, 4],
        ['META', 'MET', 5, 4], ['VICHADA', 'VIC', 7, 4], ['CAUCA', 'CAU', 1, 5], ['HUILA', 'HUI', 3, 5], ['GUAVIARE', 'GUV', 6, 5], ['GUAINIA', 'GUN', 8, 5], ['NARINO', 'NAR', 1, 6], ['PUTUMAYO', 'PUT', 3, 6],
        ['CAQUETA', 'CAQ', 4, 6], ['VAUPES', 'VAU', 7, 6], ['AMAZONAS', 'AMA', 5, 7]];
    function norm(s) { return String(s || '').normalize('NFD').replace(/[\u0300-\u036f]/g, '').toUpperCase(); }
    function tileFor(name) { var n = norm(name); for (var i = 0; i < TILES.length; i++) if (n.indexOf(TILES[i][0]) >= 0) return TILES[i]; return null; }
    function renderMap(d) {
        var by = {}; d.departamentos.forEach(function (r) { var t = tileFor(r.k); if (t) by[t[1]] = r; });
        var vals = Object.keys(by).map(function (k) { var r = by[k]; return MAPM === 'ing' ? r.ing : MAPM === 'n' ? r.n : r.mebitda; }).filter(function (v) { return v != null; });
        var lo = Math.min.apply(null, vals.concat([0])), hi = Math.max.apply(null, vals.concat([1]));
        function val(r) { return MAPM === 'ing' ? r.ing : MAPM === 'n' ? r.n : r.mebitda; }
        function show(r) { var v = val(r); return v == null ? '—' : MAPM === 'ing' ? O.money(v, true) : MAPM === 'n' ? O.int(v) : O.pct(v); }
        var html = TILES.map(function (t) {
            var r = by[t[1]];
            var style = 'grid-column:' + (t[2] + 1) + ';grid-row:' + (t[3] + 1) + ';';
            if (!r || val(r) == null) return '<div class="obs-tile off" style="' + style + '" title="' + esc(t[0]) + '"><b>' + t[1] + '</b></div>';
            var f = hi > lo ? (val(r) - lo) / (hi - lo) : 0; f = MAPM === 'ing' || MAPM === 'n' ? Math.sqrt(Math.max(0, f)) : Math.max(0, f);
            var on = st.d === r.k;
            return '<button type="button" class="obs-tile' + (on ? ' on' : '') + (f > .55 ? ' dark' : '') + '" style="' + style + 'background:rgba(88,58,255,' + (.1 + f * .85).toFixed(2) + ')" data-d="' + esc(r.k) + '" title="' + esc(r.k) + ' · ' + esc(show(r)) + ' · ' + O.int(r.n) + ' ' + L('empresas', 'companies') + '"><b>' + t[1] + '</b><span>' + esc(show(r)) + '</span></button>';
        }).join('');
        $('deptoMap').innerHTML = html;
        $('deptoMap').onclick = function (e) { var b = e.target.closest('[data-d]'); if (!b) return; var k = b.getAttribute('data-d'); st.d = st.d === k ? '' : k; st.p = 1; syncControls(); refresh(); };
    }

    // treemap por actividad (algoritmo squarified propio, sin librerías)
    function squarify(vals, W, H) {
        var total = vals.reduce(function (s, v) { return s + v.v; }, 0), scale = W * H / total, rects = [], x = 0, y = 0, w = W, h = H, row = [];
        var rest = vals.map(function (v) { return { i: v.i, a: v.v * scale }; });
        function sum(r) { return r.reduce(function (s, v) { return s + v.a; }, 0); }
        function worst(r, side) { var s = sum(r), mx = Math.max.apply(null, r.map(function (v) { return v.a; })), mn = Math.min.apply(null, r.map(function (v) { return v.a; })); return Math.max(side * side * mx / (s * s), s * s / (side * side * mn)); }
        function layout(r) {
            var s = sum(r);
            if (w >= h) { var cw = s / h, cy = y; r.forEach(function (v) { var rh = v.a / cw; rects.push({ i: v.i, x: x, y: cy, w: cw, h: rh }); cy += rh; }); x += cw; w -= cw; }
            else { var rh2 = s / w, cx = x; r.forEach(function (v) { var rw = v.a / rh2; rects.push({ i: v.i, x: cx, y: y, w: rw, h: rh2 }); cx += rw; }); y += rh2; h -= rh2; }
        }
        while (rest.length) {
            var side = Math.min(w, h), it = rest[0];
            if (!row.length || worst(row.concat([it]), side) <= worst(row, side)) { row.push(it); rest.shift(); } else { layout(row); row = []; }
        }
        if (row.length) layout(row);
        return rects;
    }
    function renderTreemap(d) {
        var rows = d.divisiones.filter(function (r) { return r.ing > 0; });
        if (!rows.length) { $('treemap').innerHTML = '<p class="obs-hint p-3">' + L('Sin datos', 'No data') + '</p>'; return; }
        var W = 100, H = 56, sel = st.k;
        var rects = squarify(rows.map(function (r, i) { return { i: i, v: r.ing }; }), W, H);
        $('treemap').innerHTML = rects.map(function (q) {
            var r = rows[q.i], on = sel.indexOf(r.k) >= 0, dim = sel.length && !on;
            var name = r.k + ' · ' + (r.etiqueta || ''), small = q.w < 9 || q.h < 7;
            return '<button type="button" class="obs-tm' + (on ? ' on' : '') + (dim ? ' dim' : '') + '" data-k="' + esc(r.k) + '" style="left:' + q.x + '%;top:' + (q.y / H * 100) + '%;width:' + q.w + '%;height:' + (q.h / H * 100) + '%;background:' + O.PALETTE[q.i % O.PALETTE.length] + '" title="' + esc(name) + ' · ' + esc(O.money(r.ing)) + '">' +
                (small ? '' : '<b>' + esc(name.length > 46 ? name.slice(0, 44) + '…' : name) + '</b><span>' + esc(O.money(r.ing, true)) + '</span>') + '</button>';
        }).join('');
        $('treemap').onclick = function (e) { var b = e.target.closest('[data-k]'); if (!b) return; var k = b.getAttribute('data-k'), i = st.k.indexOf(k); if (i >= 0) st.k.splice(i, 1); else st.k.push(k); st.p = 1; syncControls(); refresh(); };
    }
    function renderDist(d) {
        LASTD = d;
        var tam = { S: '#8F7BFF', M: '#01E1D7', L: '#583AFF', XL: '#160933' };
        O.chart('chTam', {
            type: 'doughnut',
            data: { labels: d.tamanos.map(function (x) { return x.codigo; }), datasets: [{ data: d.tamanos.map(function (x) { return x.n; }), backgroundColor: d.tamanos.map(function (x) { return tam[x.codigo]; }) }] },
            options: Object.assign({}, O.COMMON, {
                plugins: { legend: { position: 'bottom' }, tooltip: { callbacks: { label: function (c) { var x = d.tamanos[c.dataIndex]; return x.codigo + ': ' + O.int(x.n) + ' ' + L('empresas', 'companies') + ' · ' + O.money(x.ing); } } } },
                onClick: function (e, els) { if (!els.length) return; var t = d.tamanos[els[0].index].codigo, i = st.t.indexOf(t); if (i >= 0) st.t.splice(i, 1); else st.t.push(t); st.p = 1; syncControls(); refresh(); }
            })
        });
        renderBox(d); renderMap(d); renderTreemap(d);
        var ms = {}, n = 0; d.dispersion.forEach(function (p) { if (!(p.t in ms)) ms[p.t] = n++; });
        var maxA = Math.max.apply(null, d.dispersion.map(function (p) { return p.r; }).concat([1]));
        var ds = Object.keys(ms).map(function (m) {
            var c = O.PALETTE[ms[m] % O.PALETTE.length];
            return { label: (m || '—') + ' · ' + ((NODE[m] && NODE[m].n) ? NODE[m].n.slice(0, 28) : ''), backgroundColor: c + 'B3', borderColor: c, obsLabels: function (i) { var q = this.data[i]; return q && q.r >= 12 ? String(q.razon).slice(0, 16) : ''; }, data: d.dispersion.filter(function (p) { return p.t === m; }).map(function (p) { return { x: p.x, y: p.y, r: 3 + 17 * Math.sqrt(Math.max(p.r, 0) / maxA), nit: p.nit, razon: p.razon, url: p.url }; }) };
        });
        O.chart('chScatter', {
            type: 'bubble', data: { datasets: ds },
            options: Object.assign({}, O.COMMON, {
                plugins: { legend: { position: 'bottom', labels: { boxWidth: 12 } }, tooltip: { callbacks: { label: function (c) { var r = c.raw; return r.razon + ' — ' + L('Endeud. ', 'Leverage ') + O.pct(r.x) + ', ' + L('Mg EBITDA ', 'EBITDA mg ') + O.pct(r.y); } } } },
                scales: { x: { title: { display: true, text: L('Endeudamiento (pasivo/activo %)', 'Leverage (liabilities/assets %)') }, min: 0, max: 200 }, y: { title: { display: true, text: L('Margen EBITDA %', 'EBITDA margin %') }, min: -60, max: 80 } },
                onClick: function (e, els) { if (els.length) { var r = ds[els[0].datasetIndex].data[els[0].index]; location.href = r.url || (EMP + r.nit); } },
                onHover: function (evt, els) { evt.native.target.style.cursor = els.length ? 'pointer' : 'default'; }
            })
        });
    }
    $('distView').addEventListener('click', function (e) {
        var b = e.target.closest('button'); if (!b) return; DIST = b.getAttribute('data-v');
        Array.prototype.forEach.call($('distView').querySelectorAll('button'), function (x) { x.classList.toggle('active', x === b); });
        Array.prototype.forEach.call(document.querySelectorAll('.obs-dist'), function (p) { p.hidden = p.getAttribute('data-view') !== DIST; });
        O.resizeCharts();
    });
    $('boxMetric').addEventListener('click', function (e) {
        var b = e.target.closest('button'); if (!b) return; BOXM = b.getAttribute('data-m');
        Array.prototype.forEach.call($('boxMetric').querySelectorAll('button'), function (x) { x.classList.toggle('active', x === b); });
        if (LASTD) renderBox(LASTD);
    });
    $('mapMetric').addEventListener('click', function (e) {
        var b = e.target.closest('button'); if (!b) return; MAPM = b.getAttribute('data-m');
        Array.prototype.forEach.call($('mapMetric').querySelectorAll('button'), function (x) { x.classList.toggle('active', x === b); });
        if (LASTD) renderMap(LASTD);
    });

    function sigChips(r) {
        if (!r.senales || !r.senales.length) return '';
        var defs = (opc && opc.senales) || [];
        return '<span class="obs-rowsig">' + r.senales.slice(0, 3).map(function (c) {
            var s = defs.filter(function (x) { return x.clave === c; })[0];
            return s ? '<i class="' + (s.tipo === 'oportunidad' ? 'opp' : 'prs') + '" title="' + esc(s.regla) + '">' + esc(L(s.es, s.en)) + '</i>' : '';
        }).join('') + '</span>';
    }
    function renderTable(d) {
        Array.prototype.forEach.call(document.querySelectorAll('.obs-table th[data-o]'), function (h) {
            h.classList.toggle('sorted', h.getAttribute('data-o') === st.o);
            h.setAttribute('data-dir', h.getAttribute('data-o') === st.o ? (st.asc ? '▲' : '▼') : '');
            h.onclick = function () { var o = h.getAttribute('data-o'); if (st.o === o) st.asc = !st.asc; else { st.o = o; st.asc = o === 'nombre'; } st.p = 1; refresh(); };
        });
        var full = st.c.length >= 4;
        $('obsRows').innerHTML = d.tabla.map(function (r) {
            var on = st.c.indexOf(r.nit) >= 0;
            return '<tr><td><input type="checkbox" class="form-check-input obs-cmp" data-nit="' + esc(r.nit) + '"' + (on ? ' checked' : '') + (full && !on ? ' disabled' : '') + ' aria-label="' + L('Comparar', 'Compare') + '"></td>' +
                '<td><a href="' + esc(r.url || EMP + r.nit) + '" class="obs-co">' + esc(r.razon) + '</a><small>NIT ' + esc(r.nit) + ' · ' + esc(r.ciudad) + ' · <b class="obs-tam">' + r.tam + '</b></small>' + sigChips(r) + '</td>' +
                '<td class="text-end"><span class="obs-pot ' + (r.potencial >= 60 ? 'hi' : r.potencial >= 35 ? 'mid' : 'lo') + '" title="' + L('Score de salud ', 'Health score ') + O.int(r.score) + '">' + O.int(r.potencial) + '</span></td>' +
                '<td class="text-end">' + O.money(r.ing) + '</td>' +
                '<td class="text-end ' + (r.crec != null && r.crec < 0 ? 'obs-neg' : '') + '">' + (r.crec == null ? '—' : (r.crec >= 0 ? '+' : '') + O.pct(r.crec)) + '</td>' +
                '<td class="text-end ' + (r.uop < 0 ? 'obs-neg' : '') + '">' + O.money(r.uop) + '</td><td class="text-end ' + (r.ebitda < 0 ? 'obs-neg' : '') + '">' + O.money(r.ebitda) + '</td><td class="text-end">' + O.money(r.deuda) + '</td>' +
                '<td class="text-end">' + O.pct(r.mbruto) + '</td><td class="text-end">' + O.pct(r.moper) + '</td><td class="text-end">' + O.pct(r.mebitda) + '</td><td class="text-end">' + O.x(r.deudaebitda) + '</td><td class="text-end">' + O.pct(r.ktingresos) + '</td></tr>';
        }).join('') || '<tr><td colspan="13" class="text-center text-muted py-4">' + L('Sin resultados', 'No results') + '</td></tr>';

        var p = d.pagina, n = d.paginas, html = '';
        function pb(i, lbl, dis, act) { return '<button type="button" data-p="' + i + '"' + (dis ? ' disabled' : '') + (act ? ' class="active"' : '') + '>' + lbl + '</button>'; }
        html += pb(p - 1, '‹', p <= 1);
        var from = Math.max(1, p - 2), to = Math.min(n, p + 2);
        if (from > 1) html += pb(1, '1') + (from > 2 ? '<span>…</span>' : '');
        for (var i = from; i <= to; i++) html += pb(i, i, false, i === p);
        if (to < n) html += (to < n - 1 ? '<span>…</span>' : '') + pb(n, n);
        html += pb(p + 1, '›', p >= n);
        $('obsPager').innerHTML = n > 1 ? html + '<small>' + L('Página ', 'Page ') + p + L(' de ', ' of ') + O.int(n) + '</small>' : '';
        $('obsPager').onclick = function (e) { var b = e.target.closest('button'); if (!b || b.disabled) return; st.p = parseInt(b.getAttribute('data-p'), 10); refresh(); $('tabEmpresas').scrollIntoView({ behavior: 'smooth', block: 'start' }); };
        $('obsRows').onchange = function (e) {
            var c = e.target.closest('.obs-cmp'); if (!c) return;
            var nit = c.getAttribute('data-nit'), i = st.c.indexOf(nit);
            if (c.checked && i < 0 && st.c.length < 4) st.c.push(nit); else if (!c.checked && i >= 0) st.c.splice(i, 1);
            refresh();
        };
        var b = $('obsCompare'); b.hidden = st.c.length < 2; b.href = '/pulso-bufins/comparar?nits=' + st.c.join('|');
        b.querySelector('span').textContent = L('Comparar (', 'Compare (') + st.c.length + ')';
    }

    O.share($('obsShare'));

    fromUrl();
    O.api('opciones').then(function (o) { opc = o; buildFilters(); renderChips(); load(); });
})();
