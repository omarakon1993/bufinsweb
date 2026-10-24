"""
Genera App_Data/pulso/ciiu.json (secciones, divisiones, grupos y clases) a partir del texto del PDF oficial
del DANE "CIIU Rev. 4 A.C. (2022)" -> https://www.dane.gov.co/files/sen/nomenclatura/ciiu/CIIU_Rev_4_AC2022.pdf

Uso (una sola vez; el resultado se versiona en el repo):
    pdftotext -layout -enc UTF-8 CIIU_Rev_4_AC2022.pdf ciiu.txt
    python -I Tools/PulsoEtl/build_ciiu.py ciiu.txt
"""
import json
import os
import re
import sys

src = sys.argv[1]
lines = open(src, encoding="utf-8").read().split("\n")

start = [i for i, l in enumerate(lines) if re.match(r"^\s*10\.\s+Estructura detallada", l)][-1]
end = next((i for i in range(start + 1, len(lines)) if re.match(r"^\s*11\.\s", lines[i])), len(lines))
# Si no hay "11.", la estructura termina donde empiezan las notas explicativas (cabecera de clase en columna 0)
for i in range(start + 1, min(end, len(lines))):
    if re.match(r"^011 Cultivos", lines[i]):
        end = i
        break

secciones, divisiones, grupos, clases = {}, {}, {}, {}
seccion_actual = None
ultimo = None  # (dict, key) para continuar descripciones envueltas en varias líneas


def limpia(t):
    return re.sub(r"\s+", " ", t).strip()


i = start
while i < end:
    raw = lines[i]
    l = raw.strip()
    i += 1
    if not l or l.startswith("|") or "Clasificación Industrial Internacional" in l or l.startswith("Revisión 4") \
            or l.startswith("DANE,") or l.startswith("División Grupo Clase"):
        continue
    m = re.match(r"^Sección ([A-U])$", l)
    if m:
        seccion_actual = m.group(1)
        # la siguiente línea no vacía es el título
        while i < end and not lines[i].strip():
            i += 1
        secciones[seccion_actual] = limpia(lines[i])
        ultimo = (secciones, seccion_actual)
        i += 1
        continue
    m = re.match(r"^División (\d{2})\s+(.+)$", l)
    if m:
        divisiones[m.group(1)] = {"n": limpia(m.group(2)), "s": seccion_actual}
        ultimo = (divisiones[m.group(1)], "n")
        continue
    m = re.match(r"^(\d{3})\s+(\d{4})(?:\s+(.+))?$", l)  # grupo con una sola clase: "611 6110 Título" (título opcional en la línea)
    if m:
        t = limpia(m.group(3) or "")
        grupos[m.group(1)] = t
        clases[m.group(2)] = t
        ultimo = [(grupos, m.group(1)), (clases, m.group(2))]
        continue
    m = re.match(r"^(\d{4})\s+(.+)$", l)
    if m:
        clases[m.group(1)] = limpia(m.group(2))
        ultimo = (clases, m.group(1))
        continue
    m = re.match(r"^(\d{3})\s+(.+)$", l)
    if m:
        grupos[m.group(1)] = limpia(m.group(2))
        ultimo = (grupos, m.group(1))
        continue
    # continuación de la descripción anterior
    if ultimo is not None and not re.match(r"^\d", l):
        for d, k in (ultimo if isinstance(ultimo, list) else [ultimo]):
            d[k] = limpia(d[k] + " " + l)

# Los títulos de clase más fiables son los encabezados de las notas explicativas (columna 0, sin sangría):
#   "0111 Título"  o  "611 6110 Título" (grupo de una sola clase), con continuación hasta línea en blanco.
def titulo_completo(idx):
    partes = []
    j = idx
    while j < len(lines) and lines[j].strip() and not lines[j].startswith("|"):
        partes.append(lines[j].strip())
        j += 1
    t = limpia(" ".join(partes))
    # El encabezado termina antes del texto explicativo ("Esta clase incluye...", viñetas, etc.)
    t = re.split(r"\s+(?:Esta\s+(?:clase|subclase|divisi\w+|grupo)|Este\s+grupo|[\u2022\u25cf\u25a0])", t)[0]
    return t.strip()


codigos_grupo = {k[:3] for k in clases}
titulos_grupo_notas = set()
for idx in range(end, len(lines)):
    raw = lines[idx]
    m = re.match(r"^(\d{3}) (\d{4}) (\S.*)$", raw)
    if m and m.group(2) in clases:
        t = titulo_completo(idx)[len(m.group(1)) + len(m.group(2)) + 2:]
        grupos[m.group(1)] = t
        clases[m.group(2)] = t
        continue
    m = re.match(r"^(\d{4}) (\S.*)$", raw)
    if m and m.group(1) in clases:
        clases[m.group(1)] = titulo_completo(idx)[5:]
        continue
    m = re.match(r"^(\d{3}) ([A-ZÁÉÍÓÚÑa-z].*)$", raw)
    if m and m.group(1) in codigos_grupo and m.group(1) not in titulos_grupo_notas:
        titulos_grupo_notas.add(m.group(1))
        grupos[m.group(1)] = titulo_completo(idx)[4:]

for _k, _v in list(clases.items()):
    _v = re.split(r"\s+Comprende", _v)[0]          # notas al pie pegadas al título
    clases[_k] = re.sub(r"(?<=[a-záéíóúñ])\d$", "", _v).strip()

# Títulos de sección que arrastran texto de notas del PDF
secciones["K"] = "Actividades financieras y de seguros"
secciones["S"] = "Otras actividades de servicios"

out = {"fuente": "DANE - CIIU Rev. 4 A.C. (2022)", "secciones": secciones, "divisiones": divisiones, "grupos": grupos, "clases": clases}
dest = os.path.join(os.path.dirname(__file__), "..", "..", "App_Data", "pulso", "ciiu.json")
os.makedirs(os.path.dirname(dest), exist_ok=True)
with open(dest, "w", encoding="utf-8") as f:
    json.dump(out, f, ensure_ascii=False, separators=(",", ":"))
print("secciones", len(secciones), "divisiones", len(divisiones), "grupos", len(grupos), "clases", len(clases))
