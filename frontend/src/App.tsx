export default function App() {
  return (
    <div className="min-h-screen bg-slate-50 text-slate-900">
      <header className="border-b bg-white px-6 py-4 shadow-sm">
        <h1 className="text-xl font-semibold">adaptive-observability</h1>
        <p className="text-sm text-slate-500">Phase 0 placeholder dashboard</p>
      </header>
      <main className="p-6">
        <p className="text-slate-700">
          Backend health: <code>http://localhost:8080/health</code>
        </p>
        <p className="mt-2 text-sm text-slate-500">
          Phase 3 (dashboard MVP) will replace this placeholder with the live admin UI.
        </p>
      </main>
    </div>
  );
}
