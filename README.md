# GovAsset — Gestão Participativa de Ativos Escolares (UPX-6)

Plataforma web em que alunos, pais e professores relatam problemas de infraestrutura de escolas públicas (com fotos e
localização) e a gestão pública consulta, em um painel, as **divergências** entre esses relatos e os dados oficiais
(Censo Escolar/INEP e SIOPE/FNDE). Implementa o que está descrito nos itens 2.2, 5 e 6 do documento da UPX-6.

| Parte | Tecnologia | Pasta |
|---|---|---|
| Back-end (API REST) | C# / ASP.NET Core 10, EF Core, PostgreSQL, JWT | `backend/` |
| Front-end (público + painel) | React 19, TypeScript, Vite, Leaflet/OpenStreetMap | `frontend/` |
| Dados de exemplo | CSVs no layout do importador | `data/samples/` |

```
Usuário → formulário (React) → API valida e grava (PostgreSQL) ─┐
Dados abertos INEP/SIOPE → importação (CSV) → registros oficiais ─┴→ motor de conciliação
        → divergência (indicativo) → painel de transparência (lista, filtros por escola/categoria, mapa)
```

## Acesso rápido (para quem vai avaliar/rodar)

1. Instale o **Docker Desktop** e deixe-o aberto.
2. Na pasta do projeto (a que tem o `docker-compose.yml`), rode: `docker compose up -d --build`
3. Abra **http://localhost:5173**

| O quê | Valor |
|---|---|
| Site público (relatar problema) | http://localhost:5173 |
| Painel da gestão | http://localhost:5173/admin/login |
| **E-mail do painel** | `admin@govasset.local` |
| **Senha do painel** | `Admin@123456` |

> Credenciais apenas para uso local/demonstração (definidas no `docker-compose.yml`). Em produção, troque
> `Admin__Email` e `Admin__Senha`.

**Teste em 1 minuto** (escolas reais de São Paulo, Censo Escolar 2025):

| Escola (buscar em "Relatar") | Relato | Resultado no painel |
|---|---|---|
| ALUISIO DE AZEVEDO | Acessibilidade → Rampa de acesso → **Não existe** | divergência **Alta** (o Censo diz que a rampa existe) |
| THEODORO DE MORAES | Acessibilidade → Rampa de acesso → **Existe e está adequado** | divergência **Baixa** (o Censo diz que não há rampa) |

Depois de enviar o relato, entre no painel com o login acima: a divergência aparece na lista e, ao abrir, mostra o
registro oficial × o relato × as fotos.

## Como rodar localmente (recomendado: Docker)

Requisito: só o **Docker Desktop**. Não precisa instalar .NET, Node nem PostgreSQL.

Na pasta onde está o `docker-compose.yml`:

```bash
docker compose up -d --build
```

Isso sobe tudo de uma vez:

| Serviço | Endereço |
|---|---|
| Front (site público + painel) | http://localhost:5173 |
| API | http://localhost:5080 |
| PostgreSQL | localhost:5432 |

Na primeira subida a API cria as tabelas, o usuário admin e **importa sozinha os dados reais** de `data/real/`
(leva alguns segundos; se abrir o site antes, é só recarregar).

| Comando | O que faz |
|---|---|
| `docker compose up -d` | sobe tudo em segundo plano |
| `docker compose down` | para tudo (os dados continuam salvos) |
| `docker compose up -d --build` | recompila depois de mexer no código (front ou back) |
| `docker compose logs -f api` | mostra os logs da API |
| `docker compose down -v` | para tudo **e apaga** banco e fotos (a próxima subida reimporta os dados reais) |

Login do painel: `admin@govasset.local` / `Admin@123456` (definido no `docker-compose.yml`; só para uso local,
nunca use em produção).

Problemas comuns:
- `no configuration file provided: not found` → você está na pasta errada; entre na que contém o `docker-compose.yml`.
- `port is already allocated` → outra coisa já usa a porta (ex.: um PostgreSQL ou container antigo). Pare-o
  (`docker rm -f govasset-db`) ou troque a porta da esquerda no `docker-compose.yml`.
