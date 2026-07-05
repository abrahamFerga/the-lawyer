// TheLawyer local orchestration — the full Cortex-based stack in one command:
//   dotnet run --project src/TheLawyer.AppHost    (or `aspire run`)
//
// Zero-config: the chat assistant uses Cortex's dependency-free Mock provider and the RAG
// pipeline uses the deterministic Mock embedder, so no API keys are required. The web UI ships
// from the Cortex frontend packages (@cortex/ui, @cortex/admin-ui); until they publish to npm,
// run them from the Cortex repo with VITE_API_BASE pointed at this API.

var builder = DistributedApplication.CreateBuilder(args);

// STABLE dev password (overridable via Parameters:cortex-pg-password in user-secrets). Postgres
// bakes the password into the data volume at first init and never re-reads it — with Aspire's
// default *generated* password, losing/regenerating user-secrets leaves the volume unopenable
// ("28P01 password authentication failed", the API waits forever, the console shows nothing).
// A fixed dev default can't drift. Local demo container only — not a production credential.
var pgPassword = builder.AddParameter("cortex-pg-password", "thelawyer-dev-only", secret: true);

var postgres = builder.AddPostgres("cortex-pg", password: pgPassword)
    // pgvector-enabled Postgres — Cortex's opt-in RAG pipeline needs the vector extension at
    // migration time. pg17 pairs with Aspire's data-volume mount (see the Cortex AppHost notes:
    // a volume created by a different Postgres major needs `docker volume rm` to reset).
    .WithImage("pgvector/pgvector")
    .WithImageTag("pg17")
    .WithDataVolume();

var platformDb = postgres.AddDatabase("cortex-platform");
var auditDb = postgres.AddDatabase("cortex-audit");

var redis = builder.AddRedis("cortex-redis");

// AI + embedding providers stay parameters so a real model is a user-secret away:
//   dotnet user-secrets --project src/TheLawyer.AppHost set "Parameters:ai-provider" "OpenAI"
//   dotnet user-secrets --project src/TheLawyer.AppHost set "Parameters:ai-api-key"  "sk-..."
var aiProvider = builder.AddParameter("ai-provider", "Mock", publishValueAsDefault: true);
var aiModel = builder.AddParameter("ai-model", "gpt-4o-mini", publishValueAsDefault: true);
var aiApiKey = builder.AddParameter("ai-api-key", "", secret: true);

builder.AddProject<Projects.TheLawyer_Host>("thelawyer-api")
    .WithReference(platformDb)
    .WithReference(auditDb)
    .WithReference(redis)
    .WaitFor(platformDb)
    .WaitFor(auditDb)
    .WithEnvironment("Ai__Provider", aiProvider)
    .WithEnvironment("Ai__Model", aiModel)
    .WithEnvironment("Ai__ApiKey", aiApiKey)
    .WithExternalHttpEndpoints();

builder.Build().Run();
