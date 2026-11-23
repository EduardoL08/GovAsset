// Cliente da API REST do GovAsset (ASP.NET Core).

const BASE = (import.meta.env.VITE_API_URL as string | undefined)?.replace(/\/$/, '') ?? 'http://localhost:5080'

export type Categoria = 'Acessibilidade' | 'Seguranca' | 'Conservacao' | 'Mobiliario'
export type Situacao = 'Inexistente' | 'Inadequado' | 'Adequado'
export type Status = 'Aberta' | 'EmAnalise' | 'Confirmada' | 'Descartada'
export type Severidade = 'Baixa' | 'Media' | 'Alta'

export const CATEGORIAS: Record<Categoria, string> = {
  Acessibilidade: 'Acessibilidade',
  Seguranca: 'Segurança',
  Conservacao: 'Conservação das instalações',
  Mobiliario: 'Mobiliário',
}
export const SITUACOES: Record<Situacao, string> = {
  Inexistente: 'Não existe',
  Inadequado: 'Existe, mas está inadequado / danificado',
  Adequado: 'Existe e está adequado',
}
export const STATUS: Record<Status, string> = {
  Aberta: 'Aberta',
  EmAnalise: 'Em análise',
  Confirmada: 'Confirmada',
  Descartada: 'Descartada',
}

export interface Escola {
  id: number
  codigoInep: string
  nome: string
  municipio: string
  uf: string
  rede: string
  latitude: number | null
  longitude: number | null
}
export interface ItemCatalogo { chave: string; rotulo: string }
export interface CategoriaCatalogo { categoria: Categoria; rotulo: string; itens: ItemCatalogo[] }

export interface DivergenciaResumo {
  id: number
  relatoId: number
  escolaId: number
  escolaNome: string
  codigoInep: string
  municipio: string
  categoria: Categoria
  item: string
  itemRotulo: string
  severidade: Severidade
  descricao: string
  status: Status
  criadoEmUtc: string
  latitude: number | null
  longitude: number | null
}
export interface Pagina<T> { total: number; pagina: number; tamanho: number; itens: T[] }
export interface PontoMapa {
  id: number
  latitude: number
  longitude: number
  escolaNome: string
  categoria: Categoria
  severidade: Severidade
  status: Status
}
export interface DivergenciaDetalhe extends Omit<DivergenciaResumo, 'latitude' | 'longitude'> {
  uf: string
  notaAnalise: string | null
  atualizadoEmUtc: string | null
  situacaoRelatada: Situacao
  descricaoRelato: string
  relatoCriadoEmUtc: string
  latitude: number | null
  longitude: number | null
  evidencias: { id: number; contentType: string }[]
  registroOficial: {
    fonte: 'Inep' | 'Siope'
    ano: number
    existe: boolean | null
    valor: number | null
    rede: string | null
    descricao: string | null
  }
}
export interface Contagem { chave: string; total: number }
export interface Resumo {
  totalRelatos: number
  totalEscolas: number
  totalDivergencias: number
  porStatus: Contagem[]
  porCategoria: Contagem[]
  escolasComMaisDivergencias: { escolaId: number; nome: string; municipio: string; total: number }[]
}
export interface ImportResultado { inseridos: number; atualizados: number; ignorados: number; erros: string[] }
export interface FiltrosDivergencia {
  escolaId?: number
  categoria?: Categoria | ''
  status?: Status | ''
  severidade?: Severidade | ''
}

// ------------------------------------------------------------------ infraestrutura

const TOKEN_KEY = 'govasset.token'
export const getToken = () => sessionStorage.getItem(TOKEN_KEY)
export const setToken = (t: string | null) =>
  t ? sessionStorage.setItem(TOKEN_KEY, t) : sessionStorage.removeItem(TOKEN_KEY)

export class ApiError extends Error {
  status: number
  campos: Record<string, string[]>
  constructor(status: number, message: string, campos: Record<string, string[]> = {}) {
    super(message)
    this.status = status
    this.campos = campos
  }
}

