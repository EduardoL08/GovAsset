import { useEffect, useId, useState } from 'react'
import { buscarEscolas, type Escola } from '../api/client'

interface Props {
  value: Escola | null
  onChange: (e: Escola | null) => void
  label?: string
  placeholder?: string
  error?: string
}

/** Busca escolas por nome, município ou código INEP (com atraso para não sobrecarregar a API). */
export function SchoolPicker({ value, onChange, label = 'Escola', placeholder = 'Digite o nome, município ou código INEP', error }: Props) {
  const id = useId()
  const [texto, setTexto] = useState('')
  const [resultados, setResultados] = useState<Escola[]>([])
  const [carregando, setCarregando] = useState(false)
  const [falha, setFalha] = useState<string | null>(null)

  useEffect(() => {
    const busca = texto.trim()
    if (busca.length < 2) return
    let cancelado = false
    const t = setTimeout(async () => {
      setCarregando(true)
      setFalha(null)
      try {
        const r = await buscarEscolas(busca)
        if (!cancelado) setResultados(r)
      } catch (e) {
        if (!cancelado) setFalha(e instanceof Error ? e.message : 'Falha na busca.')
      } finally {
        if (!cancelado) setCarregando(false)
      }
    }, 300)
    return () => {
      cancelado = true
      clearTimeout(t)
    }
  }, [texto])

  if (value) {
    return (
      <div className="field">
        <span className="label">{label}</span>
        <div className="selected-school">
          <div>
            <strong>{value.nome}</strong>
            <div className="muted">
              {value.municipio}/{value.uf} · INEP {value.codigoInep} · Rede {value.rede}
            </div>
          </div>
          <button type="button" className="btn small ghost" onClick={() => onChange(null)}>
            Trocar
          </button>
        </div>
      </div>
    )
  }

  const mostrar = texto.trim().length >= 2 ? resultados : []
  return (
    <div className="field">
      <label className="label" htmlFor={id}>{label}</label>
      <input
        id={id}
        type="search"
        value={texto}
        onChange={(e) => setTexto(e.target.value)}
        placeholder={placeholder}
        autoComplete="off"
        aria-invalid={!!error}
      />
      {carregando && <div className="muted">Buscando…</div>}
      {falha && <div className="error">{falha}</div>}
      {mostrar.length > 0 && (
        <ul className="suggestions" role="listbox" aria-label="Escolas encontradas">
          {mostrar.map((e) => (
            <li key={e.id}>
              <button type="button" onClick={() => { onChange(e); setTexto(''); setResultados([]) }}>
                <strong>{e.nome}</strong>
                <span className="muted"> {e.municipio}/{e.uf} · INEP {e.codigoInep}</span>
              </button>
            </li>
          ))}
        </ul>
      )}
      {!carregando && !falha && texto.trim().length >= 2 && mostrar.length === 0 && (
        <div className="muted">Nenhuma escola encontrada.</div>
      )}
      {error && <div className="error">{error}</div>}
    </div>
  )
}
