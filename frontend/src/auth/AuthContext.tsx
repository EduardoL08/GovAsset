import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from 'react'
import { getToken, login as apiLogin, setToken } from '../api/client'

interface AuthState {
  autenticado: boolean
  nome: string | null
  entrar: (email: string, senha: string) => Promise<void>
  sair: () => void
}

const NOME_KEY = 'govasset.nome'
const AuthContext = createContext<AuthState | null>(null)

export function AuthProvider({ children }: { children: ReactNode }) {
  const [token, setTokenState] = useState<string | null>(getToken())
  const [nome, setNome] = useState<string | null>(sessionStorage.getItem(NOME_KEY))

  const sair = useCallback(() => {
    setToken(null)
    sessionStorage.removeItem(NOME_KEY)
    setTokenState(null)
    setNome(null)
  }, [])

  const entrar = useCallback(async (email: string, senha: string) => {
    const r = await apiLogin(email, senha)
    setToken(r.token)
    sessionStorage.setItem(NOME_KEY, r.nome)
    setTokenState(r.token)
    setNome(r.nome)
  }, [])

  // a API respondeu 401 (token expirado/inválido): encerra a sessão
  useEffect(() => {
    window.addEventListener('govasset:unauthorized', sair)
    return () => window.removeEventListener('govasset:unauthorized', sair)
  }, [sair])

  const valor = useMemo(() => ({ autenticado: !!token, nome, entrar, sair }), [token, nome, entrar, sair])
  return <AuthContext.Provider value={valor}>{children}</AuthContext.Provider>
}

// eslint-disable-next-line react-refresh/only-export-components
export function useAuth(): AuthState {
  const ctx = useContext(AuthContext)
  if (!ctx) throw new Error('useAuth deve ser usado dentro de <AuthProvider>')
  return ctx
}
