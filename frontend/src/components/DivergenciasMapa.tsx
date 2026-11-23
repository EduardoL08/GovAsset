import { useEffect } from 'react'
import { CircleMarker, MapContainer, Popup, TileLayer, useMap } from 'react-leaflet'
import { Link } from 'react-router-dom'
import L from 'leaflet'
import 'leaflet/dist/leaflet.css'
import { CATEGORIAS, STATUS, type PontoMapa, type Severidade } from '../api/client'

const COR: Record<Severidade, string> = { Alta: '#c62828', Media: '#ef8f00', Baixa: '#1e88a8' }

function Enquadrar({ pontos }: { pontos: PontoMapa[] }) {
  const map = useMap()
  useEffect(() => {
    if (pontos.length === 0) return
    const limites = L.latLngBounds(pontos.map((p) => [p.latitude, p.longitude] as [number, number]))
    map.fitBounds(limites, { padding: [30, 30], maxZoom: 16 })
  }, [map, pontos])
  return null
}

export function DivergenciasMapa({ pontos }: { pontos: PontoMapa[] }) {
  return (
    <div className="map-box" style={{ height: 460 }}>
      <MapContainer center={[-19.9167, -43.9345]} zoom={11} style={{ height: '100%' }}>
        <TileLayer
          attribution='&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a>'
          url="https://tile.openstreetmap.org/{z}/{x}/{y}.png"
        />
        <Enquadrar pontos={pontos} />
        {pontos.map((p) => (
          <CircleMarker
            key={p.id}
            center={[p.latitude, p.longitude]}
            radius={9}
            pathOptions={{ color: '#fff', weight: 2, fillColor: COR[p.severidade], fillOpacity: 0.9 }}
          >
            <Popup>
              <strong>{p.escolaNome}</strong>
              <br />
              {CATEGORIAS[p.categoria]} · {STATUS[p.status]}
              <br />
              <Link to={`/admin/divergencias/${p.id}`}>Ver detalhes</Link>
            </Popup>
          </CircleMarker>
        ))}
      </MapContainer>
      <div className="legend">
        <span><i style={{ background: COR.Alta }} /> Alta</span>
        <span><i style={{ background: COR.Media }} /> Média</span>
        <span><i style={{ background: COR.Baixa }} /> Baixa</span>
      </div>
    </div>
  )
}
