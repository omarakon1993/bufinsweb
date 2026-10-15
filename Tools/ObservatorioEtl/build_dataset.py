#!/usr/bin/env python3
"""
ETL del Observatorio Bufins: estados financieros NIIF de las sociedades que reportan a la Supersociedades.

Fuente (API abierta Socrata, sin llave): https://www.datos.gov.co  (publicador: Superintendencia de Sociedades)
    Carátula ............ 6hqw-m3dm   (razón social, CIIU, constitución, estado, objeto social, unidad de cifras)
    Situación financiera  pfdp-zks5   (balance)
    Resultado integral .. prwj-nzxa   (estado de resultados)
    Flujo de efectivo ... ctcp-462n

Genera en App_Data/observatorio/:
    empresas.json   índice en memoria: perfil + 22 métricas por empresa y año (miles de COP)
    detalle.bin     bloques gzip por empresa con todos los rubros mapeados (se leen bajo demanda)
    detalle.idx.json  NIT -> [offset, longitud] dentro de detalle.bin

Uso (solo librería estándar de Python 3.8+):
    python Tools/ObservatorioEtl/build_dataset.py                  # 2020..último año disponible
    python Tools/ObservatorioEtl/build_dataset.py --years 2024 2025 --force
Variable opcional SOCRATA_APP_TOKEN para evitar límites de uso.
"""
import argparse
import csv
import gzip
import io
import json
import os
import random
import re
import sys
import time
import unicodedata
import urllib.error
import urllib.parse
import urllib.request
from concurrent.futures import ThreadPoolExecutor, as_completed
from datetime import datetime, timezone

BASE = "https://www.datos.gov.co"
DS = {"car": "6hqw-m3dm", "bal": "pfdp-zks5", "eri": "prwj-nzxa", "flu": "ctcp-462n"}
PAGE = 50000
OUT_DIR = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "App_Data", "observatorio")
TOKEN = os.environ.get("SOCRATA_APP_TOKEN")


def norm(s):
    s = unicodedata.normalize("NFD", s or "")
    s = "".join(c for c in s if unicodedata.category(c) != "Mn").lower()
    return re.sub(r"\s+", " ", s).strip()


# ---- concepto normalizado (sin tildes, minúsculas) -> clave interna -------------------------------------------
RULES = {
    "eri": {
        "ingresos de actividades ordinarias": "ing",
        "costo de ventas": "costo",
        "ganancia bruta": "ub",
        "gastos de administracion": "gadm",
        "gastos de ventas": "gven",
        "costos de distribucion": "gdis",
        "otros ingresos": "oing",
        "otros gastos": "ogas",
        "otras ganancias (perdidas)": "ogan",
        "ganancia (perdida) por actividades de operacion": "uop",
        "ingresos financieros": "ifin",
        "costos financieros": "cfin",
        "ganancia (perdida), antes de impuestos": "uai",
        "ingreso (gasto) por impuestos": "imp",
        "ganancia (perdida)": "un",
    },
    "bal": {
        "efectivo y equivalentes al efectivo": "caja",
        "cuentas comerciales por cobrar y otras cuentas por cobrar corrientes": "cxc",
        "inventarios corrientes": "inv",
        "activos por impuestos corrientes, corriente": "aimp",
        "otros activos no financieros corrientes": "oanf",
        "activos corrientes totales": "ac",
        "propiedades, planta y equipo": "ppye",
        "activos intangibles distintos de la plusvalia": "intang",
        "plusvalia": "plus",
        "total de activos no corrientes": "anc",
        "total de activos": "act",
        "cuentas por pagar comerciales y otras cuentas por pagar corrientes": "cxp",
        "pasivos por impuestos corrientes, corriente": "pimp",
        "provisiones corrientes por beneficios a los empleados": "pbe",
        "otras provisiones corrientes": "oprov",
        "otros pasivos no financieros corrientes": "opnf",
        "otros pasivos financieros corrientes": "opfc",
        "prestamos corrientes": "pcor",
        "parte corriente de prestamos no corrientes": "pcpnc",
        "otros pasivos financieros no corrientes": "opfnc",
        "parte no corriente de prestamos no corrientes": "pnc",
        "pasivos corrientes totales": "pc",
        "total de pasivos no corrientes": "pnct",
        "total pasivos": "pas",
        "patrimonio total": "pat",
        "capital emitido": "capital",
        "ganancias acumuladas": "gacum",
    },
    "flu": {
        "( + ) ajustes por gastos de depreciacion y amortizacion": "da",
        "flujos de efectivo netos procedentes de (utilizados en) actividades de operacion": "fco",
        "flujos de efectivo netos procedentes de (utilizados en) actividades de inversion": "fci",
        "flujos de efectivo netos procedentes de (utilizados en) actividades de financiacion": "fcf",
        "(-) compras de propiedades, planta y equipo": "capex",
        "efectivo y equivalentes al efectivo al final del periodo": "cajaf",
        "efectivo y equivalentes al efectivo al principio del periodo": "cajai",
        "incremento (disminucion) neto de efectivo y equivalentes al efectivo": "dcaja",
        "(-) intereses pagados": "intpag",
        "(-) dividendos pagados": "divpag",
        "(+) importes procedentes de prestamos": "prest_in",
        "(-) reembolsos de prestamos": "prest_out",
    },
    "car": {
        "razon social de la sociedad": "razon",
        "clasificacion industrial internacional uniforme version 4 a.c (ciiu)": "ciiu",
        "fecha de constitucion (aaaa-mm-dd)": "fconst",
        "objeto social principal": "objeto",
        "estado actual": "estado",
        "ciudad de la direccion del domicilio": "ciudad",
        "departamento de la direccion del domicilio": "depto",
        "tipo societario": "tipo",
        "la sociedad es": "clase",
        "unidad de diligenciamiento utilizada en este reporte": "unidad",
        "concepto del revisor fiscal en su informe": "rfop",
        "la compania esta obligada a tener revisor fiscal?": "rf",
    },
}

