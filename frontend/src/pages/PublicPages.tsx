import { useEffect, useMemo, useState, type FormEvent } from 'react'
import { Link, useLocation, useNavigate } from 'react-router-dom'
import {
  ApiError, CATEGORIAS, SITUACOES, criarRelato, getCatalogo,
  type Categoria, type CategoriaCatalogo, type Escola, type Situacao,
} from '../api/client'
import { LocationPicker, type Ponto } from '../components/LocationPicker'
import { PhotoPicker } from '../components/PhotoPicker'
import { SchoolPicker } from '../components/SchoolPicker'

export function Home() {
  return (
    <>
      <section className="hero">
        <h1>Ajude a cuidar da escola pública da sua comunidade</h1>
        <p>
          Alunos, pais e professores podem relatar problemas de acessibilidade, segurança, conservação e mobiliário,
          com fotos e localização. O GovAsset compara os relatos com os dados oficiais de infraestrutura e orçamento
          para apoiar a transparência e a atuação da gestão pública.
        </p>
        <Link to="/relatar" className="btn primary large">Relatar um problema</Link>
      </section>

      <section className="grid3">
        <article className="card">
          <h2>1. Você relata</h2>
          <p>Escolha a escola, descreva o problema e, se quiser, anexe fotos e marque o local no mapa.</p>
        </article>
        <article className="card">
          <h2>2. O sistema cruza os dados</h2>
          <p>
            O relato é comparado com o Censo Escolar (INEP) e com o orçamento da rede (FNDE/SIOPE). Quando há diferença,
            é registrado um indicativo para análise.
          </p>
        </article>
        <article className="card">
          <h2>3. A gestão analisa</h2>
          <p>Gestores e órgãos de fiscalização consultam as divergências por escola e categoria, inclusive no mapa.</p>
        </article>
      </section>

      <section className="card">
        <h2>Sua privacidade</h2>
        <p>
          Não pedimos nome, e-mail nem telefone. Guardamos apenas o necessário para registrar o problema: escola,
          descrição, fotos e localização (se você informar). As fotos não ficam públicas.
        </p>
      </section>
    </>
  )
}

interface Erros { [campo: string]: string | undefined }

