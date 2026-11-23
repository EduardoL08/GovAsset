import { BrowserRouter, Route, Routes } from 'react-router-dom'
import { AuthProvider } from './auth/AuthContext'
import { AdminLayout, PublicLayout } from './components/Layouts'
import { DivergenciaDetalhePage, Importacao, Login, Painel } from './pages/AdminPages'
import { Home, Relatar, RelatoEnviado } from './pages/PublicPages'

export default function App() {
  return (
    <BrowserRouter>
      <AuthProvider>
        <Routes>
          <Route element={<PublicLayout />}>
            <Route path="/" element={<Home />} />
            <Route path="/relatar" element={<Relatar />} />
            <Route path="/relato-enviado" element={<RelatoEnviado />} />
            <Route path="/admin/login" element={<Login />} />
          </Route>
          <Route path="/admin" element={<AdminLayout />}>
            <Route index element={<Painel />} />
            <Route path="divergencias/:id" element={<DivergenciaDetalhePage />} />
            <Route path="importacao" element={<Importacao />} />
          </Route>
          <Route path="*" element={<PublicLayout />}>
            <Route path="*" element={<p className="card">Página não encontrada.</p>} />
          </Route>
        </Routes>
      </AuthProvider>
    </BrowserRouter>
  )
}
