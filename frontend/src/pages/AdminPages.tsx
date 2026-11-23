import { useCallback, useEffect, useState, type FormEvent } from 'react'
import { Link, Navigate, useLocation, useNavigate, useParams } from 'react-router-dom'
import {
  ApiError, CATEGORIAS, SITUACOES, STATUS, atualizarDivergencia, conciliarTudo, getDivergencia, getResumo,
  importarCsv, listarDivergencias, mapaDivergencias,
  type Categoria, type DivergenciaDetalhe, type DivergenciaResumo, type Escola, type FiltrosDivergencia,
  type ImportResultado, type Pagina, type PontoMapa, type Resumo, type Severidade, type Status, type TipoImportacao,
} from '../api/client'
import { useAuth } from '../auth/AuthContext'
import { DivergenciasMapa } from '../components/DivergenciasMapa'
import { LocationPicker } from '../components/LocationPicker'
import { SchoolPicker } from '../components/SchoolPicker'
import { Aviso, EvidenciaImagem, Paginacao, SeveridadeBadge, StatusBadge, formatarData } from '../components/Widgets'

const mensagem = (e: unknown) => (e instanceof ApiError || e instanceof Error ? e.message : 'Erro inesperado.')

// ------------------------------------------------------------------ login
export function Login() {
  const { autenticado, entrar } = useAuth()
  const navegar = useNavigate()
  const local = useLocation() as { state: { de?: string } | null }
  const [email, setEmail] = useState('')
  const [senha, setSenha] = useState('')
  const [erro, setErro] = useState<string | null>(null)
  const [enviando, setEnviando] = useState(false)

  if (autenticado) return <Navigate to="/admin" replace />

  async function enviar(ev: FormEvent) {
    ev.preventDefault()
    setErro(null)
    setEnviando(true)
    try {
      await entrar(email, senha)
      navegar(local.state?.de ?? '/admin', { replace: true })
    } catch (e) {
      setErro(mensagem(e))
    } finally {
      setEnviando(false)
    }
  }

  return (
    <form className="card form narrow" onSubmit={enviar}>
      <h1>Painel da gestão pública</h1>
      <p className="muted">Acesso restrito a usuários autorizados.</p>
      {erro && <div className="alert error" role="alert">{erro}</div>}
      <div className="field">
        <label className="label" htmlFor="email">E-mail</label>
        <input id="email" type="email" autoComplete="username" required value={email} onChange={(e) => setEmail(e.target.value)} />
      </div>
      <div className="field">
        <label className="label" htmlFor="senha">Senha</label>
        <input id="senha" type="password" autoComplete="current-password" required value={senha} onChange={(e) => setSenha(e.target.value)} />
      </div>
      <button className="btn primary" disabled={enviando}>{enviando ? 'Entrando…' : 'Entrar'}</button>
    </form>
  )
}