export function Relatar() {
  const navegar = useNavigate()
  const [catalogo, setCatalogo] = useState<CategoriaCatalogo[]>([])
  const [erroCatalogo, setErroCatalogo] = useState<string | null>(null)

  const [escola, setEscola] = useState<Escola | null>(null)
  const [categoria, setCategoria] = useState<Categoria | ''>('')
  const [item, setItem] = useState('')
  const [situacao, setSituacao] = useState<Situacao | ''>('')
  const [descricao, setDescricao] = useState('')
  const [fotos, setFotos] = useState<File[]>([])
  const [local, setLocal] = useState<Ponto | null>(null)
  const [ciente, setCiente] = useState(false)

  const [erros, setErros] = useState<Erros>({})
  const [erroGeral, setErroGeral] = useState<string | null>(null)
  const [enviando, setEnviando] = useState(false)

  useEffect(() => {
    getCatalogo().then(setCatalogo).catch((e) => setErroCatalogo(e instanceof Error ? e.message : 'Falha ao carregar o formulário.'))
  }, [])

  const itens = useMemo(() => catalogo.find((c) => c.categoria === categoria)?.itens ?? [], [catalogo, categoria])
  const referencia: Ponto | null = escola?.latitude != null && escola.longitude != null
    ? { lat: escola.latitude, lon: escola.longitude }
    : null

  function validar(): Erros {
    const e: Erros = {}
    if (!escola) e.escola = 'Selecione a escola.'
    if (!categoria) e.categoria = 'Escolha a categoria.'
    if (categoria && !item) e.item = 'Escolha o item.'
    if (!situacao) e.situacao = 'Informe a situação do item.'
    if (descricao.trim().length < 10) e.descricao = 'Descreva o problema com pelo menos 10 caracteres.'
    if (!ciente) e.ciente = 'É preciso concordar com o tratamento dos dados para enviar.'
    return e
  }

  async function enviar(ev: FormEvent) {
    ev.preventDefault()
    setErroGeral(null)
    const e = validar()
    setErros(e)
    if (Object.keys(e).length > 0) return

    const form = new FormData()
    form.append('EscolaId', String(escola!.id))
    form.append('Categoria', categoria)
    form.append('Item', item)
    form.append('Situacao', situacao)
    form.append('Descricao', descricao.trim())
    if (local) {
      form.append('Latitude', String(local.lat))
      form.append('Longitude', String(local.lon))
    }
    fotos.forEach((f) => form.append('Fotos', f))

    setEnviando(true)
    try {
      const r = await criarRelato(form)
      navegar('/relato-enviado', { state: { protocolo: r.protocolo } })
    } catch (err) {
      setErroGeral(err instanceof ApiError ? err.message : 'Não foi possível enviar o relato.')
      window.scrollTo({ top: 0, behavior: 'smooth' })
    } finally {
      setEnviando(false)
    }
  }

  return (
    <form className="card form" onSubmit={enviar} noValidate>
      <h1>Relatar um problema</h1>
      <p className="muted">Leva poucos minutos. Não pedimos seus dados pessoais.</p>

      {erroCatalogo && <div className="alert error">{erroCatalogo}</div>}
      {erroGeral && <div className="alert error" role="alert">{erroGeral}</div>}

      <SchoolPicker value={escola} onChange={setEscola} error={erros.escola} />

      <fieldset className="field">
        <legend className="label">Categoria</legend>
        <div className="chips">
          {catalogo.map((c) => (
            <label key={c.categoria} className={`chip ${categoria === c.categoria ? 'on' : ''}`}>
              <input
                type="radio" name="categoria" value={c.categoria}
                checked={categoria === c.categoria}
                onChange={() => { setCategoria(c.categoria); setItem('') }}
              />
              {CATEGORIAS[c.categoria]}
            </label>
          ))}
        </div>
        {erros.categoria && <div className="error">{erros.categoria}</div>}
      </fieldset>

      <div className="field">
        <label className="label" htmlFor="item">Item</label>
        <select id="item" value={item} onChange={(e) => setItem(e.target.value)} disabled={!categoria}>
          <option value="">{categoria ? 'Selecione…' : 'Escolha a categoria primeiro'}</option>
          {itens.map((i) => <option key={i.chave} value={i.chave}>{i.rotulo}</option>)}
        </select>
        {erros.item && <div className="error">{erros.item}</div>}
      </div>

      <fieldset className="field">
        <legend className="label">Qual é a situação?</legend>
        {(Object.keys(SITUACOES) as Situacao[]).map((s) => (
          <label key={s} className="radio">
            <input type="radio" name="situacao" value={s} checked={situacao === s} onChange={() => setSituacao(s)} />
            {SITUACOES[s]}
          </label>
        ))}
        {erros.situacao && <div className="error">{erros.situacao}</div>}
      </fieldset>

      <div className="field">
        <label className="label" htmlFor="descricao">Descreva o problema</label>
        <textarea
          id="descricao" rows={5} maxLength={2000} value={descricao}
          onChange={(e) => setDescricao(e.target.value)}
          placeholder="O que acontece, desde quando, quem é afetado…"
        />
        <div className="muted right">{descricao.length}/2000</div>
        {erros.descricao && <div className="error">{erros.descricao}</div>}
      </div>

      <PhotoPicker fotos={fotos} onChange={setFotos} />
      <LocationPicker value={local} onChange={setLocal} referencia={referencia} />

      <div className="field">
        <label className="radio">
          <input type="checkbox" checked={ciente} onChange={(e) => setCiente(e.target.checked)} />
          <span>
            Concordo que as informações, fotos e localização enviadas sejam tratadas pela gestão pública para análise da
            infraestrutura escolar, conforme a LGPD (Lei nº 13.709/2018). Não incluí dados pessoais de terceiros.
          </span>
        </label>
        {erros.ciente && <div className="error">{erros.ciente}</div>}
      </div>

      <button className="btn primary large" disabled={enviando}>{enviando ? 'Enviando…' : 'Enviar relato'}</button>
    </form>
  )
}

export function RelatoEnviado() {
  const { state } = useLocation() as { state: { protocolo?: string } | null }
  return (
    <section className="card center">
      <h1>Relato enviado. Obrigado!</h1>
      {state?.protocolo && <p>Protocolo: <strong>{state.protocolo}</strong></p>}
      <p>Seu relato foi registrado e será considerado na análise da infraestrutura desta escola.</p>
      <Link to="/relatar" className="btn primary">Enviar outro relato</Link>{' '}
      <Link to="/" className="btn ghost">Voltar ao início</Link>
    </section>
  )
}
