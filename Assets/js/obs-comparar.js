/* Observatorio Bufins – comparación de hasta 4 empresas */
(function () {
    'use strict';
    var root = document.getElementById('cmpApp');
    if (!root || typeof Chart === 'undefined' || !window.OBS) return;
    var O = OBS, L = O.L, $ = O.$, esc = O.esc, EMP = root.getAttribute('data-empresa-url');
    var NITS = [], D = null, Y = null, names = {};

    // [clave, es, en, formato, mayorMejor (null = informativo), grupo es/en]
    var ROWS = [
        { h: ['Tamaño', 'Size'] },
        ['ing', 'Ingresos', 'Revenue', O.money, true, 1], ['crec', 'Crecimiento de ingresos', 'Revenue growth', O.pct, true], ['ub', 'Utilidad bruta', 'Gross profit', O.money, true, 1],
        ['ebitda', 'EBITDA', 'EBITDA', O.money, true, 1], ['un', 'Utilidad neta', 'Net profit', O.money, true, 1], ['act', 'Activos', 'Assets', O.money, null, 1], ['pat', 'Patrimonio', 'Equity', O.money, null, 1],
        { h: ['Rentabilidad', 'Profitability'] },
        ['mbruto', 'Margen bruto', 'Gross margin', O.pct, true], ['moper', 'Margen operativo', 'Operating margin', O.pct, true], ['mebitda', 'Margen EBITDA', 'EBITDA margin', O.pct, true],
        ['mneto', 'Margen neto', 'Net margin', O.pct, true], ['roa', 'ROA', 'ROA', O.pct, true], ['roe', 'ROE', 'ROE', O.pct, true],
        { h: ['Endeudamiento', 'Leverage'] },
        ['deuda', 'Deuda financiera', 'Financial debt', O.money, null, 1], ['deudaebitda', 'Deuda / EBITDA', 'Debt / EBITDA', O.x, false], ['endeud', 'Pasivo / activo', 'Liabilities / assets', O.pct, false], ['cobertura', 'Cobertura de intereses', 'Interest coverage', O.x, true],
        { h: ['Liquidez y capital de trabajo', 'Liquidity & working capital'] },
        ['ktn', 'Capital de trabajo neto', 'Net working capital', O.money, null, 1], ['liquidez', 'Liquidez corriente', 'Current ratio', O.x, true], ['acida', 'Razón ácida', 'Quick ratio', O.x, true],
        ['diascxc', 'Días de cartera', 'Receivable days', O.days, false], ['diasinv', 'Días de inventario', 'Inventory days', O.days, false], ['diascxp', 'Días de proveedores', 'Payable days', O.days, true], ['ciclo', 'Ciclo de caja (días)', 'Cash cycle (days)', O.days, false]
    ];

    function fromUrl() {
        var u = new URL(location.href);
        NITS = (u.searchParams.get('nits') || '').split(/[|,]/).filter(Boolean).slice(0, 4);
        Y = parseInt(u.searchParams.get('anio'), 10) || null;
    }
    function toUrl() {
        var u = new URL(location.href);
        if (NITS.length) u.searchParams.set('nits', NITS.join('|')); else u.searchParams.delete('nits');
        if (Y) u.searchParams.set('anio', Y);
        history.replaceState(null, '', u.pathname + u.search);
    }

    // ---------- casillas de selección ----------
    function renderSlots() {
        var h = '';
        for (var i = 0; i < 4; i++) {
            var nit = NITS[i];
            if (nit) {
                var it = D && D.items.filter(function (x) { return x.nit === nit; })[0];
                h += '<div class="obs-slot filled" style="--c:' + O.CMP[i] + '"><span class="obs-slot-n">' + (i + 1) + '</span><div><a href="' + EMP + esc(nit) + '"><strong>' + esc(names[nit] || (it && it.razon) || nit) + '</strong></a><small>NIT ' + esc(nit) + (it ? ' · ' + esc(it.actividad) : '') + '</small></div>' +
                    '<button type="button" class="obs-slot-x" data-rm="' + esc(nit) + '" aria-label="' + L('Quitar', 'Remove') + '"><i class="fas fa-times"></i></button></div>';
            } else {
                h += '<div class="obs-slot" style="--c:' + O.CMP[i] + '"><span class="obs-slot-n">' + (i + 1) + '</span><div class="obs-input-icon"><i class="fas fa-search"></i><input type="search" class="form-control obs-slot-in" data-i="' + i + '" placeholder="' + (i < 2 ? L('Empresa ' + (i + 1) + ' (nombre o NIT)', 'Company ' + (i + 1) + ' (name or tax ID)') : L('Agregar otra (opcional)', 'Add another (optional)')) + '" /></div></div>';
            }
        }
        $('cmpSlots').innerHTML = h;
        Array.prototype.forEach.call(document.querySelectorAll('.obs-slot-in'), function (inp) {
            O.search(inp, { onSelect: function (it) { names[it.nit] = it.razon; if (NITS.indexOf(it.nit) < 0) NITS.push(it.nit); update(); } });
        });
    }
    $('cmpSlots').addEventListener('click', function (e) { var b = e.target.closest('[data-rm]'); if (!b) return; NITS = NITS.filter(function (x) { return x !== b.getAttribute('data-rm'); }); update(); });

    function update() {
        toUrl();
        if (NITS.length === 0) { D = null; $('cmpEmpty').hidden = false; $('cmpBody').hidden = true; renderSlots(); return; }
        O.api('series', 'nits=' + encodeURIComponent(NITS.join(','))).then(function (d) {
            D = d;
            NITS = NITS.filter(function (n) { return d.items.some(function (x) { return x.nit === n; }); });
            d.items.forEach(function (x) { names[x.nit] = x.razon; });
            if (!Y || d.anios.indexOf(Y) < 0) Y = d.anios[d.anios.length - 1];
            renderSlots(); render();
            O.track('comparar', String(d.items.length), d.items.map(function (x) { return x.nit; }).join(','));
        });
    }

    // ---------- render ----------
    function idx() { return D.anios.indexOf(Y); }
    function vOf(item, k) { var a = item.serie[k]; return a ? a[idx()] : null; }
    document.addEventListener('obs:unit', function () { if (D) render(); });
    function render() {
        var n = D.items.length;
        $('cmpEmpty').hidden = n >= 2; $('cmpBody').hidden = n < 2;
        if (n < 2) { $('cmpEmpty').innerHTML = '<i class="fas fa-balance-scale"></i><p>' + L('Agrega al menos otra empresa para comparar.', 'Add at least one more company to compare.') + '</p>'; return; }
        $('cmpYears').innerHTML = D.anios.slice().reverse().map(function (a) { return '<button type="button" data-y="' + a + '" class="' + (a === Y ? 'active' : '') + '">' + a + '</button>'; }).join('');

        // ganadores por indicador
        var wins = D.items.map(function () { return 0; }), total = 0;
        var body = ROWS.map(function (r) {
            if (r.h) return '<tr class="obs-grp"><td colspan="' + (n + 1) + '">' + L(r.h[0], r.h[1]) + '</td></tr>';
            var vals = D.items.map(function (it) { return vOf(it, r[0]); });
            var best = -1;
            if (r[4] !== null && r[4] !== undefined) {
                var cand = vals.map(function (v, i) { return { v: v, i: i }; }).filter(function (x) { return x.v != null && !isNaN(x.v); });
                if (cand.length >= 2) { cand.sort(function (a, b) { return r[4] ? b.v - a.v : a.v - b.v; }); best = cand[0].i; wins[best]++; total++; }
            }
            return '<tr><td>' + L(r[1], r[2]) + '</td>' + vals.map(function (v, i) {
                return '<td class="text-end' + (i === best ? ' best' : '') + (v != null && v < 0 && (r[3] === O.money) ? ' obs-neg' : '') + '">' + r[3](v) + '</td>';
            }).join('') + '</tr>';
        }).join('');
        $('cmpMatrix').innerHTML = '<thead><tr><th>' + Y + '</th>' + D.items.map(function (it, i) {
            return '<th class="text-end" style="--c:' + O.CMP[i] + '"><a href="' + esc(it.url || EMP + it.nit) + '"><span class="obs-dotc" style="background:' + O.CMP[i] + '"></span>' + esc(it.razon) + '</a><small>' + esc(it.actividad.length > 38 ? it.actividad.slice(0, 36) + '…' : it.actividad) + '</small></th>';
        }).join('') + '</tr></thead><tbody>' + body + '</tbody>';

        $('cmpWins').innerHTML = D.items.map(function (it, i) {
            return '<div class="obs-win" style="--c:' + O.CMP[i] + '"><strong>' + wins[i] + '</strong><span>' + L('de ', 'of ') + total + ' ' + L('indicadores', 'metrics') + '</span><small>' + esc(it.razon) + '</small></div>';
        }).join('');

        // gráficos: una serie por empresa
        function line(id, key, fmt, scaleMoney) {
            var max = 0;
            if (scaleMoney) D.items.forEach(function (it) { it.serie[key].forEach(function (v) { if (v != null) max = Math.max(max, Math.abs(v)); }); });
            var u = scaleMoney ? O.unitFor(max || 1) : { d: 1, l: '' };
            O.chart(id, {
                type: 'line',
                data: { labels: D.anios, datasets: D.items.map(function (it, i) { return { label: it.razon, data: it.serie[key].map(function (v) { return v == null ? null : v / u.d; }), borderColor: O.CMP[i], backgroundColor: O.CMP[i], tension: .3, spanGaps: true, pointRadius: 3 }; }) },
                options: Object.assign({}, O.COMMON, { plugins: { legend: { position: 'bottom', labels: { boxWidth: 12 } }, title: { display: !!u.l, text: u.l } }, scales: { y: { ticks: { callback: function (v) { return fmt ? fmt(v) : v; } } } } })
            });
        }
        line('chIng', 'ing', null, true);
        line('chCrec', 'crec', function (v) { return v + '%'; });
        line('chMebitda', 'mebitda', function (v) { return v + '%'; });
        line('chMneto', 'mneto', function (v) { return v + '%'; });
        line('chDeuda', 'deudaebitda', function (v) { return v + 'x'; });
        line('chLiq', 'liquidez', function (v) { return v + 'x'; });
        line('chCiclo', 'ciclo');
        line('chEndeud', 'endeud', function (v) { return v + '%'; });
    }

    $('cmpYears').addEventListener('click', function (e) { var b = e.target.closest('button'); if (!b) return; Y = parseInt(b.getAttribute('data-y'), 10); toUrl(); render(); });
    $('cmpPrint').addEventListener('click', function () { O.track('imprimir', 'comparar'); window.print(); });
    O.share($('cmpShare'));

    fromUrl(); renderSlots(); update();
})();