// ------------------------------------------------------------------ painel de transparência
export function Painel() {
  const [resumo, setResumo] = useState<Resumo | null>(null)
  const [escola, setEscola] = useState<Escola | null>(null)
  const [categoria, setCategoria] = useState<Categoria | ''>('')
  const [status, setStatus] = useState<Status | ''>('')
  const [severidade, setSeveridade] = useState<Severidade | ''>('')
  const [pagina, setPagina] = useState(1)
  const [vista, setVista] = useState<'lista' | 'mapa'>('lista')

  const [dados, setDados] = useState<Pagina<DivergenciaResumo> | null>(null)
  const [pontos, setPontos] = useState<PontoMapa[]>([])
  const [erro, setErro] = useState<string | null>(null)
  const [carregando, setCarregando] = useState(false)

  const filtros: FiltrosDivergencia = { escolaId: escola?.id, categoria, status, severidade }
  const chave = JSON.stringify(filtros)

  useEffect(() => {
    getResumo().then(setResumo).catch((e) => setErro(mensagem(e)))
  }, [])

  const carregar = useCallback(async () => {
    setCarregando(true)
    setErro(null)
    try {
      const f = JSON.parse(chave) as FiltrosDivergencia
      if (vista === 'lista') setDados(await listarDivergencias(f, pagina))
      else setPontos(await mapaDivergencias(f))
    } catch (e) {
      setErro(mensagem(e))
    } finally {
      setCarregando(false)
    }
  }, [chave, pagina, vista])

  useEffect(() => { void carregar() }, [carregar])

  function mudar<T>(set: (v: T) => void) {
    return (v: T) => { set(v); setPagina(1) }
  }

  const maxCat = Math.max(1, ...(resumo?.porCategoria.map((c) => c.total) ?? [1]))

  return (
    <>
      <h1>Painel de transparência</h1>
      <Aviso />
      {erro && <div className="alert error" role="alert">{erro}</div>}

      {resumo && (
        <section className="stats">
          <div className="stat"><strong>{resumo.totalRelatos}</strong><span>relatos recebidos</span></div>
          <div className="stat"><strong>{resumo.totalDivergencias}</strong><span>divergências (indicativos)</span></div>
          <div className="stat">
            <strong>{resumo.porStatus.find((s) => s.chave === 'Aberta')?.total ?? 0}</strong><span>abertas</span>
          </div>
          <div className="stat"><strong>{resumo.totalEscolas}</strong><span>escolas na base oficial</span></div>
        </section>
      )}

      {resumo && resumo.totalDivergencias > 0 && (
        <section className="card">
          <h2>Divergências por categoria</h2>
          <ul className="bars">
            {resumo.porCategoria.map((c) => (
              <li key={c.chave}>
                <span className="bar-label">{CATEGORIAS[c.chave as Categoria] ?? c.chave}</span>
                <span className="bar"><i style={{ width: `${(c.total / maxCat) * 100}%` }} /></span>
                <span className="bar-value">{c.total}</span>
              </li>
            ))}
          </ul>
          {resumo.escolasComMaisDivergencias.length > 0 && (
            <>
              <h3>Escolas com mais indicativos</h3>
              <ol>
                {resumo.escolasComMaisDivergencias.map((e) => (
                  <li key={e.escolaId}>{e.nome} <span className="muted">({e.municipio}) — {e.total}</span></li>
                ))}
              </ol>
            </>
          )}
        </section>
      )}

      <section className="card">
        <div className="filters">
          <SchoolPicker label="Filtrar por escola" value={escola} onChange={mudar(setEscola)} placeholder="Todas as escolas" />
          <div className="field">
            <label className="label" htmlFor="f-cat">Categoria</label>
            <select id="f-cat" value={categoria} onChange={(e) => mudar(setCategoria)(e.target.value as Categoria | '')}>
              <option value="">Todas</option>
              {(Object.keys(CATEGORIAS) as Categoria[]).map((c) => <option key={c} value={c}>{CATEGORIAS[c]}</option>)}
            </select>
          </div>
          <div className="field">
            <label className="label" htmlFor="f-st">Status</label>
            <select id="f-st" value={status} onChange={(e) => mudar(setStatus)(e.target.value as Status | '')}>
              <option value="">Todos</option>
              {(Object.keys(STATUS) as Status[]).map((s) => <option key={s} value={s}>{STATUS[s]}</option>)}
            </select>
          </div>
          <div className="field">
            <label className="label" htmlFor="f-sev">Severidade</label>
            <select id="f-sev" value={severidade} onChange={(e) => mudar(setSeveridade)(e.target.value as Severidade | '')}>
              <option value="">Todas</option>
              <option value="Alta">Alta</option>
              <option value="Media">Média</option>
              <option value="Baixa">Baixa</option>
            </select>
          </div>
        </div>

        <div className="tabs" role="tablist">
          <button role="tab" aria-selected={vista === 'lista'} className={vista === 'lista' ? 'on' : ''} onClick={() => setVista('lista')}>Lista</button>
          <button role="tab" aria-selected={vista === 'mapa'} className={vista === 'mapa' ? 'on' : ''} onClick={() => setVista('mapa')}>Mapa</button>
        </div>

        {carregando && <div className="muted">Carregando…</div>}

        {vista === 'lista' && dados && (
          <>
            <div className="table-wrap">
              <table>
                <thead>
                  <tr><th>Escola</th><th>Categoria / item</th><th>Indicativo</th><th>Severidade</th><th>Status</th><th>Data</th></tr>
                </thead>
                <tbody>
                  {dados.itens.map((d) => (
                    <tr key={d.id}>
                      <td><Link to={`/admin/divergencias/${d.id}`}><strong>{d.escolaNome}</strong></Link><div className="muted">{d.municipio}</div></td>
                      <td>{CATEGORIAS[d.categoria]}<div className="muted">{d.itemRotulo}</div></td>
                      <td className="desc">{d.descricao}</td>
                      <td><SeveridadeBadge valor={d.severidade} /></td>
                      <td><StatusBadge valor={d.status} /></td>
                      <td>{formatarData(d.criadoEmUtc)}</td>
                    </tr>
                  ))}
                  {dados.itens.length === 0 && <tr><td colSpan={6} className="muted center">Nenhuma divergência encontrada com esses filtros.</td></tr>}
                </tbody>
              </table>
            </div>
            <Paginacao pagina={dados.pagina} total={dados.total} tamanho={dados.tamanho} onChange={setPagina} />
          </>
        )}

        {vista === 'mapa' && (
          <>
            <DivergenciasMapa pontos={pontos} />
            {pontos.length === 0 && !carregando && <div className="muted">Nenhuma divergência com localização para esses filtros.</div>}
          </>
        )}
      </section>
    </>
  )
}

