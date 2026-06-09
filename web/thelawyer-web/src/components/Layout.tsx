import { Outlet, NavLink } from 'react-router-dom';
import { Briefcase, Calendar, Receipt, Wallet, Users, BarChart3, Settings, MessageSquare } from 'lucide-react';
import { cn } from '../lib/utils';

// Layout shell — sidebar (per-role nav) + topbar (tenant switch + persistent timer + chatbot toggle).
// The role-gated nav items will be filtered by the user's claims in a later epic.
// Today every item renders for everyone to demonstrate the shell.
const NAV: ReadonlyArray<{ to: string; label: string; icon: typeof Briefcase }> = [
  { to: '/', label: 'Home', icon: Briefcase },
  { to: '/matters', label: 'Matters', icon: Briefcase },
  { to: '/calendar', label: 'Calendar', icon: Calendar },
  { to: '/billing', label: 'Billing', icon: Receipt },
  { to: '/trust', label: 'Trust', icon: Wallet },
  { to: '/reports', label: 'Reports', icon: BarChart3 },
  { to: '/integrations', label: 'Integrations', icon: Users },
  { to: '/settings', label: 'Settings', icon: Settings },
];

export function Layout() {
  return (
    <div className="flex min-h-screen">
      <aside className="w-56 border-r bg-muted/30">
        <div className="flex h-14 items-center border-b px-4">
          <span className="text-lg font-semibold">TheLawyer</span>
        </div>
        <nav className="flex flex-col gap-1 p-2">
          {NAV.map((item) => (
            <NavLink
              key={item.to}
              to={item.to}
              end={item.to === '/'}
              className={({ isActive }) =>
                cn(
                  'flex items-center gap-2 rounded-md px-3 py-2 text-sm transition-colors',
                  isActive ? 'bg-primary text-primary-foreground' : 'text-foreground hover:bg-muted',
                )
              }
            >
              <item.icon className="h-4 w-4" />
              {item.label}
            </NavLink>
          ))}
        </nav>
      </aside>

      <div className="flex flex-1 flex-col">
        <header className="flex h-14 items-center justify-between border-b px-4">
          <div className="flex items-center gap-3">
            {/* Tenant switcher and persistent matter timer land here in the Time & Billing epic */}
            <span className="text-sm text-muted-foreground">Tenant: <strong>Acme Law LLP</strong></span>
          </div>
          <div className="flex items-center gap-2">
            <button type="button" className="rounded-md p-2 hover:bg-muted" aria-label="Toggle chatbot">
              <MessageSquare className="h-4 w-4" />
            </button>
          </div>
        </header>

        <main className="flex-1 overflow-auto p-6">
          <Outlet />
        </main>
      </div>
    </div>
  );
}
