/* Observatorio Bufins – análisis individual de una empresa */
(function () {
    'use strict';
    var root = document.getElementById('empApp');
    if (!root || typeof Chart === 'undefined' || !window.OBS) return;
    var O = OBS, L = O.L, $ = O.$, esc = O.esc;
    var NIT = root.getAttribute('data-nit');
    var D = null, Y = parseInt(root.getAttribute('data-anio'), 10), STM = 'pyg', MODE = 'v';

    // ---------- definición de indicadores (mismas claves que el servidor) ----------
    var RATIOS = [
        ['mbruto', 'Margen bruto', 'Gross margin', O.pct, 'Rentabilidad', 'Profitability'],
        ['moper', 'Margen operativo', 'Operating margin', O.pct, 'Rentabilidad', 'Profitability'],
        ['mebitda', 'Margen EBITDA', 'EBITDA margin', O.pct, 'Rentabilidad', 'Profitability'],
        ['mneto', 'Margen neto', 'Net margin', O.pct, 'Rentabilidad', 'Profitability'],
        ['roa', 'ROA', 'ROA', O.pct, 'Rentabilidad', 'Profitability'],
        ['roe', 'ROE', 'ROE', O.pct, 'Rentabilidad', 'Profitability'],
        ['endeud', 'Endeudamiento (pasivo/activo)', 'Leverage (liabilities/assets)', O.pct, 'Endeudamiento', 'Leverage'],
        ['deudaebitda', 'Deuda / EBITDA', 'Debt / EBITDA', O.x, 'Endeudamiento', 'Leverage'],
        ['cobertura', 'Cobertura de intereses (EBITDA / costos fin.)', 'Interest coverage (EBITDA / finance costs)', O.x, 'Endeudamiento', 'Leverage'],
        ['liquidez', 'Liquidez corriente', 'Current ratio', O.x, 'Liquidez', 'Liquidity'],
        ['acida', 'Razón ácida', 'Quick ratio', O.x, 'Liquidez', 'Liquidity'],
        ['ktingresos', 'Capital de trabajo / ingresos', 'Working capital / revenue', O.pct, 'Eficiencia', 'Efficiency'],
        ['diascxc', 'Días de cartera', 'Receivable days', O.days, 'Eficiencia', 'Efficiency'],
        ['diasinv', 'Días de inventario', 'Inventory days', O.days, 'Eficiencia', 'Efficiency'],
        ['diascxp', 'Días de proveedores', 'Payable days', O.days, 'Eficiencia', 'Efficiency'],
        ['ciclo', 'Ciclo de conversión de efectivo (días)', 'Cash conversion cycle (days)', O.days, 'Eficiencia', 'Efficiency']
    ];
    var RDEF = {}; RATIOS.forEach(function (r) { RDEF[r[0]] = r; });

    // ---------- estados financieros (filas) ----------
    function g(y, k) { var r = D.rubros[y]; return r && r[k] != null ? r[k] : 0; }
    function has(y, k) { var r = D.rubros[y]; return !!(r && r[k] != null); }
    var dc = function (y) { return has(y, 'opfc') ? g(y, 'opfc') : g(y, 'pcor') + g(y, 'pcpnc'); };
    var dnc = function (y) { return has(y, 'opfnc') ? g(y, 'opfnc') : g(y, 'pnc'); };
    var S = {
        pyg: [
            { k: 'ing', es: 'Ingresos', en: 'Revenue', t: 'total' },
            { k: 'costo', es: 'Costo de ventas', en: 'Cost of sales' },
            { k: 'ub', es: 'Utilidad bruta', en: 'Gross profit', t: 'sub' },
            { k: 'gadm', es: 'Gastos de administración', en: 'Administrative expenses' },
            { k: 'gven', es: 'Gastos de ventas', en: 'Selling expenses' },
            { k: 'gdis', es: 'Costos de distribución', en: 'Distribution costs' },
            { k: 'ogas', es: 'Otros gastos operacionales', en: 'Other operating expenses' },
            { k: 'gop', es: 'Total gastos operacionales', en: 'Total operating expenses', t: 'sub' },
            { k: 'oing', es: 'Otros ingresos operacionales', en: 'Other operating income' },
            { k: 'ogan', es: 'Otras ganancias (pérdidas)', en: 'Other gains (losses)' },
            { k: 'uop', es: 'Utilidad operativa', en: 'Operating profit', t: 'sub' },
            { k: 'da', es: 'Depreciación y amortización', en: 'Depreciation & amortization' },
            { k: 'ebitda', es: 'EBITDA', en: 'EBITDA', t: 'sub' },
            { k: 'ifin', es: 'Ingresos financieros', en: 'Finance income' },
            { k: 'cfin', es: 'Costos financieros', en: 'Finance costs' },
            { k: 'uai', es: 'Utilidad antes de impuestos', en: 'Profit before tax', t: 'sub' },
            { k: 'imp', es: 'Impuestos', en: 'Income tax' },
            { k: 'un', es: 'Utilidad neta', en: 'Net profit', t: 'total' }
        ],
        kt: [
            { h: ['Capital de trabajo', 'Working capital'] },
            { k: 'cxc', es: 'Cuentas por cobrar comerciales', en: 'Trade receivables' },
            { k: 'inv', es: 'Inventarios', en: 'Inventories' },
            { k: 'aimp', es: 'Impuestos a favor', en: 'Tax assets' },
            { k: 'oanf', es: 'Otros activos corrientes operativos', en: 'Other operating current assets' },
            { k: '_awk', es: 'Activos operativos', en: 'Operating assets', t: 'sub', f: function (y) { return g(y, 'cxc') + g(y, 'inv') + g(y, 'aimp') + g(y, 'oanf'); } },
            { k: 'cxp', es: 'Proveedores y cuentas por pagar', en: 'Trade payables' },
            { k: 'pbe', es: 'Obligaciones laborales', en: 'Employee benefits' },
            { k: 'pimp', es: 'Impuestos por pagar', en: 'Tax payables' },
            { k: 'oprov', es: 'Provisiones', en: 'Provisions' },
            { k: '_pwk', es: 'Pasivos operativos', en: 'Operating liabilities', t: 'sub', f: function (y) { return g(y, 'cxp') + g(y, 'pbe') + g(y, 'pimp') + g(y, 'oprov'); } },
            { k: 'ktn', es: 'Capital de trabajo neto', en: 'Net working capital', t: 'total' },
            { h: ['Días de capital de trabajo', 'Working capital days'] },
            { k: '_dcxc', es: 'Días de cartera', en: 'Receivable days', d: 1, f: function (y) { return D.serie.diascxc[ix(y)]; } },
            { k: '_dinv', es: 'Días de inventario', en: 'Inventory days', d: 1, f: function (y) { return D.serie.diasinv[ix(y)]; } },
            { k: '_dcxp', es: 'Días de proveedores', en: 'Payable days', d: 1, f: function (y) { return D.serie.diascxp[ix(y)]; } },
            { k: '_ciclo', es: 'Ciclo de conversión de efectivo', en: 'Cash conversion cycle', t: 'sub', d: 1, f: function (y) { return D.serie.ciclo[ix(y)]; } },
            { h: ['Activos de largo plazo', 'Long-term assets'] },
            { k: 'ppye', es: 'Propiedades, planta y equipo', en: 'Property, plant & equipment' },
            { k: 'intang', es: 'Intangibles', en: 'Intangibles' },
            { k: 'plus', es: 'Plusvalía', en: 'Goodwill' },
            { k: 'capex', es: 'CapEx (compras de PPyE)', en: 'CapEx (PP&E purchases)' }
        ],
        bal: [
            { h: ['Activos', 'Assets'] },
            { k: 'caja', es: 'Efectivo y equivalentes', en: 'Cash & equivalents' },
            { k: 'cxc', es: 'Cuentas por cobrar comerciales', en: 'Trade receivables' },
            { k: 'inv', es: 'Inventarios', en: 'Inventories' },
            { k: 'aimp', es: 'Activos por impuestos corrientes', en: 'Current tax assets' },
            { k: 'oanf', es: 'Otros activos no financieros corrientes', en: 'Other non-financial current assets' },
            { k: '_oac', es: 'Otros activos corrientes', en: 'Other current assets', f: function (y) { return g(y, 'ac') - g(y, 'caja') - g(y, 'cxc') - g(y, 'inv') - g(y, 'aimp') - g(y, 'oanf'); }, o: 1 },
            { k: 'ac', es: 'Total activos corrientes', en: 'Total current assets', t: 'sub' },
            { k: 'ppye', es: 'Propiedades, planta y equipo', en: 'Property, plant & equipment' },
            { k: 'intang', es: 'Intangibles', en: 'Intangibles' },
            { k: 'plus', es: 'Plusvalía', en: 'Goodwill' },
            { k: '_oanc', es: 'Otros activos no corrientes', en: 'Other non-current assets', f: function (y) { return g(y, 'anc') - g(y, 'ppye') - g(y, 'intang') - g(y, 'plus'); }, o: 1 },
            { k: 'anc', es: 'Total activos no corrientes', en: 'Total non-current assets', t: 'sub' },
            { k: 'act', es: 'TOTAL ACTIVOS', en: 'TOTAL ASSETS', t: 'total' },
            { h: ['Pasivos y patrimonio', 'Liabilities & equity'] },
            { k: 'cxp', es: 'Proveedores y cuentas por pagar', en: 'Trade payables' },
            { k: 'pbe', es: 'Obligaciones laborales', en: 'Employee benefits' },
            { k: 'pimp', es: 'Impuestos por pagar', en: 'Tax payables' },
            { k: '_dc', es: 'Deuda financiera corriente', en: 'Current financial debt', f: dc },
            { k: '_opc', es: 'Otros pasivos corrientes', en: 'Other current liabilities', f: function (y) { return g(y, 'pc') - g(y, 'cxp') - g(y, 'pbe') - g(y, 'pimp') - dc(y); }, o: 1 },
            { k: 'pc', es: 'Total pasivos corrientes', en: 'Total current liabilities', t: 'sub' },
            { k: '_dnc', es: 'Deuda financiera no corriente', en: 'Non-current financial debt', f: dnc },
            { k: '_opnc', es: 'Otros pasivos no corrientes', en: 'Other non-current liabilities', f: function (y) { return g(y, 'pnct') - dnc(y); }, o: 1 },
            { k: 'pnct', es: 'Total pasivos no corrientes', en: 'Total non-current liabilities', t: 'sub' },
            { k: 'pas', es: 'TOTAL PASIVOS', en: 'TOTAL LIABILITIES', t: 'total' },
            { k: 'capital', es: 'Capital emitido', en: 'Share capital' },
            { k: 'gacum', es: 'Ganancias acumuladas', en: 'Retained earnings' },
            { k: '_opat', es: 'Otras partidas de patrimonio', en: 'Other equity items', f: function (y) { return g(y, 'pat') - g(y, 'capital') - g(y, 'gacum'); }, o: 1 },
            { k: 'pat', es: 'TOTAL PATRIMONIO', en: 'TOTAL EQUITY', t: 'total' }
        ],
        flu: [
            { k: 'ebitda', es: 'EBITDA', en: 'EBITDA', t: 'sub' },
            { k: 'fco', es: 'Flujo de caja operativo', en: 'Operating cash flow', t: 'total' },
            { k: 'capex', es: 'CapEx (compras de PPyE)', en: 'CapEx (PP&E purchases)' },
            { k: 'fci', es: 'Flujo de caja de inversión', en: 'Investing cash flow', t: 'total' },
            { k: 'prest_in', es: 'Préstamos recibidos', en: 'Loans received' },
            { k: 'prest_out', es: 'Pago de préstamos', en: 'Loan repayments' },
            { k: 'intpag', es: 'Intereses pagados', en: 'Interest paid' },
            { k: 'divpag', es: 'Dividendos pagados', en: 'Dividends paid' },
            { k: 'fcf', es: 'Flujo de caja de financiación', en: 'Financing cash flow', t: 'total' },
            { k: 'dcaja', es: 'Variación neta del efectivo', en: 'Net change in cash', t: 'sub' },
            { k: 'cajai', es: 'Efectivo al inicio', en: 'Opening cash' },
            { k: 'cajaf', es: 'Efectivo al final', en: 'Closing cash', t: 'total' },
            { h: ['Indicadores de caja', 'Cash indicators'] },
            { k: '_conv', es: 'Conversión de caja (FCO / EBITDA)', en: 'Cash conversion (OCF / EBITDA)', pct: 1, f: function (y) { return g(y, 'ebitda') > 0 ? g(y, 'fco') / g(y, 'ebitda') * 100 : null; } },
            { k: '_fcl', es: 'Flujo libre aprox. (FCO − CapEx)', en: 'Approx. free cash flow (OCF − CapEx)', t: 'sub', f: function (y) { return g(y, 'fco') - g(y, 'capex'); } }
        ]
    };
    var BASE = { pyg: 'ing', kt: 'ing', bal: 'act', flu: 'ing' };
    var TITLES = { pyg: ['Estado de resultados', 'Income statement'], kt: ['Capital de trabajo y activos de largo plazo', 'Working capital & long-term assets'], bal: ['Balance general', 'Balance sheet'], flu: ['Flujo de caja', 'Cash flow'] };

    function ix(y) { return D.serie.anios.indexOf(y); }
    function val(row, y) { return row.f ? row.f(y) : g(y, row.k); }

    // ---------- resumen ----------
    var kpi = O.kpi;
    function renderKpis() {
        var s = D.serie, i = ix(Y), p = i > 0 ? i - 1 : -1;
        function prev(k) { return p >= 0 ? s[k][p] : null; }
        var gop = s.gop[i], ing = s.ing[i];
        $('empKpis').innerHTML =
            kpi(L('Ingresos', 'Revenue') + ' ' + Y, O.money(s.ing[i]), O.delta(s.ing[i], prev('ing'))) +
            kpi(L('Utilidad bruta', 'Gross profit'), O.money(s.ub[i]), '<em>' + L('Margen', 'Margin') + ' ' + O.pct(s.mbruto[i]) + '</em>') +
            kpi(L('Gastos operacionales', 'Operating expenses'), O.money(gop), '<em>' + O.pct(ing > 0 ? gop / ing * 100 : null) + ' ' + L('de ingresos', 'of revenue') + '</em>') +
            kpi('EBITDA', O.money(s.ebitda[i]), '<em>' + L('Margen', 'Margin') + ' ' + O.pct(s.mebitda[i]) + '</em>', s.ebitda[i] < 0 ? 'neg' : '') +
            kpi(L('Capital de trabajo neto', 'Net working capital'), O.money(s.ktn[i]), '<em>' + O.pct(s.ktingresos[i]) + ' ' + L('de ingresos', 'of revenue') + '</em>', s.ktn[i] < 0 ? 'neg' : '') +
            kpi(L('Deuda financiera', 'Financial debt'), O.money(s.deuda[i]), '<em>' + L('Deuda/EBITDA', 'Debt/EBITDA') + ' ' + O.x(s.deudaebitda[i]) + '</em>') +
            kpi(L('Utilidad neta', 'Net profit'), O.money(s.un[i]), '<em>' + L('Margen', 'Margin') + ' ' + O.pct(s.mneto[i]) + '</em>', s.un[i] < 0 ? 'neg' : '') +
            kpi(L('Activos', 'Assets'), O.money(s.act[i]), '<em>ROA ' + O.pct(s.roa[i]) + '</em>') +
            kpi(L('Patrimonio', 'Equity'), O.money(s.pat[i]), '<em>' + L('Pasivo/Activo', 'Liabilities/Assets') + ' ' + O.pct(s.endeud[i]) + '</em>') +
            kpi(L('Liquidez corriente', 'Current ratio'), O.x(s.liquidez[i]), '<em>' + L('Ciclo de caja', 'Cash cycle') + ' ' + O.days(s.ciclo[i]) + ' ' + L('días', 'days') + '</em>');
    }
    function renderCharts() {
        var s = D.serie, a = s.anios;
        var gastosPct = a.map(function (_, i) { return s.ing[i] > 0 ? Math.round(s.gop[i] / s.ing[i] * 1000) / 10 : null; });
        O.combo('chIng', a, { label: L('Ingresos', 'Revenue'), data: s.ing }, { label: L('Crecimiento %', 'Growth %'), data: s.crec }, Y, '#583AFF');
        O.combo('chUb', a, { label: L('Utilidad bruta', 'Gross profit'), data: s.ub }, { label: L('Margen bruto %', 'Gross margin %'), data: s.mbruto }, Y, '#14B8A6');
        O.combo('chGop', a, { label: L('Gastos operacionales', 'Operating expenses'), data: s.gop }, { label: L('% de ingresos', '% of revenue'), data: gastosPct }, Y, '#8F7BFF');
        O.combo('chEbitda', a, { label: 'EBITDA', data: s.ebitda }, { label: L('Margen EBITDA %', 'EBITDA margin %'), data: s.mebitda }, Y, '#160933');
        O.combo('chKtn', a, { label: L('Capital de trabajo neto', 'Net working capital'), data: s.ktn }, { label: L('% de ingresos', '% of revenue'), data: s.ktingresos }, Y, '#0EA5E9');
        O.combo('chDeuda', a, { label: L('Deuda', 'Debt'), data: s.deuda }, { label: 'Deuda/EBITDA', data: s.deudaebitda, fmt: function (v) { return v + 'x'; } }, Y, '#FF9A66');
    }

    function renderInsights() {
        var b = D.bench[Y], s = D.serie, i = ix(Y), out = [];
        function add(type, txt) { out.push('<li class="' + type + '"><i class="fas ' + (type === 'good' ? 'fa-check-circle' : type === 'bad' ? 'fa-exclamation-triangle' : 'fa-info-circle') + '"></i><span>' + txt + '</span></li>'); }
        if (s.un[i] < 0) add('bad', L('Cerró ' + Y + ' con <strong>pérdida neta</strong> de ' + O.money(s.un[i]) + '.', 'Closed ' + Y + ' with a <strong>net loss</strong> of ' + O.money(s.un[i]) + '.'));
        if (s.ebitda[i] < 0) add('bad', L('El <strong>EBITDA es negativo</strong>: la operación no genera caja antes de intereses e impuestos.', '<strong>EBITDA is negative</strong>: operations do not generate cash before interest and tax.'));
        if (s.liquidez[i] != null && s.liquidez[i] < 1) add('bad', L('<strong>Liquidez corriente de ' + O.x(s.liquidez[i]) + '</strong>: sus activos corrientes no cubren los pasivos corrientes.', '<strong>Current ratio of ' + O.x(s.liquidez[i]) + '</strong>: current assets do not cover current liabilities.'));
        if (s.deudaebitda[i] != null && s.deudaebitda[i] > 4) add('bad', L('Apalancamiento alto: <strong>deuda / EBITDA de ' + O.x(s.deudaebitda[i]) + '</strong>.', 'High leverage: <strong>debt / EBITDA of ' + O.x(s.deudaebitda[i]) + '</strong>.'));
        if (s.crec[i] != null) add(s.crec[i] >= 0 ? 'good' : 'bad', L('Ingresos ' + (s.crec[i] >= 0 ? 'crecieron ' : 'cayeron ') + O.pct(Math.abs(s.crec[i])) + ' frente a ' + (Y - 1) + '.', 'Revenue ' + (s.crec[i] >= 0 ? 'grew ' : 'fell ') + O.pct(Math.abs(s.crec[i])) + ' versus ' + (Y - 1) + '.'));
        if (b) {
            var arr = [];
            Object.keys(b.ratios).forEach(function (k) { var r = b.ratios[k]; if (r.mejorQue != null && RDEF[k]) arr.push({ k: k, r: r }); });
            arr.sort(function (x, y) { return y.r.mejorQue - x.r.mejorQue; });
            arr.slice(0, 2).filter(function (x) { return x.r.mejorQue >= 70; }).forEach(function (x) { add('good', L('<strong>' + RDEF[x.k][1] + '</strong> (' + RDEF[x.k][3](x.r.valor) + ') es mejor que el ' + O.int(x.r.mejorQue) + '% de su sector.', '<strong>' + RDEF[x.k][2] + '</strong> (' + RDEF[x.k][3](x.r.valor) + ') beats ' + O.int(x.r.mejorQue) + '% of its sector.')); });
            arr.slice(-2).reverse().filter(function (x) { return x.r.mejorQue <= 30; }).forEach(function (x) { add('bad', L('<strong>' + RDEF[x.k][1] + '</strong> (' + RDEF[x.k][3](x.r.valor) + ') está por debajo del ' + O.int(100 - x.r.mejorQue) + '% de su sector.', '<strong>' + RDEF[x.k][2] + '</strong> (' + RDEF[x.k][3](x.r.valor) + ') is behind ' + O.int(100 - x.r.mejorQue) + '% of its sector.')); });
        }
        $('empInsights').innerHTML = out.length ? '<h3>' + L('Lectura rápida', 'Quick read') + ' · ' + Y + '</h3><ul>' + out.slice(0, 6).join('') + '</ul>' : '';
    }

    // ---------- frente al sector ----------
    function renderBench() {
        var b = D.bench[Y];
        if (!b) { $('benchTable').innerHTML = '<tr><td class="text-muted">' + L('Sin datos de comparación para este año.', 'No benchmark data for this year.') + '</td></tr>'; $('benchWho').textContent = ''; return; }
        var nivel = { clase: L('clase', 'class'), grupo: L('grupo', 'group'), division: L('división', 'division'), seccion: L('sección', 'section'), todas: L('todas las empresas', 'all companies') }[b.nivel];
        $('benchWho').innerHTML = L('Comparada con ', 'Compared with ') + '<strong>' + O.int(b.empresas) + '</strong> ' + L('empresas de la ', 'companies in the ') + nivel + ' ' + (b.codigo ? '<strong>' + esc(b.codigo) + ' · ' + esc(b.nombre) + '</strong>' : '');
        var cat = '', rows = [];
        RATIOS.forEach(function (d) {
            var r = b.ratios[d[0]]; if (!r) return;
            var c = L(d[4], d[5]);
            if (c !== cat) { rows.push('<tr class="obs-grp"><td colspan="5">' + c + '</td></tr>'); cat = c; }
            var lo = Math.min(r.p25, r.med, r.valor == null ? r.p25 : r.valor), hi = Math.max(r.p75, r.med, r.valor == null ? r.p75 : r.valor);
            var pad = (hi - lo) * .12 || 1; lo -= pad; hi += pad;
            var pos = function (x) { return Math.max(0, Math.min(100, (x - lo) / (hi - lo) * 100)); };
            var tone = r.mejorQue == null ? '' : r.mejorQue >= 66 ? 'good' : r.mejorQue >= 33 ? 'mid' : 'bad';
            rows.push('<tr><td>' + L(d[1], d[2]) + '</td><td class="text-end"><strong>' + d[3](r.valor) + '</strong></td><td class="text-end">' + d[3](r.med) + '</td>' +
                '<td class="obs-rng"><div class="obs-rng-bar"><span class="band" style="left:' + pos(r.p25) + '%;width:' + (pos(r.p75) - pos(r.p25)) + '%"></span><span class="med" style="left:' + pos(r.med) + '%"></span>' +
                (r.valor != null ? '<span class="dot ' + tone + '" style="left:' + pos(r.valor) + '%"></span>' : '') + '</div><small>' + d[3](r.p25) + ' – ' + d[3](r.p75) + '</small></td>' +
                '<td class="text-end">' + (r.mejorQue == null ? '—' : '<span class="obs-pill ' + tone + '">' + L('Mejor que ', 'Better than ') + O.int(r.mejorQue) + '%</span>') + '</td></tr>');
        });
        $('benchTable').innerHTML = '<thead><tr><th>' + L('Indicador', 'Metric') + '</th><th class="text-end">' + L('Empresa', 'Company') + '</th><th class="text-end">' + L('Mediana', 'Median') + '</th><th>' + L('Rango P25–P75', 'P25–P75 range') + '</th><th class="text-end">' + L('Posición', 'Position') + '</th></tr></thead><tbody>' + rows.join('') + '</tbody>';
    }

    // ---------- estados ----------
    function fmtCell(row, v, mode, base, prevV) {
        if (v == null || isNaN(v)) return '—';
        if (row.d) return mode === 'v' ? O.days(v) : '';
        if (row.pct) return mode === 'v' ? O.pct(v) : '';
        if (mode === 'p') return base ? O.pct(v / base * 100) : '—';
        if (mode === 'g') return prevV != null && Math.abs(prevV) > 1e-9 ? (function (x) { return (x >= 0 ? '+' : '') + O.pct(x); })((v - prevV) / Math.abs(prevV) * 100) : '—';
        return Math.abs(v) < 0.5 && v !== 0 ? O.num(v) : (v === 0 ? '–' : O.full(v));
    }
    function renderStatement() {
        var rows = S[STM], anios = D.serie.anios;
        var head = '<thead><tr><th></th>' + anios.map(function (a) { return '<th class="text-end' + (a === Y ? ' sel' : '') + '">' + a + '</th>'; }).join('') + '</tr></thead>';
        var body = rows.map(function (r) {
            if (r.h) return '<tr class="obs-grp"><td colspan="' + (anios.length + 1) + '">' + L(r.h[0], r.h[1]) + '</td></tr>';
            if (r.o && anios.every(function (a) { return Math.abs(val(r, a) || 0) < 0.5; })) return '';
            var tds = anios.map(function (a, i) {
                if (!D.rubros[a]) return '<td class="text-end text-muted">—</td>';
                var v = val(r, a), prevV = i > 0 && D.rubros[anios[i - 1]] ? val(r, anios[i - 1]) : null;
                var base = g(a, BASE[STM]);
                return '<td class="text-end' + (a === Y ? ' sel' : '') + (v != null && v < 0 && !r.d && !r.pct ? ' obs-neg' : '') + '">' + fmtCell(r, v, MODE, base, prevV) + '</td>';
            }).join('');
            return '<tr class="' + (r.t || '') + '"><td>' + L(r.es, r.en) + '</td>' + tds + '</tr>';
        }).join('');
        $('stmtTable').innerHTML = head + '<tbody>' + body + '</tbody>';
        $('stmtTitle').textContent = L(TITLES[STM][0], TITLES[STM][1]);
        renderStmtChart();
    }
    function renderStmtChart() {
        var a = D.serie.anios, s = D.serie, id = 'chStmt', m;
        function ds(label, data, color, type, extra) { return Object.assign({ type: type || 'bar', label: label, data: data, backgroundColor: color, borderColor: color, borderRadius: type === 'line' ? 0 : 5, tension: .3 }, extra || {}); }
        var max = Math.max.apply(null, s.ing.concat(s.act).map(function (v) { return Math.abs(v || 0); }).concat([1])), u = O.unitFor(max);
        var sc = function (arr) { return arr.map(function (v) { return v == null ? null : v / u.d; }); };
        var cfg;
        if (STM === 'pyg') { $('stmtChartTitle').textContent = L('Ingresos, EBITDA y utilidad neta', 'Revenue, EBITDA and net profit') + ' (' + u.l + ')'; cfg = { datasets: [ds(L('Ingresos', 'Revenue'), sc(s.ing), '#583AFF'), ds('EBITDA', sc(s.ebitda), '#01E1D7'), ds(L('Utilidad neta', 'Net profit'), sc(s.un), '#FF5500')] }; }
        else if (STM === 'kt') { $('stmtChartTitle').textContent = L('Días de capital de trabajo', 'Working capital days'); cfg = { datasets: [ds(L('Cartera', 'Receivables'), s.diascxc, '#583AFF', 'line'), ds(L('Inventario', 'Inventory'), s.diasinv, '#01B3AB', 'line'), ds(L('Proveedores', 'Payables'), s.diascxp, '#FF5500', 'line'), ds(L('Ciclo', 'Cycle'), s.ciclo, '#160933', 'line', { borderDash: [6, 4] })] }; }
        else if (STM === 'bal') { $('stmtChartTitle').textContent = L('Estructura de financiación', 'Funding structure') + ' (' + u.l + ')'; cfg = { datasets: [ds(L('Patrimonio', 'Equity'), sc(s.pat), '#583AFF', 'bar', { stack: 'a' }), ds(L('Deuda financiera', 'Financial debt'), sc(s.deuda), '#FF5500', 'bar', { stack: 'a' }), ds(L('Otros pasivos', 'Other liabilities'), sc(a.map(function (_, i) { return Math.max(0, s.pas[i] - s.deuda[i]); })), '#01E1D7', 'bar', { stack: 'a' })] }; }
        else { $('stmtChartTitle').textContent = L('EBITDA vs. flujo operativo', 'EBITDA vs. operating cash flow') + ' (' + u.l + ')'; cfg = { datasets: [ds('EBITDA', sc(s.ebitda), '#160933'), ds(L('Flujo operativo', 'Operating cash flow'), sc(s.fco), '#01E1D7')] }; }
        cfg.labels = a;
        cfg.stacked = STM === 'bal';
        O.chart(id, { data: { labels: a, datasets: cfg.datasets }, options: Object.assign({}, O.COMMON, { plugins: { legend: { position: 'bottom', labels: { boxWidth: 12 } } }, scales: { y: { beginAtZero: STM !== 'kt', stacked: cfg.stacked }, x: { stacked: cfg.stacked } } }) });
    }

    // ---------- perfil y glosario ----------
    function renderPerfil() {
        var p = D.perfil, edad = '';
        if (p.fconst) { var y = parseInt(p.fconst.slice(0, 4), 10); if (y) edad = ' (' + (new Date().getFullYear() - y) + ' ' + L('años', 'years') + ')'; }
        function cell(k, v) { return '<div class="obs-info"><span>' + k + '</span><strong>' + (v ? esc(v) : '—') + '</strong></div>'; }
        $('perfilBody').innerHTML = '<div class="obs-info-grid">' +
            cell(L('Razón social', 'Company name'), p.razon) + cell('NIT', p.nit) +
            cell(L('Actividad principal (CIIU)', 'Main activity (ISIC)'), p.ciiu + ' · ' + p.actividad) +
            cell(L('Sector', 'Sector'), p.seccion + ' · ' + p.seccionNombre) + cell(L('División', 'Division'), p.division + ' · ' + p.divisionNombre) +
            cell(L('Fecha de constitución', 'Incorporation date'), p.fconst + edad) + cell(L('Estado actual', 'Current status'), p.estado) +
            cell(L('Ciudad / departamento', 'City / department'), p.ciudad + ' · ' + p.depto) + cell(L('Tipo societario', 'Company type'), p.tipo) +
            cell(L('Marco contable', 'Accounting framework'), p.grupo) + cell(L('Tamaño por ingresos', 'Size by revenue'), D.tamano + ' (S < 20.000 M · M < 100.000 M · L < 1 B)') +
            cell(L('Revisor fiscal', 'Statutory auditor'), p.rf) + cell(L('Concepto del revisor fiscal', 'Auditor opinion'), p.rfop) + '</div>' +
            '<div class="obs-info obs-info-wide"><span>' + L('Objeto social principal', 'Main corporate purpose') + '</span><p>' + esc(p.objeto || '—') + '</p></div>';
    }
    var GLOS = [
        ['EBITDA', 'Utilidad operativa + depreciación y amortización (flujo de efectivo). Aproxima la caja que genera la operación antes de intereses e impuestos.', 'Operating profit + depreciation & amortization (cash-flow statement). Approximates cash generated by operations before interest and tax.'],
        ['Utilidad bruta', 'Ingresos − costo de ventas (valor reportado).', 'Revenue − cost of sales (as reported).'],
        ['Gastos operacionales', 'Gastos de administración + ventas + distribución + otros gastos operacionales.', 'Administrative + selling + distribution + other operating expenses.'],
        ['Capital de trabajo neto', '(Cartera + inventarios + impuestos a favor + otros activos operativos corrientes) − (proveedores + obligaciones laborales + impuestos por pagar + provisiones). Excluye caja y deuda financiera.', '(Receivables + inventories + tax assets + other operating current assets) − (payables + employee benefits + taxes payable + provisions). Excludes cash and financial debt.'],
        ['Deuda financiera', 'Otros pasivos financieros (o préstamos) corrientes y no corrientes. No incluye proveedores.', 'Other financial liabilities (or loans), current and non-current. Excludes trade payables.'],
        ['Deuda / EBITDA', 'Años de EBITDA necesarios para pagar la deuda financiera. Solo se calcula con EBITDA positivo.', 'Years of EBITDA needed to repay financial debt. Only computed when EBITDA is positive.'],
        ['Endeudamiento', 'Total pasivos / total activos.', 'Total liabilities / total assets.'],
        ['Liquidez corriente', 'Activos corrientes / pasivos corrientes.', 'Current assets / current liabilities.'],
        ['Razón ácida', '(Activos corrientes − inventarios) / pasivos corrientes.', '(Current assets − inventories) / current liabilities.'],
        ['Días de cartera / inventario / proveedores', 'Cartera ÷ ingresos × 365; inventario ÷ costo de ventas × 365; proveedores ÷ costo de ventas × 365.', 'Receivables ÷ revenue × 365; inventories ÷ cost of sales × 365; payables ÷ cost of sales × 365.'],
        ['Ciclo de conversión de efectivo', 'Días de cartera + días de inventario − días de proveedores.', 'Receivable days + inventory days − payable days.'],
        ['ROA / ROE', 'Utilidad neta / activos; utilidad neta / patrimonio (cierre de año).', 'Net profit / assets; net profit / equity (year-end).'],
        ['Posición frente al sector', 'Porcentaje de empresas de la misma actividad (clase, grupo, división o sección, según haya al menos 15 comparables) con un indicador peor.', 'Share of companies in the same activity (class, group, division or section, whichever has at least 15 peers) with a worse ratio.'],
        ['Fuente y cobertura', 'Estados financieros NIIF reportados a la Superintendencia de Sociedades (datos.gov.co). Se usa el reporte individual de cada año y, si hay reexpresiones, el más reciente. Cifras en miles de pesos convertidas a millones.', 'IFRS financial statements reported to the Superintendencia de Sociedades (datos.gov.co). The individual report of each year is used and, when restated, the latest one. Figures in thousands of pesos converted to millions.']
    ];
    function renderGlosario() { $('glosBody').innerHTML = GLOS.map(function (g) { return '<dt>' + g[0] + '</dt><dd>' + L(g[1], g[2]) + '</dd>'; }).join(''); }

    // ---------- control de año y pestañas ----------
    function renderYear() {
        Array.prototype.forEach.call(document.querySelectorAll('#empYears button'), function (b) { b.classList.toggle('active', parseInt(b.getAttribute('data-y'), 10) === Y); });
        renderKpis(); renderCharts(); renderInsights(); renderBench(); renderStatement();
        var u = new URL(location.href); u.searchParams.set('anio', Y); history.replaceState(null, '', u.pathname + u.search + u.hash);
    }
    $('empYears').addEventListener('click', function (e) { var b = e.target.closest('button'); if (!b) return; Y = parseInt(b.getAttribute('data-y'), 10); renderYear(); });
    $('stmtTabs').addEventListener('click', function (e) {
        var b = e.target.closest('button'); if (!b) return; STM = b.getAttribute('data-s');
        Array.prototype.forEach.call($('stmtTabs').querySelectorAll('button'), function (x) { x.classList.toggle('active', x === b); }); renderStatement();
    });
    $('stmtMode').addEventListener('click', function (e) {
        var b = e.target.closest('button'); if (!b) return; MODE = b.getAttribute('data-m');
        Array.prototype.forEach.call($('stmtMode').querySelectorAll('button'), function (x) { x.classList.toggle('active', x === b); }); renderStatement();
    });
    $('empTabs').addEventListener('click', function (e) { var b = e.target.closest('button[data-hash]'); if (b) history.replaceState(null, '', location.pathname + location.search + '#' + b.getAttribute('data-hash')); });
    $('empPrint').addEventListener('click', function () { window.print(); });
    O.share($('empShare'));

    O.addRecent({ nit: NIT, razon: root.getAttribute('data-razon') });
    O.api('ficha', 'nit=' + encodeURIComponent(NIT)).then(function (d) {
        D = d;
        var qy = parseInt(new URL(location.href).searchParams.get('anio'), 10);
        if (qy && d.serie.anios.indexOf(qy) >= 0 && d.rubros[qy]) Y = qy;
        renderPerfil(); renderGlosario(); renderYear();
        var h = location.hash.replace('#', '');
        var btn = h && document.querySelector('#empTabs [data-hash="' + h + '"]');
        if (btn) new bootstrap.Tab(btn).show();
        root.classList.add('obs-ready');
    }).catch(function () {
        $('empKpis').insertAdjacentHTML('afterend', '<div class="obs-empty"><i class="fas fa-exclamation-triangle"></i><p>' + L('No se pudo cargar el detalle. Intenta de nuevo.', 'Detail could not be loaded. Please try again.') + '</p></div>');
    });
})();
