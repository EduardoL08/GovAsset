import { useEffect, useState } from 'react'
import L from 'leaflet'
import { MapContainer, Marker, TileLayer, useMap, useMapEvents } from 'react-leaflet'
import markerIcon from 'leaflet/dist/images/marker-icon.png'
import markerIcon2x from 'leaflet/dist/images/marker-icon-2x.png'
import markerShadow from 'leaflet/dist/images/marker-shadow.png'
import 'leaflet/dist/leaflet.css'

export const pinIcon = L.icon({
  iconUrl: markerIcon,
  iconRetinaUrl: markerIcon2x,
  shadowUrl: markerShadow,
  iconSize: [25, 41],
  iconAnchor: [12, 41],
  popupAnchor: [1, -34],
  shadowSize: [41, 41],
})

export interface Ponto { lat: number; lon: number }

const BELO_HORIZONTE: [number, number] = [-19.9167, -43.9345]

function Cliques({ onPick }: { onPick: (p: Ponto) => void }) {
  useMapEvents({ click: (e) => onPick({ lat: e.latlng.lat, lon: e.latlng.lng }) })
  return null
}

// Depende de números (não do array) para só recentrar quando o ponto muda, e não a cada render do formulário.
function Recentrar({ lat, lon, zoom }: { lat: number; lon: number; zoom: number }) {
  const map = useMap()
  useEffect(() => {
    map.setView([lat, lon], zoom)
  }, [map, lat, lon, zoom])
  return null
}

interface Props {
  value: Ponto | null
  onChange: (p: Ponto | null) => void
  /** Ponto de referência (ex.: coordenadas da escola) usado quando ainda não há localização. */
  referencia?: Ponto | null
  /** Somente exibição (ex.: painel da gestão): sem botões, sem clique e sem arrastar. */
  somenteLeitura?: boolean
}

/**
 * Localização do relato: obtida pelo navegador (geolocalização) e conferida em mapa Leaflet/OpenStreetMap.
 * O usuário pode ajustar clicando no mapa ou arrastando o marcador, ou não informar localização.
 */
export function LocationPicker({ value, onChange, referencia, somenteLeitura = false }: Props) {
  const [status, setStatus] = useState<string | null>(null)
  const [buscando, setBuscando] = useState(false)

  const centro: [number, number] = value
    ? [value.lat, value.lon]
    : referencia
      ? [referencia.lat, referencia.lon]
      : BELO_HORIZONTE
  const zoom = value || referencia ? 17 : 12

  function usarMinhaLocalizacao() {
    if (!('geolocation' in navigator)) {
      setStatus('Seu navegador não oferece geolocalização. Clique no mapa para marcar o local.')
      return
    }
    setBuscando(true)
    setStatus(null)
    navigator.geolocation.getCurrentPosition(
      (pos) => {
        setBuscando(false)
        onChange({ lat: pos.coords.latitude, lon: pos.coords.longitude })
        setStatus('Localização obtida. Confira no mapa e ajuste se necessário.')
      },
      (err) => {
        setBuscando(false)
        setStatus(
          err.code === err.PERMISSION_DENIED
            ? 'Permissão negada. Você pode clicar no mapa para marcar o local manualmente.'
            : 'Não foi possível obter a localização. Clique no mapa para marcá-la.',
        )
      },
      { enableHighAccuracy: true, timeout: 15000 },
    )
  }

  return (
    <div className="field">
      {!somenteLeitura && <span className="label">Localização do problema <span className="muted">(opcional)</span></span>}
      {!somenteLeitura && <div className="row">
        <button type="button" className="btn small" onClick={usarMinhaLocalizacao} disabled={buscando}>
          {buscando ? 'Obtendo…' : 'Usar minha localização'}
        </button>
        {value && (
          <button type="button" className="btn small ghost" onClick={() => { onChange(null); setStatus(null) }}>
            Remover localização
          </button>
        )}
      </div>}
      <div className="map-box" style={{ height: 280 }}>
        <MapContainer center={centro} zoom={zoom} scrollWheelZoom={false} style={{ height: '100%' }}>
          <TileLayer
            attribution='&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a>'
            url="https://tile.openstreetmap.org/{z}/{x}/{y}.png"
          />
          <Recentrar lat={centro[0]} lon={centro[1]} zoom={zoom} />
          {!somenteLeitura && <Cliques onPick={onChange} />}
          {value && (
            <Marker
              position={[value.lat, value.lon]}
              icon={pinIcon}
              draggable={!somenteLeitura}
              eventHandlers={{
                dragend: (e) => {
                  const p = (e.target as L.Marker).getLatLng()
                  onChange({ lat: p.lat, lon: p.lng })
                },
              }}
            />
          )}
        </MapContainer>
      </div>
      <div className="muted" aria-live="polite">
        {somenteLeitura ? 'Local informado no relato.' : value
          ? `Marcado: ${value.lat.toFixed(5)}, ${value.lon.toFixed(5)}`
          : 'Clique no mapa ou use sua localização para marcar o ponto exato.'}
        {!somenteLeitura && status && <> · {status}</>}
      </div>
    </div>
  )
}
