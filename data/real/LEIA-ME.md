# Dados reais incluídos

| Arquivo | Conteúdo | Origem |
|---|---|---|
| `escolas.csv` | 2.825 escolas **públicas em atividade** do município de **São Paulo/SP** (1.689 municipais, 1.131 estaduais, 5 federais): código INEP, nome, rede | Censo Escolar 2025 (INEP) |
| `inep_infraestrutura.csv` | 16.950 indicadores reais por escola: rampa, banheiro adaptado (PNE), piso tátil, corrimão, elevador e banheiros | Censo Escolar 2025 (INEP) |

**Origem exata.** Tabela de Escola dos microdados do Censo Escolar da Educação Básica 2025 (INEP/MEC, dados abertos),
na cópia filtrada para a cidade de São Paulo mantida em
`github.com/anderson-engenheiro-de-dados/analise-educacao-publica-sao-paulo` (`data/raw/escolas_sp_2025.csv`,
SHA-256 `370211bf93f1e505ceee9eef50116211c78ab588516adb7abf35a4add1f5d0d8`). Convertido por
`tools/converter_censo_escolar.py`. Fonte oficial: https://www.gov.br/inep/pt-br/acesso-a-informacao/dados-abertos/microdados/censo-escolar

**Como foi tratado**
- Só escolas públicas (dependência federal, estadual ou municipal) com situação "em atividade".
- Campo vazio no Censo = sem informação → a linha **não** é gerada (não se supõe "não existe").
- "Banheiros" (categoria Conservação) vem de `IN_BANHEIRO`; os demais itens de Acessibilidade vêm de
  `IN_ACESSIBILIDADE_RAMPAS`, `IN_BANHEIRO_PNE`, `IN_ACESSIBILIDADE_PISOS_TATEIS`, `IN_ACESSIBILIDADE_CORRIMAO`,
  `IN_ACESSIBILIDADE_ELEVADOR`.

**O que NÃO está aqui**
- **Latitude/longitude**: os microdados atuais do INEP não trazem coordenadas (colunas vazias). No mapa do painel,
  só aparecem relatos em que a pessoa marcou o local.
- **Orçamento SIOPE (FNDE)**: não foi possível obter dados reais (o serviço do FNDE é inacessível a partir do ambiente
  de desenvolvimento). Sem `siope_orcamento.csv` real, o motor não gera indicativos de orçamento. Não inventamos valores.
- Segurança (extintor etc.) e Mobiliário: o Censo não tem indicadores equivalentes nas colunas usadas; relatos dessas
  categorias não geram divergência com o Censo.
- **Relatos**: não há relatos de exemplo sobre escolas reais (não se atribui problema inventado a uma escola de verdade).
  Os relatos vêm da própria plataforma.

## Como atualizar ou trocar de cidade/estado
```bash
# baixe a Tabela_Escola do ano em gov.br/inep (microdados do Censo Escolar) e rode:
python tools/converter_censo_escolar.py Tabela_Escola_2025.csv --saida data/real --uf MG --municipio "Belo Horizonte"
```
Com o banco vazio, a API importa esta pasta na primeira execução (`Seed:RealDataPath`). Com o banco já criado, use a
tela *Importação de dados* do painel (escolas → inep) e depois *Executar conciliação*.
