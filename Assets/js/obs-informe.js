/* Observatorio Bufins – informe imprimible de una empresa (se guarda como PDF desde el navegador) */
(function () {
    'use strict';
    var root = document.getElementById('infApp');
    if (!root || typeof Chart === 'undefined' || !window.OBS) return;
    var O = OBS, L = O.L, $ = O.$;
    var NIT = root.getAttribute('data-nit'), Y = parseInt(root.getAttribute('data-anio'), 10);

    $('infPrint').addEventListener('click', function () { window.print(); });

    var FILAS = [
        ['mebitda', 'Margen EBITDA', 'EBITDA margin', O.pct], ['mneto', 'Margen neto', 'Net margin', O.pct], ['roa', 'ROA', 'ROA', O.pct], ['roe', 'ROE', 'ROE', O.pct],
        ['endeud', 'Endeudamiento (pasivo/activo)', 'Leverage (liabilities/assets)', O.pct], ['deudaebitda', 'Deuda / EBITDA', 'Debt / EBITDA', O.x],
        ['liquidez', 'Liquidez corriente', 'Current ratio', O.x], ['ktingresos', 'Capital de trabajo / ingresos', 'Working capital / revenue', O.pct],
        ['ciclo', 'Ciclo de conversión de efectivo (días)', 'Cash conversion cycle (days)', O.days], ['score', 'Bufins Score de salud', 'Bufins health score', function (v) { return v == null ? '—' : O.int(v); }]
    ];

    O.api('ficha', 'nit=' + encodeURIComponent(NIT)).then(function (D) {
        var s = D.serie, a = s.anios;
        var max = Math.max.apply(null, s.ing.concat(s.ebitda).map(function (v) { return Math.abs(v || 0); }).concat([1])), u = O.unitFor(max);
        var sc = function (arr) { return arr.map(function (v) { return v == null ? null : v / u.d; }); };
        new Chart($('infChart'), {
            type: 'bar',
            data: { labels: a, datasets: [
                { label: L('Ingresos', 'Revenue') + ' (' + u.l + ')', data: sc(s.ing), backgroundColor: '#583AFF', borderRadius: 4 },
                { label: 'EBITDA', data: sc(s.ebitda), backgroundColor: '#01B3AB', borderRadius: 4 },
                { label: L('Utilidad neta', 'Net profit'), data: sc(s.un), backgroundColor: '#FF5500', borderRadius: 4 }
            ] },
            options: { responsive: true, maintainAspectRatio: false, animation: false, plugins: { legend: { position: 'bottom', labels: { boxWidth: 10 } } }, scales: { y: { beginAtZero: true } } }
        });
        var b = D.bench[Y];
        if (!b) { $('infBench').innerHTML = '<tbody><tr><td>' + L('Sin datos de comparación.', 'No benchmark data.') + '</td></tr></tbody>'; return; }
        $('infWho').textContent = '(' + O.int(b.empresas) + ' ' + L('empresas comparables', 'comparable companies') + (b.nombre ? ' · ' + b.nombre : '') + ')';
        var rows = FILAS.filter(function (f) { return b.ratios[f[0]]; }).map(function (f) {
            var r = b.ratios[f[0]];
            return '<tr><td>' + L(f[1], f[2]) + '</td><td>' + f[3](r.valor) + '</td><td>' + f[3](r.med) + '</td><td>' + f[3](r.p25) + ' – ' + f[3](r.p75) + '</td><td>' + (r.mejorQue == null ? '—' : L('Mejor que ', 'Better than ') + O.int(r.mejorQue) + '%') + '</td></tr>';
        }).join('');
        $('infBench').innerHTML = '<thead><tr><th>' + L('Indicador', 'Metric') + '</th><th>' + L('Empresa', 'Company') + '</th><th>' + L('Mediana', 'Median') + '</th><th>P25 – P75</th><th>' + L('Posición', 'Position') + '</th></tr></thead><tbody>' + rows + '</tbody>';
    });
})();