- O aviso `libgssapi_krb5.so.2` no log da API é inofensivo (biblioteca Kerberos opcional do driver do PostgreSQL).

### Desenvolvendo o front com recarga automática

Para editar o front vendo as mudanças na hora, pare o container dele e rode o Vite:

```bash
docker compose stop web
cd frontend
cp .env.example .env     # no PowerShell: copy .env.example .env
npm install
npm run dev              # http://localhost:5173
```

### Alternativa sem Docker para a API

Requisitos: .NET SDK 10 e um PostgreSQL (ajuste `ConnectionStrings:Default`).

```bash
docker run -d --name govasset-db -e POSTGRES_DB=govasset -e POSTGRES_PASSWORD=postgres -p 5432:5432 postgres:17  # ou PostgreSQL próprio
cd backend/src/GovAsset.Api
dotnet run     # Development: cria tabelas, admin e dados reais (+ fictícios de demonstração)
```

No Windows com *Controle de Aplicativo* (Smart App Control) ativo, o `dotnet run` pode ser bloqueado em pastas como
Downloads (erro `0x800711C7`); nesse caso use o Docker.

Testes do back-end: `cd backend && dotnet test`. Front: `npm run build`.

## Dados reais incluídos (Censo Escolar 2025 — São Paulo/SP)

`data/real/` traz **2.825 escolas públicas reais** da cidade de São Paulo e **16.950 indicadores reais de infraestrutura**
do Censo Escolar 2025 (INEP). Em desenvolvimento, a API os importa sozinha na primeira execução (`Seed:RealDataPath`).
Detalhes de origem, tratamento e **limitações** (sem coordenadas, sem orçamento SIOPE real) em `data/real/LEIA-ME.md`.
O conversor para outros municípios/estados está em `tools/converter_censo_escolar.py`.

Teste rápido com dados reais: busque **"ALUISIO DE AZEVEDO"** (EMEI; o Censo registra rampa e elevador como existentes)
e relate *Acessibilidade → Rampa de acesso → Não existe*: o painel mostra uma divergência real **Alta**. Em
**"THEODORO DE MORAES"** (o Censo registra rampa como inexistente), relatar *Rampa → Existe e está adequado* gera o
indicativo **Baixo** de dado possivelmente desatualizado.

## Fluxo completo para testar

1. Abra `/relatar`, escolha uma escola (ex.: "Demo Centro"), categoria *Acessibilidade → Rampa de acesso*, situação
   *Não existe*, anexe uma foto e marque o local no mapa.
2. Entre em `/admin/login`. O relato aparece como divergência de severidade **Alta** (o registro oficial diz que a rampa existe).
3. No detalhe, veja o registro oficial × o relato × as fotos e registre a análise (status e nota).
4. Em *Importação de dados*, importe `data/samples/` na ordem **escolas → inep → siope** e rode a conciliação.

## Motor de conciliação (`Services/ConciliationRules.cs`)

Compara, por escola e categoria, o relato com os registros oficiais. Sempre gera **indicativo**, nunca conclusão.

| Registro oficial | Relato | Resultado |
|---|---|---|
| INEP: item **existe** | "Não existe" | divergência **Alta** |
| INEP: item **existe** | "Inadequado/danificado" | divergência **Média** |
| INEP: item **não existe** | "Existe e está adequado" | divergência **Baixa** (possível dado desatualizado) |
| SIOPE: investimento recente (ano atual ou anterior) na rede/município | problema de *Conservação* ou *Mobiliário* | indicativo **Baixo** (orçamento é da rede, não da escola) |

É idempotente (um relato gera no máximo uma divergência por registro oficial) e roda a cada novo relato e sob demanda
(`POST /api/admin/importacao/conciliar`). As regras são funções puras, cobertas por testes em `backend/tests`.

## Importação de dados abertos

O importador (`Services/CsvImportService.cs`) lê CSV UTF-8 (separador `,` ou `;`):

