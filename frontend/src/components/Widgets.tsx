import { useEffect, useState } from 'react'
import { baixarEvidencia, STATUS, type Severidade, type Status } from '../api/client'

export function SeveridadeBadge({ valor }: { valor: Severidade }) {
  const rotulo = { Baixa: 'Baixa', Media: 'Média', Alta: 'Alta' }[valor]
  return <span className={`badge sev-${valor.toLowerCase()}`}>{rotulo}</span>
}

export function StatusBadge({ valor }: { valor: Status }) {
  return <span className={`badge st-${valor.toLowerCase()}`}>{STATUS[valor]}</span>
}

export function Aviso() {
  return (
    <p className="notice" role="note">
      As divergências são <strong>indicativos</strong> gerados pelo cruzamento entre relatos da comunidade e registros
      oficiais. Elas não comprovam irregularidade: a confirmação depende da análise da gestão pública e dos órgãos de
      fiscalização.
    </p>
  )
}

/** Foto de evidência: o endpoint é protegido por JWT, então a imagem é baixada com o token e exibida via blob. */
export function EvidenciaImagem({ id, indice }: { id: number; indice: number }) {
  const [url, setUrl] = useState<string | null>(null)
  const [falha, setFalha] = useState(false)

  useEffect(() => {
    let cancelado = false
    let criada: string | null = null
    baixarEvidencia(id)
      .then((blob) => {
        if (cancelado) return
        criada = URL.createObjectURL(blob)
        setUrl(criada)
      })
      .catch(() => !cancelado && setFalha(true))
    return () => {
      cancelado = true
      if (criada) URL.revokeObjectURL(criada)
    }
  }, [id])

  if (falha) return <div className="photo placeholder">Foto indisponível</div>
  if (!url) return <div className="photo placeholder">Carregando…</div>
  return (
    <a href={url} target="_blank" rel="noreferrer" className="photo">
      <img src={url} alt={`Evidência fotográfica ${indice}`} />
    </a>
  )
}

export function Paginacao({
  pagina, total, tamanho, onChange,
}: { pagina: number; total: number; tamanho: number; onChange: (p: number) => void }) {
  const paginas = Math.max(1, Math.ceil(total / tamanho))
  if (paginas <= 1) return null
  return (
    <div className="pager">
      <button className="btn small ghost" disabled={pagina <= 1} onClick={() => onChange(pagina - 1)}>← Anterior</button>
      <span>Página {pagina} de {paginas}</span>
      <button className="btn small ghost" disabled={pagina >= paginas} onClick={() => onChange(pagina + 1)}>Próxima →</button>
    </div>
  )
}

export function formatarData(iso: string): string {
  return new Date(iso).toLocaleString('pt-BR', { dateStyle: 'short', timeStyle: 'short' })
}
