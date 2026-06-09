// TheLawyer — Aspire AppHost.
//
// Local orchestration: spins up Postgres (with pgvector extension), Redis, the API,
// and the Vite dev server. The same composition produces the deployment manifest
// for Azure Container Apps via `azd init` + `azd up`.

var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres")
    .WithImage("pgvector/pgvector", "pg16")
    .WithDataVolume(name: "thelawyer-postgres-data")
    .WithPgAdmin();

var thelawyerDb = postgres.AddDatabase("thelawyerdb");

var redis = builder.AddRedis("redis")
    .WithDataVolume(name: "thelawyer-redis-data");

var api = builder.AddProject<Projects.TheLawyer_Api>("api")
    .WithReference(thelawyerDb)
    .WithReference(redis)
    .WaitFor(postgres)
    .WaitFor(redis)
    .WithEnvironment("ConnectionStrings__postgres", thelawyerDb.Resource.ConnectionStringExpression);

// The SPA dev server. Production deploy serves the built SPA from Azure Static Web Apps
// (or via a separate Container App with NGINX) — that's wired in the IaC, not here.
builder.AddNpmApp("web", "../../web/thelawyer-web", "dev")
    .WithReference(api)
    .WithHttpEndpoint(env: "VITE_PORT")
    .WithExternalHttpEndpoints();

builder.Build().Run();
