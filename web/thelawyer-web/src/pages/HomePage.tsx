export function HomePage() {
  return (
    <div className="space-y-6">
      <header>
        <h1 className="text-2xl font-semibold">Welcome</h1>
        <p className="text-muted-foreground">
          Foundations epic shell. Home dashboard, recent matters jump-list, and "matter has a heartbeat"
          widgets land in later epics per <code>PLAN.md</code>.
        </p>
      </header>

      <section className="grid grid-cols-1 gap-4 md:grid-cols-3">
        <Card title="Open matters" value="—" note="Populated by Matters epic" />
        <Card title="WIP this week" value="—" note="Populated by Time &amp; Billing epic" />
        <Card title="Trust reconciliation due" value="—" note="Populated by Trust epic" />
      </section>
    </div>
  );
}

function Card(props: { title: string; value: string; note: string }) {
  return (
    <div className="rounded-lg border bg-card p-4">
      <div className="text-sm text-muted-foreground">{props.title}</div>
      <div className="mt-1 text-3xl font-semibold">{props.value}</div>
      <div className="mt-2 text-xs text-muted-foreground">{props.note}</div>
    </div>
  );
}