# Métricas del índice en memoria (miles de COP), en este orden
METRICAS = ["ing", "ub", "gop", "uop", "ebitda", "un", "act", "pas", "pat", "ktn", "deuda", "cxc", "inv", "cxp", "fco",
            "caja", "costo", "da", "cfin", "ac", "pc", "ppye"]


# ---- reparación de texto: la fuente trae U+FFFD donde iba una letra con tilde o eñe ----------------------------
PALABRA = re.compile(r"[A-Za-zÁÉÍÓÚÜÑáéíóúüñ�]+")
MAYUS = "ÁÉÍÓÚÑÜ"
MINUS = "áéíóúñü"


SEMILLA = ("anónima anónimo reestructuración ejecución trámite liquidación obligatoria judicial económica extranjera extranjeras régimen "
           "inspección vigilancia constitución compañía compañías operación operaciones información administración construcción "
           "distribución comercialización producción fabricación transformación explotación prestación educación comunicación "
           "organización organizaciones tecnología tecnologías asesoría consultoría ingeniería inversión inversiones importación "
           "exportación participación sociedad sociedades corporación fundación cooperativa asociación mercancía mercancías "
           "artículos máquinas maquinaria vehículos energía alimentación alimenticios químicos plásticos ópticos eléctricos "
           "electrónicos metálicos métodos servicios jurídicos públicos públicas básicos").split()


def construir_vocab(textos):
    from collections import Counter
    c = Counter({w: 1 for w in SEMILLA})
    for t in textos:
        for tok in PALABRA.findall(t or ""):
            if "�" not in tok and any(ord(ch) > 127 for ch in tok):
                c[tok.lower()] += 1
    return c


def reparar_token(tok, vocab):
    pos = [i for i, ch in enumerate(tok) if ch == "�"]
    if not pos:
        return tok
    letras = [ch for ch in tok if ch != "�"]
    mayus = bool(letras) and all(ch.isupper() for ch in letras)
    juego = MAYUS if mayus else MINUS
    if len(pos) <= 3:
        import itertools
        mejor, mejor_n = None, 0
        for combo in itertools.product(juego, repeat=len(pos)):
            cand = list(tok)
            for i, ch in zip(pos, combo):
                cand[i] = ch
            cand = "".join(cand)
            n = vocab.get(cand.lower(), 0)
            if n > mejor_n:
                mejor, mejor_n = cand, n
        if mejor:
            return mejor
    # reglas de respaldo
    out = []
    for i, ch in enumerate(tok):
        if ch != "�":
            out.append(ch)
            continue
        prev = tok[i - 1].lower() if i > 0 else ""
        nxt = tok[i + 1].lower() if i + 1 < len(tok) else ""
        if nxt == "n" and i + 2 == len(tok):
            r = "o"
        elif prev in "aeiou" and nxt in "aeiou" and nxt:
            r = "n"
        else:
            r = "i"
        out.append((r.upper() if mayus else r).replace("O", "Ó").replace("o", "ó").replace("N", "Ñ").replace("n", "ñ").replace("I", "Í").replace("i", "í"))
    return "".join(out)