// ------------------------------------------------------------------ detalhe da divergência
export function DivergenciaDetalhePage() {
  const { id } = useParams()
  const [d, setD] = useState<DivergenciaDetalhe | null>(null)
  const [erro, setErro] = useState<string | null>(null)
  const [status, setStatus] = useState<Status>('Aberta')
  const [nota, setNota] = useState('')
  const [salvando, setSalvando] = useState(false)
  const [salvo, setSalvo] = useState(false)

  useEffect(() => {
    getDivergencia(Number(id))
      .then((r) => { setD(r); setStatus(r.status); setNota(r.notaAnalise ?? '') })
      .catch((e) => setErro(mensagem(e)))
  }, [id])

  async function salvar(ev: FormEvent) {
    ev.preventDefault()
    setSalvando(true)
    setSalvo(false)
    setErro(null)
    try {
      await atualizarDivergencia(Number(id), status, nota)
      setSalvo(true)
    } catch (e) {
      setErro(mensagem(e))
    } finally {
      setSalvando(false)
    }
  }

  if (erro && !d) return <><Link to="/admin">← Voltar</Link><div className="alert error">{erro}</div></>
  if (!d) return <div className="muted">Carregando…</div>

  const o = d.registroOficial
  const ponto = d.latitude != null && d.longitude != null ? { lat: d.latitude, lon: d.longitude } : null

  return (
    <>
      <Link to="/admin">← Voltar ao painel</Link>
      <h1>{d.escolaNome}</h1>
      <p className="muted">{d.municipio}/{d.uf} · INEP {d.codigoInep}</p>
      <Aviso />

      <section className="card">
        <div className="row spread">
          <h2>{CATEGORIAS[d.categoria]} — {d.itemRotulo}</h2>
          <div><SeveridadeBadge valor={d.severidade} /> <StatusBadge valor={d.status} /></div>
        </div>
        <p>{d.descricao}</p>
      </section>

      <section className="grid2">
        <article className="card">
          <h3>Registro oficial ({o.fonte === 'Inep' ? 'Censo Escolar/INEP' : 'FNDE/SIOPE'} {o.ano})</h3>
          {o.fonte === 'Inep'
            ? <p>Item registrado como <strong>{o.existe ? 'existente' : 'inexistente'}</strong> na escola.</p>
            : <p>Investimento registrado: <strong>R$ {o.valor?.toLocaleString('pt-BR', { minimumFractionDigits: 2 })}</strong> na rede {o.rede}.</p>}
          {o.descricao && <p className="muted">{o.descricao}</p>}
        </article>
        <article className="card">
          <h3>Relato da comunidade ({formatarData(d.relatoCriadoEmUtc)})</h3>
          <p><strong>{SITUACOES[d.situacaoRelatada]}</strong></p>
          <p>{d.descricaoRelato}</p>
        </article>
      </section>

      {d.evidencias.length > 0 && (
        <section className="card">
          <h3>Evidências fotográficas</h3>
          <div className="photos">
            {d.evidencias.map((e, i) => <EvidenciaImagem key={e.id} id={e.id} indice={i + 1} />)}
          </div>
        </section>
      )}

      {ponto && (
        <section className="card">
          <h3>Localização</h3>
          <LocationPicker value={ponto} onChange={() => undefined} somenteLeitura />
        </section>
      )}

      <form className="card form" onSubmit={salvar}>
        <h3>Análise da gestão</h3>
        {erro && <div className="alert error" role="alert">{erro}</div>}
        {salvo && <div className="alert ok" role="status">Análise salva.</div>}
        <div className="field">
          <label className="label" htmlFor="status">Status</label>
          <select id="status" value={status} onChange={(e) => setStatus(e.target.value as Status)}>
            {(Object.keys(STATUS) as Status[]).map((s) => <option key={s} value={s}>{STATUS[s]}</option>)}
          </select>
        </div>
        <div className="field">
          <label className="label" htmlFor="nota">Nota da análise</label>
          <textarea id="nota" rows={3} maxLength={1000} value={nota} onChange={(e) => setNota(e.target.value)} />
        </div>
        <button className="btn primary" disabled={salvando}>{salvando ? 'Salvando…' : 'Salvar análise'}</button>
        {d.atualizadoEmUtc && <span className="muted"> Última atualização: {formatarData(d.atualizadoEmUtc)}</span>}
      </form>
    </>
  )
}

