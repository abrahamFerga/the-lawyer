using Cortex.AspNetCore.Connectors;
using Cortex.AspNetCore.Hosting;
using Cortex.AspNetCore.Modules;
using Cortex.Connectors.AzureBlob;
using Cortex.Connectors.LocalFolder;
using Cortex.Connectors.MsGraph;
using Cortex.Connectors.Peer;
using Cortex.Modules.Legal;

// ─────────────────────────────────────────────────────────────────────────────
// TheLawyer — a single-vertical legal system built ENTIRELY on the Cortex
// platform. The platform packages supply auth (OIDC / Entra External ID-ready),
// multi-tenancy, RBAC, append-only audit, chat over AG-UI + SignalR, document
// tools, the permission-aware RAG pipeline, background jobs, connectors, and
// the admin API; this host adds only the legal module and product composition.
// ─────────────────────────────────────────────────────────────────────────────

var builder = WebApplication.CreateBuilder(args);

builder.AddCortexPlatform();

builder.AddCortexModule<LegalModule>();

// Data-source connectors (default-off per tenant; enabled in the admin console):
builder.AddCortexConnector<LocalFolderConnector>();
builder.AddCortexConnector<AzureBlobConnector>();
builder.AddCortexConnector<MsGraphConnector>();    // "the firm keeps its files in Microsoft 365"
builder.AddCortexConnector<CortexPeerConnector>(); // talk to sibling Cortex systems

var app = builder.Build();

await app.RunCortexPlatformAsync();

/// <summary>Exposed so integration tests can host this app via WebApplicationFactory&lt;Program&gt;.</summary>
public partial class Program;