def reparar(texto, vocab):
    if not texto or "�" not in texto:
        return texto
    return PALABRA.sub(lambda m: reparar_token(m.group(0), vocab), texto).replace("�", "")


def http_get(url, retries=6):
    headers = {"User-Agent": "BufinsObservatorioETL/1.0", "Accept-Encoding": "gzip"}
    if TOKEN:
        headers["X-App-Token"] = TOKEN
    for intento in range(retries):
        try:
            req = urllib.request.Request(url, headers=headers)
            with urllib.request.urlopen(req, timeout=180) as r:
                data = r.read()
                if r.headers.get("Content-Encoding") == "gzip":
                    data = gzip.decompress(data)
                return data
        except (urllib.error.URLError, urllib.error.HTTPError, TimeoutError, ConnectionError) as e:
            espera = min(60, 2 ** intento + random.random() * 2)
            print("  reintento %d (%s) en %.0fs" % (intento + 1, getattr(e, "code", e), espera), file=sys.stderr)
            time.sleep(espera)
    raise RuntimeError("No se pudo obtener " + url[:200])


def soql(ds, params, fmt="json"):
    return http_get("%s/resource/%s.%s?%s" % (BASE, DS[ds], fmt, urllib.parse.urlencode(params, quote_via=urllib.parse.quote)))


def rango_anio(y):
    return "fecha_corte between '%d-01-01T00:00:00' and '%d-12-31T23:59:59' AND periodo='Periodo Actual'" % (y, y)


def coincide(regla, concepto):
    """La fuente trae U+FFFD donde iba una letra con tilde/eñe: se acepta como comodín de 1 carácter."""
    src = norm(concepto)
    return len(regla) == len(src) and all(a == b or b == "�" for a, b in zip(regla, src))


def clave_de(ds, concepto):
    n = norm(concepto)
    k = RULES[ds].get(n)
    if k:
        return k
    if "�" in n:
        for regla, k in RULES[ds].items():
            if coincide(regla, concepto):
                return k
    return None


def listar_conceptos(ds, y):
    """Conceptos exactos (como están en la fuente) que nos interesan para ese dataset y año."""
    data = soql(ds, {"$select": "concepto,count(*) as n", "$where": rango_anio(y), "$group": "concepto", "$limit": 5000})
    exact = {}
    for r in json.loads(data):
        k = clave_de(ds, r["concepto"])
        if k:
            exact[r["concepto"]] = k
    return exact


CACHE = os.path.join(os.path.dirname(os.path.abspath(__file__)), ".cache")


def pagina_csv(ds, y, params, offset):
    """Página CSV con caché en disco (Tools/ObservatorioEtl/.cache) para poder reintentar el proceso sin volver a descargar."""
    os.makedirs(CACHE, exist_ok=True)
    ruta = os.path.join(CACHE, "%s_%d_%d.csv" % (ds, y, offset))
    if os.path.exists(ruta):
        return open(ruta, "rb").read()
    data = soql(ds, params, "csv")
    tmp = ruta + ".tmp"
    open(tmp, "wb").write(data)
    os.replace(tmp, ruta)
    return data


def descargar(ds, y, exact):
    """Descarga paginada (CSV) de las filas de los conceptos dados. Devuelve lista de tuplas."""
    lista = ",".join("'%s'" % c.replace("'", "''") for c in exact)
    cols = "nit,codigo_instancia,punto_entrada,concepto,valor" if ds == "car" else "nit,codigo_instancia,concepto,valor"
    filas, offset = [], 0
    while True:
        data = pagina_csv(ds, y, {"$select": cols, "$where": rango_anio(y) + " AND concepto in (%s)" % lista,
                                  "$order": ":id", "$limit": PAGE, "$offset": offset}, offset)
        rows = list(csv.reader(io.StringIO(data.decode("utf-8"))))
        n = len(rows) - 1
        filas.extend(rows[1:])
        print("  %s %d: +%d (total %d)" % (ds, y, n, len(filas)), flush=True)
        if n < PAGE:
            return filas
        offset += PAGE


def num(s):
    try:
        return float(str(s).replace(",", "."))
    except ValueError:
        return None


