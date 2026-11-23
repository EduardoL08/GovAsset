import { useEffect, useMemo, useRef, useState } from 'react'

export const MAX_FOTOS = 5
export const MAX_BYTES = 5 * 1024 * 1024
const TIPOS = ['image/jpeg', 'image/png', 'image/webp']

interface Props {
  fotos: File[]
  onChange: (f: File[]) => void
}

export function PhotoPicker({ fotos, onChange }: Props) {
  const entrada = useRef<HTMLInputElement>(null)
  const [aviso, setAviso] = useState<string | null>(null)

  const previews = useMemo(() => fotos.map((f) => URL.createObjectURL(f)), [fotos])
  useEffect(() => () => previews.forEach((u) => URL.revokeObjectURL(u)), [previews])

  function adicionar(lista: FileList | null) {
    if (!lista) return
    const novas: File[] = []
    const problemas: string[] = []
    for (const f of Array.from(lista)) {
      if (!TIPOS.includes(f.type)) problemas.push(`"${f.name}": use JPEG, PNG ou WEBP.`)
      else if (f.size > MAX_BYTES) problemas.push(`"${f.name}": maior que 5 MB.`)
      else novas.push(f)
    }
    const total = [...fotos, ...novas]
    if (total.length > MAX_FOTOS) problemas.push(`Máximo de ${MAX_FOTOS} fotos.`)
    onChange(total.slice(0, MAX_FOTOS))
    setAviso(problemas.length ? problemas.join(' ') : null)
    if (entrada.current) entrada.current.value = ''
  }

  return (
    <div className="field">
      <span className="label">Fotos do problema <span className="muted">(opcional, até {MAX_FOTOS}, 5 MB cada)</span></span>
      <input
        ref={entrada}
        type="file"
        accept={TIPOS.join(',')}
        multiple
        onChange={(e) => adicionar(e.target.files)}
        disabled={fotos.length >= MAX_FOTOS}
        aria-label="Selecionar fotos"
      />
      {aviso && <div className="error">{aviso}</div>}
      {fotos.length > 0 && (
        <ul className="thumbs">
          {fotos.map((f, i) => (
            <li key={`${f.name}-${i}`}>
              <img src={previews[i]} alt={`Pré-visualização da foto ${i + 1}`} />
              <button type="button" className="btn small ghost" onClick={() => onChange(fotos.filter((_, j) => j !== i))}>
                Remover
              </button>
            </li>
          ))}
        </ul>
      )}
      <div className="muted">
        Evite fotografar rostos de pessoas, especialmente de crianças. As fotos são vistas apenas pela gestão pública.
      </div>
    </div>
  )
}
