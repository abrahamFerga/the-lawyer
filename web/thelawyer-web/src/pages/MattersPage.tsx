export function MattersPage() {
  return (
    <div className="space-y-6">
      <header className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-semibold">Matters</h1>
          <p className="text-muted-foreground">Matter list + tabbed detail page land in the Matters epic.</p>
        </div>
        <button type="button" className="rounded-md bg-primary px-4 py-2 text-sm text-primary-foreground" disabled>
          New matter (coming in Matters epic)
        </button>
      </header>

      <div className="rounded-lg border bg-card p-6 text-sm text-muted-foreground">
        Empty state. The Matters epic adds the matter list, the conflict-check intake flow, and the
        tabbed matter detail page (Overview / Documents / Time / Billing / Notes / Communications / Tasks).
      </div>
    </div>
  );
}
