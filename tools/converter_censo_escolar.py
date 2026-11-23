#!/usr/bin/env python3
"""
Converte a "Tabela de Escola" dos microdados do Censo Escolar (INEP) para o layout do importador do GovAsset.

Uso:
    python converter_censo_escolar.py Tabela_Escola_2025.csv --saida ../data/real [--uf SP] [--municipio "São Paulo"]

Entrada : CSV oficial do INEP (separador ';' ou ',', UTF-8 ou Latin-1), com as colunas CO_ENTIDADE, NO_ENTIDADE,
          NO_MUNICIPIO, SG_UF, TP_DEPENDENCIA, TP_SITUACAO_FUNCIONAMENTO, NU_ANO_CENSO e os indicadores IN_*.
Saída   : escolas.csv  (codigo_inep,nome,municipio,uf,rede,latitude,longitude)
          inep_infraestrutura.csv  (codigo_inep,categoria,item,existe,ano)

Regras:
  * Só escolas PÚBLICAS (dependência 1=Federal, 2=Estadual, 3=Municipal) e EM ATIVIDADE (situação 1).
  * Campo vazio no Censo = "sem informação": a linha NÃO é gerada (não se infere "não existe").
  * Os microdados atuais do INEP não trazem latitude/longitude: o campo sai vazio.
  * Apenas python3 (biblioteca padrão).
"""
import argparse
import csv
import io
import sys
import unicodedata
from pathlib import Path

REDE = {"1": "Federal", "2": "Estadual", "3": "Municipal"}

# (categoria, chave do catálogo do GovAsset) <- coluna do Censo Escolar
INDICADORES = {
    "IN_ACESSIBILIDADE_RAMPAS": ("Acessibilidade", "rampa"),
    "IN_BANHEIRO_PNE": ("Acessibilidade", "banheiro_adaptado"),
    "IN_ACESSIBILIDADE_PISOS_TATEIS": ("Acessibilidade", "piso_tatil"),
    "IN_ACESSIBILIDADE_CORRIMAO": ("Acessibilidade", "corrimao"),
    "IN_ACESSIBILIDADE_ELEVADOR": ("Acessibilidade", "elevador"),
    "IN_BANHEIRO": ("Conservacao", "banheiros"),
}


def ler_texto(caminho: Path) -> str:
    bruto = caminho.read_bytes()
    for enc in ("utf-8-sig", "latin-1"):
        try:
            return bruto.decode(enc)
        except UnicodeDecodeError:
            continue
    raise SystemExit("Não foi possível decodificar o arquivo.")


def norm(s: str) -> str:
    return "".join(c for c in unicodedata.normalize("NFD", s.lower()) if unicodedata.category(c) != "Mn")


def main() -> None:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("entrada", type=Path)
    ap.add_argument("--saida", type=Path, default=Path("."))
    ap.add_argument("--uf", help="filtra por UF (ex.: SP)")
    ap.add_argument("--municipio", help="filtra por nome do município (ex.: 'São Paulo')")
    a = ap.parse_args()

    texto = ler_texto(a.entrada)
    primeira = texto.split("\n", 1)[0]
    delim = ";" if primeira.count(";") > primeira.count(",") else ","
    leitor = csv.DictReader(io.StringIO(texto), delimiter=delim)

    obrigatorias = {"CO_ENTIDADE", "NO_ENTIDADE", "NO_MUNICIPIO", "SG_UF", "TP_DEPENDENCIA",
                    "TP_SITUACAO_FUNCIONAMENTO", "NU_ANO_CENSO"}
    faltando = obrigatorias - set(leitor.fieldnames or [])
    if faltando:
        sys.exit(f"Colunas obrigatórias ausentes: {sorted(faltando)}")
    usadas = {c: v for c, v in INDICADORES.items() if c in leitor.fieldnames}
    ausentes = sorted(set(INDICADORES) - set(usadas))
    if ausentes:
        print(f"AVISO: indicadores ausentes neste arquivo (ignorados): {ausentes}", file=sys.stderr)

    a.saida.mkdir(parents=True, exist_ok=True)
    n_escolas = n_infra = 0
    sem_info = 0
    with open(a.saida / "escolas.csv", "w", newline="", encoding="utf-8") as fe, \
         open(a.saida / "inep_infraestrutura.csv", "w", newline="", encoding="utf-8") as fi:
        we, wi = csv.writer(fe), csv.writer(fi)
        we.writerow(["codigo_inep", "nome", "municipio", "uf", "rede", "latitude", "longitude"])
        wi.writerow(["codigo_inep", "categoria", "item", "existe", "ano"])

        for r in leitor:
            if r["TP_DEPENDENCIA"] not in REDE or r["TP_SITUACAO_FUNCIONAMENTO"] != "1":
                continue
            if a.uf and r["SG_UF"].upper() != a.uf.upper():
                continue
            if a.municipio and norm(r["NO_MUNICIPIO"]) != norm(a.municipio):
                continue
            codigo = r["CO_ENTIDADE"].strip().zfill(8)
            we.writerow([codigo, r["NO_ENTIDADE"].strip(), r["NO_MUNICIPIO"].strip(), r["SG_UF"].strip().upper(),
                         REDE[r["TP_DEPENDENCIA"]], "", ""])
            n_escolas += 1
            for coluna, (cat, item) in usadas.items():
                v = (r[coluna] or "").strip()
                if v not in ("0", "1"):
                    sem_info += 1
                    continue
                wi.writerow([codigo, cat, item, "sim" if v == "1" else "nao", r["NU_ANO_CENSO"].strip()])
                n_infra += 1

    print(f"escolas.csv: {n_escolas} escolas | inep_infraestrutura.csv: {n_infra} linhas "
          f"({sem_info} indicadores sem informação foram omitidos)")


if __name__ == "__main__":
    main()