def factor_miles(unidad):
    u = norm(unidad)
    if u.startswith("millones"):
        return 1000.0
    if u.startswith("miles"):
        return 1.0
    if u.startswith("pesos"):
        return 0.001
    return 1.0


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--years", nargs="*", type=int)
    ap.add_argument("--force", action="store_true")
    ap.add_argument("--fresh", action="store_true", help="ignorar la caché de descargas")
    ap.add_argument("--workers", type=int, default=5)
    a = ap.parse_args()

    if a.fresh and os.path.isdir(CACHE):
        for f in os.listdir(CACHE):
            os.remove(os.path.join(CACHE, f))

    # Años: 2020..último año con datos
    if a.years:
        years = sorted(a.years)
    else:
        top = json.loads(soql("car", {"$select": "max(fecha_corte) as m"}))[0]["m"]
        years = list(range(2020, int(top[:4]) + 1))
    print("Años:", years)

    # Fecha de actualización de la fuente (la más reciente de los 4 datasets)
    act = 0
    for ds in DS:
        v = json.loads(http_get("%s/api/views/%s.json" % (BASE, DS[ds])))
        act = max(act, int(v.get("rowsUpdatedAt", 0)))
    fuente_act = datetime.fromtimestamp(act, tz=timezone.utc).strftime("%Y-%m-%d")
    prev_path = os.path.join(OUT_DIR, "empresas.json")
    if os.path.exists(prev_path) and not a.force:
        try:
            prev = json.load(open(prev_path, encoding="utf-8"))["meta"]
            if prev.get("fuenteActualizado") == fuente_act and prev.get("anios") == years:
                print("Sin cambios en la fuente (%s). Use --force para regenerar." % fuente_act)
                return
        except Exception:
            pass

    # 1) Conceptos exactos por dataset (unión de todos los años)
    print("Listando conceptos…")
    exact = {ds: {} for ds in DS}
    with ThreadPoolExecutor(max_workers=a.workers) as ex:
        futs = {ex.submit(listar_conceptos, ds, y): (ds, y) for ds in DS for y in years}
        for f in as_completed(futs):
            ds, _ = futs[f]
            exact[ds].update(f.result())
    for ds in DS:
        faltan = set(RULES[ds].values()) - set(exact[ds].values())
        print("  %s: %d conceptos mapeados; sin datos: %s" % (ds, len(exact[ds]), sorted(faltan) or "-"))

    # 2) Descarga en paralelo
    print("Descargando…")
    crudo = {}
    with ThreadPoolExecutor(max_workers=a.workers) as ex:
        futs = {ex.submit(descargar, ds, y, exact[ds]): (ds, y) for ds in DS for y in years}
        for f in as_completed(futs):
            crudo[futs[f]] = f.result()

    # 3) Carátula -> instancia elegida por (nit, año)
    print("Procesando…")
    cars = {}  # instancia -> dict
    for y in years:
        for nit, inst, pe, concepto, valor in crudo[("car", y)]:
            d = cars.setdefault(inst, {"nit": nit, "year": y, "pe": pe})
            k = exact["car"].get(concepto)
            if k and k not in d:
                d[k] = valor
    elegido = {}
    for inst, d in cars.items():
        individual = d["pe"].startswith(("10", "40"))
        clave = (d["nit"], d["year"])
        puntaje = (1 if individual else 0, int(inst) if inst.isdigit() else 0)
        if clave not in elegido or puntaje > elegido[clave][0]:
            elegido[clave] = (puntaje, inst)
    inst_sel = {inst: clave for clave, (_, inst) in elegido.items()}
    print("  instancias elegidas:", len(inst_sel))

    # 4) Valores por instancia
    vals = {}  # instancia -> {clave: valor_en_miles}
    for ds in ("bal", "eri", "flu"):
        for y in years:
            for nit, inst, concepto, valor in crudo[(ds, y)]:
                if inst not in inst_sel:
                    continue
                k = exact[ds].get(concepto)
                v = num(valor)
                if k is None or v is None:
                    continue
                d = vals.setdefault(inst, {})
                if k not in d:
                    d[k] = v * factor_miles(cars[inst].get("unidad", "MILES DE PESOS"))
    crudo.clear()

    # 5) Métricas y salida
    empresas = {}
    detalle = {}
    for inst, (nit, y) in inst_sel.items():
        v = vals.get(inst)
        if not v or ("ing" not in v and "act" not in v):
            continue
        g = lambda k: v.get(k, 0.0)
        ub = v["ub"] if "ub" in v else g("ing") - g("costo")
        gop = g("gadm") + g("gven") + g("gdis") + g("ogas")
        oing = g("oing") + g("ogan")
        uop = v["uop"] if "uop" in v else ub - gop + oing
        ebitda = uop + g("da")
        # "Otros pasivos financieros" y "Préstamos" suelen repetir el mismo valor: se toma uno u otro, no la suma.
        deuda_c = g("opfc") if "opfc" in v else g("pcor") + g("pcpnc")
        deuda_nc = g("opfnc") if "opfnc" in v else g("pnc")
        deuda = deuda_c + deuda_nc
        ktn = (g("cxc") + g("inv") + g("aimp") + g("oanf")) - (g("cxp") + g("pimp") + g("pbe") + g("oprov"))
        der = dict(v, ub=ub, gop=gop, oing_net=oing, uop=uop, ebitda=ebitda, deuda=deuda, ktn=ktn)
        fila = [int(round(der.get(m, 0.0))) for m in METRICAS]

        c = cars[inst]
        e = empresas.setdefault(nit, {"n": nit, "y": {}, "_ult": 0})
        e["y"][str(y)] = fila
        if y >= e["_ult"]:
            e["_ult"] = y
            ciiu = re.sub(r"\D", "", (c.get("ciiu", "") or "").split("-")[0])
            e.update({
                "r": c.get("razon", "").strip(), "k": ciiu, "c": c.get("ciudad", "").strip(), "d": c.get("depto", "").strip(),
                "s": c.get("estado", "").strip(), "f": (c.get("fconst", "") or "")[:10], "t": c.get("tipo", "").strip(),
                "g": ("1" if c["pe"].startswith(("10", "20")) else "2"),
            })
            # el perfil es el del año más reciente; los años ya procesados se conservan (el orden de iteración no es cronológico)
            detalle.setdefault(nit, {"p": {}, "y": {}})["p"] = {
                "objeto": (c.get("objeto", "") or "").strip(), "clase": c.get("clase", "").strip(),
                "rf": c.get("rf", "").strip(), "rfop": c.get("rfop", "").strip(), "pe": c["pe"]}
        detalle.setdefault(nit, {"p": {}, "y": {}})["y"][str(y)] = {k: int(round(x)) for k, x in der.items() if x}

    # Reparar textos corruptos con vocabulario de las propias palabras limpias (+ títulos CIIU)
    textos = []
    for nit, d in detalle.items():
        textos.append(d["p"].get("objeto", ""))
    for e in empresas.values():
        textos += [e.get("r", ""), e.get("t", ""), e.get("s", ""), e.get("c", ""), e.get("d", "")]
    ciiu_path = os.path.join(OUT_DIR, "ciiu.json")
    if os.path.exists(ciiu_path):
        cj = json.load(open(ciiu_path, encoding="utf-8"))
        textos += list(cj["clases"].values()) + list(cj["grupos"].values()) + [v["n"] for v in cj["divisiones"].values()]
    vocab = construir_vocab(textos)
    for e in empresas.values():
        for k in ("r", "t", "s", "c", "d"):
            if k in e:
                e[k] = reparar(e[k], vocab)
    for d in detalle.values():
        for k in ("objeto", "clase", "rf", "rfop"):
            if k in d["p"]:
                d["p"][k] = reparar(d["p"][k], vocab)

    os.makedirs(OUT_DIR, exist_ok=True)
    lista = []
    for nit in sorted(empresas):
        e = empresas[nit]
        e.pop("_ult", None)
        if e.get("r"):
            lista.append(e)

    # detalle.bin + índice
    idx = {}
    with open(os.path.join(OUT_DIR, "detalle.bin"), "wb") as f:
        for e in lista:
            blob = gzip.compress(json.dumps(detalle[e["n"]], ensure_ascii=False, separators=(",", ":")).encode("utf-8"), 6)
            idx[e["n"]] = [f.tell(), len(blob)]
            f.write(blob)
    with open(os.path.join(OUT_DIR, "detalle.idx.json"), "w", encoding="utf-8") as f:
        json.dump(idx, f, separators=(",", ":"))

    meta = {
        "fuente": "Superintendencia de Sociedades - datos.gov.co (Estados financieros NIIF)",
        "datasets": DS, "fuenteActualizado": fuente_act, "generado": datetime.now().strftime("%Y-%m-%d"),
        "unidad": "Miles de COP", "anios": years, "empresas": len(lista), "metricas": METRICAS,
    }
    with open(prev_path, "w", encoding="utf-8") as f:
        json.dump({"meta": meta, "e": lista}, f, ensure_ascii=False, separators=(",", ":"))
    mb = lambda p: os.path.getsize(os.path.join(OUT_DIR, p)) / 1048576
    print("OK: %d empresas | empresas.json %.1f MB | detalle.bin %.1f MB" % (len(lista), mb("empresas.json"), mb("detalle.bin")))


if __name__ == "__main__":
    main()