async function paraErro(res: Response): Promise<ApiError> {
  let mensagem = `Erro ${res.status}`
  let campos: Record<string, string[]> = {}
  try {
    const corpo = await res.json()
    if (corpo?.errors) {
      campos = corpo.errors
      mensagem = Object.values(campos).flat().join(' ')
    } else if (corpo?.mensagem) mensagem = corpo.mensagem
    else if (corpo?.detail) mensagem = corpo.detail
    else if (corpo?.title) mensagem = corpo.title
  } catch {
    /* corpo vazio ou não-JSON */
  }
  if (res.status === 429) mensagem = 'Muitas tentativas em pouco tempo. Aguarde alguns minutos e tente novamente.'
  if (res.status === 401) window.dispatchEvent(new Event('govasset:unauthorized'))
  return new ApiError(res.status, mensagem, campos)
}

async function enviar(path: string, init: RequestInit = {}): Promise<Response> {
  const headers = new Headers(init.headers)
  const token = getToken()
  if (token && path.startsWith('/api/admin')) headers.set('Authorization', `Bearer ${token}`)
  let res: Response
  try {
    res = await fetch(BASE + path, { ...init, headers })
  } catch {
    throw new ApiError(0, 'Não foi possível conectar ao servidor. Verifique sua conexão e tente novamente.')
  }
  if (!res.ok) throw await paraErro(res)
  return res
}

async function json<T>(path: string, init: RequestInit = {}): Promise<T> {
  const res = await enviar(path, init)
  return (await res.json()) as T
}

const corpoJson = (dados: unknown): RequestInit => ({
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify(dados),
})

function query(params: Record<string, string | number | undefined | null>): string {
  const q = new URLSearchParams()
  for (const [k, v] of Object.entries(params)) if (v !== undefined && v !== null && v !== '') q.set(k, String(v))
  const s = q.toString()
  return s ? `?${s}` : ''
}

// ------------------------------------------------------------------ público

export const getCatalogo = () => json<CategoriaCatalogo[]>('/api/catalogo')
export const buscarEscolas = (busca: string) => json<Escola[]>(`/api/escolas${query({ busca })}`)

export const criarRelato = (form: FormData) =>
  json<{ id: number; protocolo: string }>('/api/relatos', { method: 'POST', body: form })

// ------------------------------------------------------------------ admin

export const login = (email: string, senha: string) =>
  json<{ token: string; expiraEmUtc: string; nome: string }>('/api/auth/login', {
    method: 'POST',
    ...corpoJson({ email, senha }),
  })

export const getResumo = () => json<Resumo>('/api/admin/resumo')

export const listarDivergencias = (f: FiltrosDivergencia, pagina: number, tamanho = 15) =>
  json<Pagina<DivergenciaResumo>>(`/api/admin/divergencias${query({ ...f, pagina, tamanho })}`)

export const mapaDivergencias = (f: FiltrosDivergencia) =>
  json<PontoMapa[]>(`/api/admin/divergencias/mapa${query({ ...f })}`)

export const getDivergencia = (id: number) => json<DivergenciaDetalhe>(`/api/admin/divergencias/${id}`)

export const atualizarDivergencia = (id: number, status: Status, nota: string) =>
  enviar(`/api/admin/divergencias/${id}`, { method: 'PATCH', ...corpoJson({ status, nota }) })

export async function baixarEvidencia(id: number): Promise<Blob> {
  return (await enviar(`/api/admin/evidencias/${id}`)).blob()
}

export type TipoImportacao = 'escolas' | 'inep' | 'siope'
export function importarCsv(tipo: TipoImportacao, arquivo: File) {
  const form = new FormData()
  form.append('arquivo', arquivo)
  return json<ImportResultado>(`/api/admin/importacao/${tipo}`, { method: 'POST', body: form })
}
export const conciliarTudo = () =>
  json<{ novasDivergencias: number }>('/api/admin/importacao/conciliar', { method: 'POST' })