- `escolas.csv`: `codigo_inep,nome,municipio,uf,rede,latitude,longitude`
- `inep_infraestrutura.csv`: `codigo_inep,categoria,item,existe,ano` (um item por linha; `item` é uma chave do `Domain/Catalogo.cs`)
- `siope_orcamento.csv`: `ano,uf,municipio,rede,categoria,valor,descricao`

> **Atenção:** o conversor do **Censo Escolar** já existe (`tools/converter_censo_escolar.py`, testado com a Tabela de
> Escola 2025). O do **SIOPE/FNDE ainda não**: o serviço do FNDE não é acessível do ambiente de desenvolvimento, então
> não há orçamento real incluído. Os CSVs em `data/samples/` e o modo demo (`Seed:Demo`) são **inventados**, só para
> exercitar o fluxo; `data/real/` é real.

## API (resumo)

Público: `GET /api/catalogo`, `GET /api/escolas?busca=`, `POST /api/relatos` (multipart: `EscolaId, Categoria, Item, Situacao,
Descricao, Latitude, Longitude, Fotos[]`), `POST /api/auth/login`.
Autenticado (Bearer JWT): `GET /api/admin/resumo`, `GET /api/admin/divergencias` (filtros `escolaId, categoria, status, severidade, pagina`),
`GET /api/admin/divergencias/mapa`, `GET|PATCH /api/admin/divergencias/{id}`, `GET /api/admin/evidencias/{id}`,
`POST /api/admin/importacao/{escolas|inep|siope|conciliar}`. Saúde: `GET /api/saude`.

## Privacidade e segurança (LGPD)

- Quem relata **não se identifica**; não guardamos IP, nome original do arquivo nem dados pessoais. Há aviso e aceite no formulário.
- Painel e fotos protegidos por **JWT** (fotos não são públicas); senha com PBKDF2-SHA256; login e envio de relatos com limite de requisições por IP.
- Fotos validadas pelo conteúdo real (JPEG/PNG/WEBP), máx. 5 por relato e 5 MB cada.
- Pendências antes de uso real: remover metadados EXIF das fotos, moderação de conteúdo (rostos/dados de terceiros),
  política de retenção, trilha de auditoria, e revisão jurídica do texto de consentimento.

## Deploy (Render + Vercel)

**Back-end (Render, Web Service → Docker, *Root Directory* `backend`)** com um PostgreSQL da Render. Variáveis:

| Variável | Valor |
|---|---|
| `DATABASE_URL` | URL interna do PostgreSQL da Render (já é lida pela API) |
| `Jwt__Key` | segredo aleatório com 32+ caracteres |
| `CORS_ORIGINS` | URL do front na Vercel, ex.: `https://govasset.vercel.app` (vários: separados por vírgula) |
| `Admin__Email`, `Admin__Senha` | usuário inicial do painel (senha com 10+ caracteres) |
| `Storage__Path` | pasta das fotos (ver aviso abaixo) |
| `Seed__Demo` | `false` em produção |

> **Fotos na Render:** o disco do plano gratuito é efêmero (as fotos somem a cada deploy/reinício). Para produção use um
> *Persistent Disk* (plano pago) apontando `Storage__Path` para ele, ou implemente `IEvidenceStorage` com S3/Cloudinary/Supabase Storage.

**Front-end (Vercel, *Root Directory* `frontend`)**: defina `VITE_API_URL` com a URL da API na Render. O `vercel.json` já
trata as rotas da SPA.

## Limitações conhecidas

- O esquema do banco é criado com `EnsureCreated` (adequado ao protótipo). Para evoluir o banco em produção, adote EF Migrations.
- Mapas usam os tiles públicos do OpenStreetMap (ok para protótipo; em volume alto, use um provedor de tiles próprio).
- Importações carregam as tabelas em memória: adequado para milhares de linhas, não para o Censo nacional inteiro de uma vez.
- Como no documento da UPX-6, não há ainda validação com usuários reais nem métricas de desempenho.
