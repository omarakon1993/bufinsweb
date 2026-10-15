/* Observatorio Bufins – análisis por sector */
(function () {
    'use strict';
    var root = document.getElementById('obs-app');
    if (!root || typeof Chart === 'undefined' || !window.OBS) return;
    var O = OBS, L = O.L, $ = O.$, esc = O.esc, EMP = root.getAttribute('data-empresa-url');

    var DEF = { anio: '', k: [], t: [], d: '', e: '', q: '', imin: '', imax: '', res: '', o: 'ing', asc: false, p: 1, c: [] };
    var st = JSON.parse(JSON.stringify(DEF)), opc = null, ctrl = null, last = null, names = {};

    // ---------- estado <-> URL ----------
    function fromUrl() {
        var u = new URLSearchParams(location.search);
        function list(k) { return (u.get(k) || '').split('|').filter(Boolean); }
        st.anio = u.get('anio') || ''; st.k = list('k'); st.t = list('t'); st.c = list('c').slice(0, 4);
        st.d = u.get('d') || ''; st.e = u.get('e') || ''; st.q = u.get('q') || '';
        st.imin = u.get('imin') || ''; st.imax = u.get('imax') || ''; st.res = u.get('res') || '';
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
        if (st.o !== 'ing') u.set('o', st.o);
        if (st.asc) u.set('asc', '1');
        if (paging && st.p > 1) u.set('p', st.p);
        if (cmp && st.c.length) u.set('c', st.c.join('|'));
        return u.toString();
    }
    function activeCount() {
        return (st.k.length ? 1 : 0) + (st.t.length ? 1 : 0) + (st.d ? 1 : 0) + (st.e ? 1 : 0) + (st.q ? 1 : 0) + (st.imin !== '' || st.imax !== '' ? 1 : 0) + (st.res ? 1 : 0);
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
    }
    function syncControls() {
        $('fQ').value = st.q; $('fIMin').value = st.imin; $('fIMax').value = st.imax;
        Array.prototype.forEach.call($('fRes').querySelectorAll('button'), function (b) { b.classList.toggle('active', b.getAttribute('data-v') === st.res); });
        Array.prototype.forEach.call($('fTam').querySelectorAll('button'), function (b) { b.classList.toggle('active', st.t.indexOf(b.getAttribute('data-t')) >= 0); });
        if (opc) { $('fAnio').value = st.anio || opc.anioActual; $('fDepto').value = st.d; $('fEstado').value = st.e; renderTree(); }
    }
    function reset() { var keep = st.c; st = JSON.parse(JSON.stringify(DEF)); st.c = keep; syncControls(); refresh(); }

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
        renderChips(); load();
    }
    function load() {
        if (ctrl) ctrl.abort();
        ctrl = typeof AbortController !== 'undefined' ? new AbortController() : null;
        root.classList.add('obs-loading');
        O.api('analisis', query(true, false), ctrl ? { signal: ctrl.signal } : {})
            .then(function (d) { last = d; render(d); root.classList.remove('obs-loading'); })
            .catch(function (e) { if (e && e.name === 'AbortError') return; root.classList.remove('obs-loading'); $('obsKpis').innerHTML = '<div class="obs-empty w-100"><i class="fas fa-exclamation-triangle"></i><p>' + L('No se pudieron cargar los datos. Intenta de nuevo.', 'Data could not be loaded. Please try again.') + '</p></div>'; });
    }

    // ---------- render ----------
    function kpi(label, value, sub, cls) { return '<div class="obs-kpi ' + (cls || '') + '"><span>' + label + '</span><strong>' + value + '</strong>' + (sub || '') + '</div>'; }
    function render(d) {
        renderKpis(d); renderMedians(d); renderCharts(d); renderDist(d); renderTable(d);
        $('obsExport').href = '/observatorio/api/exportar?' + query(false, false);
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

    function bars(id, rows, key, multi) {
        var max = Math.max.apply(null, rows.map(function (r) { return r.ing; }).concat([1])), u = O.unitFor(max);
        var sel = key === 'k' ? st.k : (st.d ? [st.d] : []);
        O.chart(id, {
            type: 'bar',
            data: {
                labels: rows.map(function (r) { var s = r.etiqueta || r.k; s = (key === 'k' ? r.k + ' · ' : '') + s; return s.length > 44 ? s.slice(0, 42) + '…' : s; }),
                datasets: [{ label: L('Ingresos', 'Revenue') + ' (' + u.l + ')', data: rows.map(function (r) { return r.ing / u.d; }), borderRadius: 6, backgroundColor: rows.map(function (r, i) { return !sel.length || sel.indexOf(r.k) >= 0 ? O.PALETTE[i % O.PALETTE.length] : 'rgba(148,163,184,.4)'; }) }]
            },
            options: Object.assign({}, O.COMMON, {
                indexAxis: 'y',
                plugins: { legend: { display: false }, tooltip: { callbacks: { label: function (c) { var r = rows[c.dataIndex]; return [L('Ingresos: ', 'Revenue: ') + O.money(r.ing), L('Empresas: ', 'Companies: ') + O.int(r.n), L('Margen EBITDA: ', 'EBITDA margin: ') + O.pct(r.mebitda)]; } } } },
                onClick: function (evt, els) {
                    if (!els.length) return; var k = rows[els[0].index].k;
                    if (key === 'k') { var i = st.k.indexOf(k); if (i >= 0) st.k.splice(i, 1); else st.k.push(k); } else st.d = st.d === k ? '' : k;
                    st.p = 1; syncControls(); refresh();
                },
                onHover: function (evt, els) { evt.native.target.style.cursor = els.length ? 'pointer' : 'default'; }
            })
        });
    }
    function renderDist(d) {
        var tam = { S: '#8F7BFF', M: '#01E1D7', L: '#583AFF', XL: '#160933' };
        O.chart('chTam', {
            type: 'doughnut',
            data: { labels: d.tamanos.map(function (x) { return x.codigo; }), datasets: [{ data: d.tamanos.map(function (x) { return x.n; }), backgroundColor: d.tamanos.map(function (x) { return tam[x.codigo]; }) }] },
            options: Object.assign({}, O.COMMON, {
                plugins: { legend: { position: 'bottom' }, tooltip: { callbacks: { label: function (c) { var x = d.tamanos[c.dataIndex]; return x.codigo + ': ' + O.int(x.n) + ' ' + L('empresas', 'companies') + ' · ' + O.money(x.ing); } } } },
                onClick: function (e, els) { if (!els.length) return; var t = d.tamanos[els[0].index].codigo, i = st.t.indexOf(t); if (i >= 0) st.t.splice(i, 1); else st.t.push(t); st.p = 1; syncControls(); refresh(); }
            })
        });
        bars('chDepto', d.departamentos, 'd'); bars('chDiv', d.divisiones, 'k');
        var ms = {}, n = 0; d.dispersion.forEach(function (p) { if (!(p.t in ms)) ms[p.t] = n++; });
        var maxA = Math.max.apply(null, d.dispersion.map(function (p) { return p.r; }).concat([1]));
        var ds = Object.keys(ms).map(function (m) {
            var c = O.PALETTE[ms[m] % O.PALETTE.length];
            return { label: (m || '—') + ' · ' + ((NODE[m] && NODE[m].n) ? NODE[m].n.slice(0, 28) : ''), backgroundColor: c + 'B3', borderColor: c, data: d.dispersion.filter(function (p) { return p.t === m; }).map(function (p) { return { x: p.x, y: p.y, r: 3 + 17 * Math.sqrt(Math.max(p.r, 0) / maxA), nit: p.nit, razon: p.razon }; }) };
        });
        O.chart('chScatter', {
            type: 'bubble', data: { datasets: ds },
            options: Object.assign({}, O.COMMON, {
                plugins: { legend: { position: 'bottom', labels: { boxWidth: 12 } }, tooltip: { callbacks: { label: function (c) { var r = c.raw; return r.razon + ' — ' + L('Endeud. ', 'Leverage ') + O.pct(r.x) + ', ' + L('Mg EBITDA ', 'EBITDA mg ') + O.pct(r.y); } } } },
                scales: { x: { title: { display: true, text: L('Endeudamiento (pasivo/activo %)', 'Leverage (liabilities/assets %)') }, min: 0, max: 200 }, y: { title: { display: true, text: L('Margen EBITDA %', 'EBITDA margin %') }, min: -60, max: 80 } },
                onClick: function (e, els) { if (els.length) { var r = ds[els[0].datasetIndex].data[els[0].index]; location.href = EMP + r.nit; } },
                onHover: function (evt, els) { evt.native.target.style.cursor = els.length ? 'pointer' : 'default'; }
            })
        });
    }

    function renderTable(d) {
        Array.prototype.forEach.call(document.querySelectorAll('.obs-table th[data-o]'), function (h) {
            h.classList.toggle('sorted', h.getAttribute('data-o') === st.o);
            h.setAttribute('data-dir', h.getAttribute('data-o') === st.o ? (st.asc ? '▲' : '▼') : '');
            h.onclick = function () { var o = h.getAttribute('data-o'); if (st.o === o) st.asc = !st.asc; else { st.o = o; st.asc = o === 'nombre'; } st.p = 1; refresh(); };
        });
        var full = st.c.length >= 4;
        $('obsRows').innerHTML = d.tabla.map(function (r) {
            var on = st.c.indexOf(r.nit) >= 0; names[r.nit] = r.razon;
            return '<tr><td><input type="checkbox" class="form-check-input obs-cmp" data-nit="' + esc(r.nit) + '"' + (on ? ' checked' : '') + (full && !on ? ' disabled' : '') + ' aria-label="' + L('Comparar', 'Compare') + '"></td>' +
                '<td><a href="' + EMP + esc(r.nit) + '" class="obs-co">' + esc(r.razon) + '</a><small>NIT ' + esc(r.nit) + ' · ' + esc(r.ciudad) + ' · <b class="obs-tam">' + r.tam + '</b></small></td>' +
                '<td class="text-end">' + O.money(r.ing) + '</td>' +
                '<td class="text-end ' + (r.crec != null && r.crec < 0 ? 'obs-neg' : '') + '">' + (r.crec == null ? '—' : (r.crec >= 0 ? '+' : '') + O.pct(r.crec)) + '</td>' +
                '<td class="text-end ' + (r.uop < 0 ? 'obs-neg' : '') + '">' + O.money(r.uop) + '</td><td class="text-end ' + (r.ebitda < 0 ? 'obs-neg' : '') + '">' + O.money(r.ebitda) + '</td><td class="text-end">' + O.money(r.deuda) + '</td>' +
                '<td class="text-end">' + O.pct(r.mbruto) + '</td><td class="text-end">' + O.pct(r.moper) + '</td><td class="text-end">' + O.pct(r.mebitda) + '</td><td class="text-end">' + O.x(r.deudaebitda) + '</td><td class="text-end">' + O.pct(r.ktingresos) + '</td></tr>';
        }).join('') || '<tr><td colspan="12" class="text-center text-muted py-4">' + L('Sin resultados', 'No results') + '</td></tr>';

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
        var b = $('obsCompare'); b.hidden = st.c.length < 2; b.href = '/observatorio/comparar?nits=' + st.c.join('|');
        b.querySelector('span').textContent = L('Comparar (', 'Compare (') + st.c.length + ')';
    }

    $('obsShare').onclick = function (e) {
        e.preventDefault(); var url = location.href, self = this;
        (navigator.clipboard ? navigator.clipboard.writeText(url) : Promise.reject()).then(function () {
            self.innerHTML = '<i class="fas fa-check me-2"></i>' + L('¡Copiado!', 'Copied!');
            setTimeout(function () { self.innerHTML = '<i class="fas fa-link me-2"></i>' + L('Copiar enlace', 'Copy link'); }, 1800);
        }).catch(function () { window.prompt(L('Copia el enlace:', 'Copy the link:'), url); });
    };

    fromUrl();
    O.api('opciones').then(function (o) { opc = o; buildFilters(); renderChips(); load(); });
})();
