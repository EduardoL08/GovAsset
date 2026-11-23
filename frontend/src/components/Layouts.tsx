import { Link, NavLink, Navigate, Outlet, useLocation } from 'react-router-dom'
import { useAuth } from '../auth/AuthContext'

export function PublicLayout() {
  const { autenticado } = useAuth()
  return (
    <div className="app">
      <header className="topbar">
        <Link to="/" className="brand" aria-label="GovAsset — página inicial">
          <span className="brand-mark" aria-hidden="true">▲</span> GovAsset
        </Link>
        <nav aria-label="Principal">
          <NavLink to="/relatar">Relatar um problema</NavLink>
          <NavLink to={autenticado ? '/admin' : '/admin/login'}>Painel da gestão</NavLink>
        </nav>
      </header>
      <main className="container">
        <Outlet />
      </main>
      <footer className="footer">
        GovAsset · Gestão Participativa de Ativos Escolares · Dados oficiais: INEP (Censo Escolar) e FNDE (SIOPE) ·
        ODS 4 e 16
      </footer>
    </div>
  )
}

export function AdminLayout() {
  const { autenticado, nome, sair } = useAuth()
  const local = useLocation()
  if (!autenticado) return <Navigate to="/admin/login" replace state={{ de: local.pathname }} />

  return (
    <div className="app">
      <header className="topbar admin">
        <Link to="/admin" className="brand">
          <span className="brand-mark" aria-hidden="true">▲</span> GovAsset <small>Painel</small>
        </Link>
        <nav aria-label="Painel">
          <NavLink to="/admin" end>Divergências</NavLink>
          <NavLink to="/admin/importacao">Importação de dados</NavLink>
          <Link to="/">Site público</Link>
          <span className="user">{nome}</span>
          <button className="btn small ghost" onClick={sair}>Sair</button>
        </nav>
      </header>
      <main className="container wide">
        <Outlet />
      </main>
    </div>
  )
}