// ------------------------------------------------------------------ importação de dados abertos
const IMPORTACOES: { tipo: TipoImportacao; titulo: string; colunas: string; ajuda: string }[] = [
  { tipo: 'escolas', titulo: '1. Escolas', colunas: 'codigo_inep, nome, municipio, uf, rede, latitude, longitude', ajuda: 'Cadastro das escolas (código INEP com 8 dígitos). Importe primeiro.' },
  { tipo: 'inep', titulo: '2. Infraestrutura (Censo Escolar / INEP)', colunas: 'codigo_inep, categoria, item, existe, ano', ajuda: 'Uma linha por item de infraestrutura da escola. "existe": sim/nao.' },
  { tipo: 'siope', titulo: '3. Orçamento (FNDE / SIOPE)', colunas: 'ano, uf, municipio, rede, categoria, valor, descricao', ajuda: 'Valores por rede de ensino. "municipio" vazio vale para toda a rede da UF.' },
]

function CartaoImportacao({ cfg }: { cfg: (typeof IMPORTACOES)[number] }) {
  const [arquivo, setArquivo] = useState<File | null>(null)
  const [res, setRes] = useState<ImportResultado | null>(null)
  const [erro, setErro] = useState<string | null>(null)
  const [enviando, setEnviando] = useState(false)

  async function enviar(ev: FormEvent) {
    ev.preventDefault()
    if (!arquivo) return
    setEnviando(true)
    setErro(null)
    setRes(null)
    try {
      setRes(await importarCsv(cfg.tipo, arquivo))
    } catch (e) {
      setErro(mensagem(e))
    } finally {
      setEnviando(false)
    }
  }

  return (
    <form className="card form" onSubmit={enviar}>
      <h3>{cfg.titulo}</h3>
      <p className="muted">{cfg.ajuda}</p>
      <p><code>{cfg.colunas}</code></p>
      <input type="file" accept=".csv,text/csv" onChange={(e) => setArquivo(e.target.files?.[0] ?? null)} aria-label={`Arquivo CSV — ${cfg.titulo}`} />
      <div><button className="btn primary small" disabled={!arquivo || enviando}>{enviando ? 'Importando…' : 'Importar'}</button></div>
      {erro && <div className="alert error" role="alert">{erro}</div>}
      {res && (
        <div className="alert ok" role="status">
          {res.inseridos} inseridos · {res.atualizados} atualizados · {res.ignorados} ignorados
          {res.erros.length > 0 && (
            <ul className="errors">{res.erros.map((e, i) => <li key={i}>{e}</li>)}</ul>
          )}
        </div>
      )}
    </form>
  )
}

export function Importacao() {
  const [msg, setMsg] = useState<string | null>(null)
  const [erro, setErro] = useState<string | null>(null)
  const [rodando, setRodando] = useState(false)

  async function conciliar() {
    setRodando(true)
    setMsg(null)
    setErro(null)
    try {
      const r = await conciliarTudo()
      setMsg(`Conciliação concluída: ${r.novasDivergencias} nova(s) divergência(s).`)
    } catch (e) {
      setErro(mensagem(e))
    } finally {
      setRodando(false)
    }
  }

  return (
    <>
      <h1>Importação de dados abertos</h1>
      <p className="muted">
        Envie arquivos CSV (UTF-8, separados por vírgula ou ponto e vírgula) no layout abaixo, gerados a partir do
        Censo Escolar (INEP) e do SIOPE (FNDE). Depois de importar, rode a conciliação para reavaliar os relatos já recebidos.
      </p>
      {IMPORTACOES.map((c) => <CartaoImportacao key={c.tipo} cfg={c} />)}
      <section className="card">
        <h3>4. Reprocessar conciliação</h3>
        <p className="muted">Compara todos os relatos com os registros oficiais atuais. Não duplica divergências já existentes.</p>
        <button className="btn primary" onClick={conciliar} disabled={rodando}>{rodando ? 'Processando…' : 'Executar conciliação'}</button>
        {msg && <div className="alert ok" role="status">{msg}</div>}
        {erro && <div className="alert error" role="alert">{erro}</div>}
      </section>
    </>
  )
}
