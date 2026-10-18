/* Observatorio Bufins – análisis individual de una empresa */
(function () {
    'use strict';
    var root = document.getElementById('empApp');
    if (!root || typeof Chart === 'undefined' || !window.OBS) return;
    var O = OBS, L = O.L, $ = O.$, esc = O.esc;
    var NIT = root.getAttribute('data-nit');
    var D = null, Y = parseInt(root.getAttribute('data-anio'), 10), STM = 'pyg', MODE = 'v', VIEW = 'evo';

    // ---------- definición de indicadores (mismas claves que el servidor) ----------
    var RATIOS = [
        ['mbruto', 'Margen bruto', 'Gross margin', O.pct, 'Rentabilidad', 'Profitability'],
        ['moper', 'Margen operativo', 'Operating margin', O.pct, 'Rentabilidad', 'Profitability'],
        ['mebitda', 'Margen EBITDA', 'EBITDA margin', O.pct, 'Rentabilidad', 'Profitability'],
        ['mneto', 'Margen neto', 'Net margin', O.pct, 'Rentabilidad', 'Profitability'],
        ['roa', 'ROA', 'ROA', O.pct, 'Rentabilidad', 'Profitability'],
        ['roe', 'ROE', 'ROE', O.pct, 'Rentabilidad', 'Profitability'],
        ['rotacion', 'Rotación de activos (ingresos / activos)', 'Asset turnover (revenue / assets)', O.x, 'Rentabilidad', 'Profitability'],
        ['apalanc', 'Apalancamiento (activos / patrimonio)', 'Equity multiplier (assets / equity)', O.x, 'Endeudamiento', 'Leverage'],
        ['endeud', 'Endeudamiento (pasivo/activo)', 'Leverage (liabilities/assets)', O.pct, 'Endeudamiento', 'Leverage'],
        ['deudaebitda', 'Deuda / EBITDA', 'Debt / EBITDA', O.x, 'Endeudamiento', 'Leverage'],
        ['cobertura', 'Cobertura de intereses (EBITDA / costos fin.)', 'Interest coverage (EBITDA / finance costs)', O.x, 'Endeudamiento', 'Leverage'],
        ['liquidez', 'Liquidez corriente', 'Current ratio', O.x, 'Liquidez', 'Liquidity'],
        ['acida', 'Razón ácida', 'Quick ratio', O.x, 'Liquidez', 'Liquidity'],
        ['ktingresos', 'Capital de trabajo / ingresos', 'Working capital / revenue', O.pct, 'Eficiencia', 'Efficiency'],
        ['diascxc', 'Días de cartera', 'Receivable days', O.days, 'Eficiencia', 'Efficiency'],
        ['diasinv', 'Días de inventario', 'Inventory days', O.days, 'Eficiencia', 'Efficiency'],
        ['diascxp', 'Días de proveedores', 'Payable days', O.days, 'Eficiencia', 'Efficiency'],
        ['ciclo', 'Ciclo de conversión de efectivo (días)', 'Cash conversion cycle (days)', O.days, 'Eficiencia', 'Efficiency'],
        ['altman', "Altman Z'' (riesgo de quiebra)", "Altman Z'' (distress risk)", function (v) { return v == null ? '—' : O.num(v); }, 'Salud financiera', 'Financial health'],
        ['score', 'Bufins Score de salud (0–100)', 'Bufins health score (0–100)', function (v) { return v == null ? '—' : O.int(v); }, 'Salud financiera', 'Financial health']
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
    // ---------- Bufins Score, DuPont y cifras en términos reales / dólares ----------
    var NIVEL = { solida: ['Sólida', 'Strong'], estable: ['Estable', 'Stable'], vigilar: ['A vigilar', 'To watch'], critica: ['Crítica', 'Critical'] };
    var ZONA = { segura: ['zona segura', 'safe zone'], gris: ['zona gris', 'grey zone'], alerta: ['zona de alerta', 'distress zone'] };
    function renderScore() {
        var s = D.salud && D.salud[Y], box = $('empScore');
        if (!s) { box.innerHTML = ''; return; }
        var prev = D.salud[Y - 1];
        var dl = prev ? s.total - prev.total : null;
        box.innerHTML = '<div class="obs-score obs-score-' + s.nivel + '">' +
            '<div class="obs-score-main"><strong>' + O.int(s.total) + '</strong><span>/ 100</span>' +
            '<em>' + L(NIVEL[s.nivel][0], NIVEL[s.nivel][1]) + '</em>' +
            (dl != null && Math.abs(dl) >= 1 ? '<small class="' + (dl >= 0 ? 'up' : 'down') + '">' + (dl >= 0 ? '▲ ' : '▼ ') + O.int(Math.abs(dl)) + ' pts vs. ' + (Y - 1) + '</small>' : '') + '</div>' +
            '<div class="obs-score-bars"><h3>' + L('Bufins Score de salud financiera', 'Bufins financial health score') + '</h3>' +
            s.pilares.map(function (p) {
                return '<div class="obs-score-row"><span>' + L(p.es, p.en) + '</span><i><b style="width:' + Math.round(p.puntos / p.max * 100) + '%"></b></i><small>' + O.int(p.puntos) + '/' + p.max + '</small></div>';
            }).join('') +
            '<p class="obs-hint">' + (s.altman != null ? "Altman Z'': <strong>" + O.num(s.altman) + '</strong> (' + L(ZONA[s.zona][0], ZONA[s.zona][1]) + '). ' : '') +
            '<a href="#glosario" data-gloss="1">' + L('Cómo se calcula', 'How it is computed') + '</a></p></div></div>';
    }
    function renderDupont() {
        var s = D.serie, i = ix(Y), p = i > 0 ? i - 1 : -1, box = $('empDupont');
        if (s.mneto[i] == null || s.rotacion[i] == null || s.apalanc[i] == null) { box.innerHTML = '<p class="obs-hint">' + L('No se puede descomponer el ROE (patrimonio no positivo o sin ingresos).', 'ROE cannot be decomposed (non-positive equity or no revenue).') + '</p>'; return; }
        function chg(k, f) { return p >= 0 && s[k][p] != null ? '<small>' + L('antes ', 'before ') + f(s[k][p]) + '</small>' : ''; }
        function cell(t, v, f, k, op) { return '<div class="obs-dp"><span>' + t + '</span><strong>' + f(v) + '</strong>' + chg(k, f) + '</div>' + (op ? '<b class="obs-dp-op">' + op + '</b>' : ''); }
        box.innerHTML = cell(L('Margen neto', 'Net margin'), s.mneto[i], O.pct, 'mneto', '×') + cell(L('Rotación de activos', 'Asset turnover'), s.rotacion[i], O.x, 'rotacion', '×') +
            cell(L('Apalancamiento', 'Equity multiplier'), s.apalanc[i], O.x, 'apalanc', '=') + cell('ROE', s.roe[i], O.pct, 'roe', '');
    }
    function renderMacro() {
        var box = $('empMacro'), m = D.macro || {}, s = D.serie, i = ix(Y), out = [];
        var trm = m.trm && m.trm[Y], ipc = m.ipc && m.ipc[Y], cr = s.crec[i];
        if (cr != null && ipc != null) { var real = ((1 + cr / 100) / (1 + ipc / 100) - 1) * 100; out.push('<div><span>' + L('Crecimiento real de ingresos', 'Real revenue growth') + '</span><strong class="' + (real >= 0 ? 'up' : 'down') + '">' + (real >= 0 ? '+' : '') + O.pct(real) + '</strong><small>' + L('nominal ', 'nominal ') + O.pct(cr) + ' · ' + L('inflación DANE ', 'CPI ') + O.pct(ipc) + '</small></div>'); }
        if (trm && s.ing[i] != null) {
            var usd = function (v) { return 'US$ ' + O.money(v / trm.prom, true); };
            out.push('<div><span>' + L('Ingresos en dólares', 'Revenue in US dollars') + '</span><strong>' + usd(s.ing[i]) + '</strong><small>' + L('TRM promedio ' + Y + ': $ ', 'Average FX rate ' + Y + ': COP ') + O.int(trm.prom) + '</small></div>');
            out.push('<div><span>' + L('EBITDA en dólares', 'EBITDA in US dollars') + '</span><strong>' + (s.ebitda[i] < 0 ? '−' : '') + usd(Math.abs(s.ebitda[i])) + '</strong><small>' + L('al cambio promedio del año', "at the year's average rate") + '</small></div>');
        }
        box.innerHTML = out.join('');
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
        $('pygView').hidden = STM !== 'pyg';
        renderStmtChart();
    }
    function renderWaterfall() {
        var s = D.serie, max = Math.max.apply(null, s.ing.concat(s.act).map(function (v) { return Math.abs(v || 0); }).concat([1])), u = O.unitFor(max);
        var ing = g(Y, 'ing'), ub = g(Y, 'ub'), uop = g(Y, 'uop'), uai = g(Y, 'uai'), un = g(Y, 'un');
        var steps = [
            { l: L('Ingresos', 'Revenue'), v: ing, t: 1 }, { l: L('Costo de ventas', 'Cost of sales'), v: ub - ing },
            { l: L('Gastos operacionales y otros', 'Operating expenses & other'), v: uop - ub }, { l: L('Resultado financiero y otros', 'Financial result & other'), v: uai - uop },
            { l: L('Impuestos', 'Income tax'), v: un - uai }, { l: L('Utilidad neta', 'Net profit'), v: un, t: 1 }
        ], run = 0, data = [], colors = [];
        steps.forEach(function (st) {
            if (st.t) { data.push([Math.min(0, st.v) / u.d, Math.max(0, st.v) / u.d]); colors.push('#583AFF'); run = st.v; }
            else { var a = run, b = run + st.v; data.push([Math.min(a, b) / u.d, Math.max(a, b) / u.d]); run = b; colors.push(st.v >= 0 ? '#01B3AB' : '#FF5500'); }
        });
        $('stmtChartTitle').textContent = L('De los ingresos a la utilidad neta', 'From revenue to net profit') + ' ' + Y + ' (' + u.l + ')';
        O.chart('chStmt', { type: 'bar', data: { labels: steps.map(function (x) { return x.l; }), datasets: [{ data: data, backgroundColor: colors, borderRadius: 4 }] },
            options: Object.assign({}, O.COMMON, { plugins: { legend: { display: false }, tooltip: { callbacks: { label: function (c) { var st = steps[c.dataIndex]; return (st.v < 0 ? '−' : st.t ? '' : '+') + O.num(Math.abs(st.v) / u.d) + ' ' + u.l; } } } }, scales: { y: { beginAtZero: true } } }) });
    }
    function renderStmtChart() {
        if (STM === 'pyg' && VIEW === 'cas') { renderWaterfall(); return; }
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
            cell(L('Marco contable', 'Accounting framework'), p.grupo) + cell(L('La sociedad es', 'The company is'), p.clase) + cell(L('Tamaño por ingresos', 'Size by revenue'), D.tamano + ' (S < 20.000 M · M < 100.000 M · L < 1 B)') +
            cell(L('Revisor fiscal', 'Statutory auditor'), p.rf) + cell(L('Concepto del revisor fiscal', 'Auditor opinion'), p.rfop) + '</div>' +
            renderContratos() +
            '<div class="obs-info obs-info-wide"><span>' + L('Objeto social principal', 'Main corporate purpose') + '</span><p>' + esc(p.objeto || '—') + '</p></div>';
    }
    function renderContratos() {
        var c = D.contratos; if (!c) return '';
        var desde = (c.desde || '').slice(0, 4);
        return '<div class="obs-info obs-info-wide"><span>' + L('Contratación con el Estado (SECOP II)', 'Public contracting (SECOP II)') + '</span><p><strong>' + O.int(c.n) + '</strong> ' + L('contratos desde ' + desde, 'contracts since ' + desde) +
            (c.valor > 0 ? ' · ' + L('valor reportado ', 'reported value ') + O.money(c.valor / 1e6) : '') +
            '. <span class="obs-hint">' + L('Cifras declaradas por las entidades en datos.gov.co; pueden incluir adiciones o errores de digitación.', 'Figures declared by public entities on datos.gov.co; may include additions or typing errors.') + '</span></p></div>';
    }
    // ---------- historia: hitos calculados con datos reales (constitución, estados financieros 2020+, SECOP) ----------
    var HTIPO = {
        fund: ['fa-rocket', 'blue'], data: ['fa-file-invoice-dollar', 'gray'], best: ['fa-trophy', 'purple'], up: ['fa-chart-line', 'green'],
        down: ['fa-arrow-down', 'red'], loss: ['fa-exclamation-triangle', 'red'], back: ['fa-undo', 'green'], debt: ['fa-hand-holding-usd', 'amber'],
        equity: ['fa-balance-scale', 'blue'], state: ['fa-handshake', 'purple'], last: ['fa-flag-checkered', 'gray']
    };
    function signed(v) { return (v > 0 ? '+' : '') + O.pct(v); }
    function mayorCambio(arr, minAbs, signo) {
        var best = null;
        for (var i = 1; i < arr.length; i++) {
            if (arr[i] == null || arr[i - 1] == null || arr[i - 1] <= 0) continue;
            var c = (arr[i] - arr[i - 1]) / arr[i - 1] * 100;
            if (signo && (c > 0) !== (signo > 0)) continue;
            if (Math.abs(c) >= minAbs && (!best || Math.abs(c) > Math.abs(best.c))) best = { i: i, c: c };
        }
        return best;
    }
    function renderHistoria() {
        var s = D.serie, a = s.anios, p = D.perfil, now = new Date().getFullYear(), H = [], first = -1, last = -1, i;
        for (i = 0; i < a.length; i++) if (s.ing[i] != null) { if (first < 0) first = i; last = i; }
        function add(y, o, t, tag, title, desc) { H.push({ y: y, o: o, t: t, tag: tag, title: title, desc: desc }); }
        function argmax(arr) { var b = -1; for (var k = 0; k < arr.length; k++) if (arr[k] != null && (b < 0 || arr[k] > arr[b])) b = k; return b; }
        function conDatos(arr) { return arr.filter(function (v) { return v != null; }).length; }

        var fy = 0, fecha = '', m = /^(\d{4})-(\d{2})-(\d{2})/.exec(p.fconst || '');
        if (m) {
            fy = parseInt(m[1], 10);
            if (fy >= 1800 && fy <= now) fecha = new Date(fy, parseInt(m[2], 10) - 1, parseInt(m[3], 10)).toLocaleDateString(L('es-CO', 'en-US'), { day: 'numeric', month: 'long', year: 'numeric' });
            else fy = 0;
        }
        if (fy) add(fy, 0, 'fund', L('Constitución', 'Incorporation'), L('Fundación de la empresa', 'Company founded'),
            L('Constituida el ' + fecha + (p.tipo ? '; hoy es ' + p.tipo + '.' : '.'), 'Incorporated on ' + fecha + (p.tipo ? '; today it is a ' + p.tipo + '.' : '.')));

        if (first >= 0) {
            add(a[first], 1, 'data', L('Estados financieros', 'Financial statements'), L('Primer año con estados financieros', 'First year with financial statements'),
                L('Ingresos de ' + O.money(s.ing[first]) + ' reportados a la Supersociedades.', 'Revenue of ' + O.money(s.ing[first]) + ' reported to the Superintendencia de Sociedades.'));
            var bi = argmax(s.ing);
            if (conDatos(s.ing) > 1 && bi >= 0 && s.ing[bi] > 0) add(a[bi], 2, 'best', L('Ingresos', 'Revenue'), L('Mejor año de ingresos', 'Best revenue year'),
                L('Ingresos de ' + O.money(s.ing[bi]) + ', el nivel más alto del periodo.', 'Revenue of ' + O.money(s.ing[bi]) + ', the highest of the period.'));
            var be = argmax(s.ebitda);
            if (conDatos(s.ebitda) > 1 && be >= 0 && s.ebitda[be] > 0) {
                var mg = s.ing[be] > 0 ? ' (' + O.pct(s.ebitda[be] / s.ing[be] * 100) + L(' de los ingresos', ' of revenue') + ')' : '';
                add(a[be], 3, 'best', 'EBITDA', L('Mejor año de EBITDA', 'Best EBITDA year'), L('EBITDA de ' + O.money(s.ebitda[be]) + mg + '.', 'EBITDA of ' + O.money(s.ebitda[be]) + mg + '.'));
            }
            var cu = mayorCambio(s.ing, 10, 1), cd = mayorCambio(s.ing, 10, -1);
            if (cu) add(a[cu.i], 4, 'up', L('Ingresos', 'Revenue'), L('Mayor crecimiento de ingresos', 'Strongest revenue growth'),
                L('Los ingresos cambiaron ' + signed(cu.c) + ' frente a ' + a[cu.i - 1] + '.', 'Revenue changed ' + signed(cu.c) + ' versus ' + a[cu.i - 1] + '.'));
            if (cd) add(a[cd.i], 4, 'down', L('Ingresos', 'Revenue'), L('Mayor caída de ingresos', 'Largest revenue drop'),
                L('Los ingresos cambiaron ' + signed(cd.c) + ' frente a ' + a[cd.i - 1] + '.', 'Revenue changed ' + signed(cd.c) + ' versus ' + a[cd.i - 1] + '.'));
            var perd = -1;
            for (i = 0; i < a.length; i++) if (s.un[i] != null && s.un[i] < 0) { perd = i; break; }
            if (perd >= 0) {
                add(a[perd], 5, 'loss', L('Resultados', 'Results'), L('Primera pérdida neta del periodo', 'First net loss of the period'),
                    L('Pérdida neta de ' + O.money(Math.abs(s.un[perd])) + '.', 'Net loss of ' + O.money(Math.abs(s.un[perd])) + '.'));
                for (i = perd + 1; i < a.length; i++) if (s.un[i] != null && s.un[i] >= 0) {
                    add(a[i], 6, 'back', L('Resultados', 'Results'), L('Regreso a utilidad', 'Back to profit'),
                        L('Utilidad neta de ' + O.money(s.un[i]) + ' tras la pérdida de ' + a[perd] + '.', 'Net profit of ' + O.money(s.un[i]) + ' after the ' + a[perd] + ' loss.'));
                    break;
                }
            }
            var neg = -1;
            for (i = 0; i < a.length; i++) if (s.pat[i] != null && s.pat[i] < 0) { neg = i; break; }
            if (neg >= 0) add(a[neg], 7, 'equity', L('Patrimonio', 'Equity'), L('Primer año con patrimonio negativo', 'First year with negative equity'),
                L('El patrimonio fue de ' + O.money(s.pat[neg]) + ': los pasivos superaron a los activos.', 'Equity was ' + O.money(s.pat[neg]) + ': liabilities exceeded assets.'));
            var pc = mayorCambio(s.pat, 50);
            if (pc && !(pc.c < 0 && pc.i === neg)) add(a[pc.i], 8, 'equity', L('Patrimonio', 'Equity'), pc.c > 0 ? L('Salto del patrimonio', 'Equity jump') : L('Caída del patrimonio', 'Equity drop'),
                L('El patrimonio cambió ' + signed(pc.c) + ' frente a ' + a[pc.i - 1] + '.', 'Equity changed ' + signed(pc.c) + ' versus ' + a[pc.i - 1] + '.'));
            var dc2 = mayorCambio(s.deuda, 50);
            if (dc2) add(a[dc2.i], 9, 'debt', L('Deuda', 'Debt'), dc2.c > 0 ? L('Aumento de la deuda financiera', 'Financial debt increase') : L('Reducción de la deuda financiera', 'Financial debt reduction'),
                L('La deuda financiera cambió ' + signed(dc2.c) + ' frente a ' + a[dc2.i - 1] + '.', 'Financial debt changed ' + signed(dc2.c) + ' versus ' + a[dc2.i - 1] + '.'));
        }
        var c = D.contratos;
        if (c && c.n > 0) {
            var cy = parseInt((c.desde || '').slice(0, 4), 10) || (last >= 0 ? a[last] : now);
            add(cy, 10, 'state', 'SECOP II', L('Contratación con el Estado', 'Public contracting'),
                L(O.int(c.n) + ' contratos firmados desde ' + cy + (c.valor > 0 ? ', por ' + O.money(c.valor / 1e6) : '') + '.', O.int(c.n) + ' contracts signed since ' + cy + (c.valor > 0 ? ', worth ' + O.money(c.valor / 1e6) : '') + '.'));
        }
        if (last > first) add(a[last], 11, 'last', L('Último reporte', 'Latest report'), L('Último reporte a la Supersociedades', 'Latest report to the Superintendencia'),
            L('Corte ' + a[last] + ': ingresos de ' + O.money(s.ing[last]) + (s.un[last] != null ? ' y utilidad neta de ' + O.money(s.un[last]) : '') + '.', 'Year-end ' + a[last] + ': revenue of ' + O.money(s.ing[last]) + (s.un[last] != null ? ' and net profit of ' + O.money(s.un[last]) : '') + '.'));
        H.sort(function (x, y) { return x.y - y.y || x.o - y.o; });

        function stat(cl, v, k) { return '<div class="obs-hstat obs-hc-' + cl + '"><strong>' + v + '</strong><span>' + k + '</span></div>'; }
        var stats = stat('blue', fy || '—', L('Año de fundación', 'Founded')) + stat('green', fy ? Math.max(0, now - fy) : '—', L('Años de operación', 'Years in operation')) +
            stat('purple', H.length, L('Hitos registrados', 'Milestones')) + stat('amber', last >= 0 ? a[last] : '—', L('Último año reportado', 'Latest year reported'));
        var line = H.map(function (h) {
            var t = HTIPO[h.t];
            return '<li class="obs-hl obs-hc-' + t[1] + '"><span class="obs-hl-ic"><i class="fas ' + t[0] + '"></i></span><div><div class="obs-hl-top"><b>' + h.y + '</b><span class="obs-hl-tag">' + esc(h.tag) +
                '</span></div><h4>' + esc(h.title) + '</h4><p>' + esc(h.desc) + '</p></div></li>';
        }).join('');
        $('histBody').innerHTML = '<div class="obs-hist"><div class="obs-hist-stats">' + stats + '</div>' +
            (H.length ? '<ol class="obs-hist-line">' + line + '</ol>' : '<div class="obs-empty"><p>' + L('Aún no hay hitos para esta empresa.', 'No milestones for this company yet.') + '</p></div>') + '</div>' +
            '<p class="obs-hist-note"><i class="fas fa-info-circle me-2"></i>' + L('Los hitos se calculan con la fecha de constitución y los estados financieros que la empresa reporta a la Supersociedades (desde 2020), más la contratación pública declarada en SECOP II. No incluyen eventos del registro mercantil como cambios de directiva o reformas estatutarias.',
                'Milestones are computed from the incorporation date and the financial statements the company reports to the Superintendencia de Sociedades (since 2020), plus public contracting declared on SECOP II. They do not include commercial-registry events such as board changes or bylaw amendments.') + '</p>';
    }

    var GLOS = [
        ['Bufins Score de salud (0–100)', 'Suma de cuatro pilares de 25 puntos: rentabilidad (margen EBITDA y ROA), liquidez (liquidez corriente y capital de trabajo/ingresos), endeudamiento (deuda/EBITDA y pasivo/activo) y solvencia (cobertura de intereses y Altman). Cada indicador se puntúa en forma lineal entre un piso y un techo. 70 o más: sólida; 50 a 69: estable; 30 a 49: a vigilar; menos de 30: crítica.', 'Sum of four 25-point pillars: profitability (EBITDA margin and ROA), liquidity (current ratio and working capital/revenue), leverage (debt/EBITDA and liabilities/assets) and solvency (interest coverage and Altman). Each ratio is scored linearly between a floor and a ceiling. 70+: strong; 50–69: stable; 30–49: to watch; below 30: critical.'],
        ["Altman Z'' (riesgo de quiebra)", "Z'' = 3,25 + 6,56·(capital de trabajo/activos) + 3,26·(utilidades retenidas/activos) + 6,72·(utilidad operativa/activos) + 1,05·(patrimonio/pasivos). Versión para mercados emergentes. Mayor a 5,85: zona segura; entre 4,15 y 5,85: zona gris; menor a 4,15: zona de alerta. Es una señal estadística, no una predicción.", "Z'' = 3.25 + 6.56·(working capital/assets) + 3.26·(retained earnings/assets) + 6.72·(operating profit/assets) + 1.05·(equity/liabilities). Emerging-markets version. Above 5.85: safe zone; 4.15 to 5.85: grey zone; below 4.15: distress zone. A statistical signal, not a prediction."],
        ['DuPont', 'Descompone el ROE: margen neto × rotación de activos (ingresos/activos) × apalancamiento (activos/patrimonio). Muestra si la rentabilidad viene del margen, de la eficiencia o de la deuda.', 'Breaks ROE into net margin × asset turnover (revenue/assets) × equity multiplier (assets/equity). Shows whether returns come from margin, efficiency or debt.'],
        ['Crecimiento real y dólares', 'Crecimiento real = (1 + crecimiento nominal) / (1 + inflación anual DANE) − 1. Dólares = cifra en pesos / TRM promedio del año (datos.gov.co). La inflación es una tabla que Bufins actualiza una vez al año.', 'Real growth = (1 + nominal growth) / (1 + DANE annual CPI) − 1. US dollars = COP figure / average FX rate of the year (datos.gov.co). CPI is a table Bufins updates once a year.'],
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
        renderKpis(); renderScore(); renderMacro(); renderCharts(); renderInsights(); renderBench(); renderDupont(); renderStatement(); renderHistoria();
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
    $('pygView').addEventListener('click', function (e) {
        var b = e.target.closest('button'); if (!b) return; VIEW = b.getAttribute('data-w');
        Array.prototype.forEach.call($('pygView').querySelectorAll('button'), function (x) { x.classList.toggle('active', x === b); }); renderStmtChart();
    });
    root.addEventListener('click', function (e) {
        var a = e.target.closest('a[data-gloss]'); if (!a) return; e.preventDefault();
        var btn = document.querySelector('#empTabs [data-hash="glosario"]'); if (btn) new bootstrap.Tab(btn).show();
    });
    O.share($('empShare'));

    document.addEventListener('obs:unit', function () { if (D) renderYear(); });
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
