using Quorum.App.Features.Catalog.Publish;
using Quorum.App.Infrastructure.Auth;
using Quorum.App.Infrastructure.Logging;
using Quorum.App.Infrastructure.Persistence;
using Quorum.App.Infrastructure.Time;
using Quorum.App.Infrastructure.Web;

var builder = WebApplication.CreateBuilder(args);

builder.AddQuorumTime();
builder.AddQuorumPersistence();
builder.AddQuorumAuth();
builder.AddQuorumLogging();
builder.AddQuorumTelemetry();
builder.AddQuorumWeb();
builder.AddCatalogPublish();

var app = builder.Build();

if (app.Environment.IsDevelopment())
    app.UseDeveloperExceptionPage();

app.UseQuorumAuth();
app.UseQuorumWeb();
app.MigrateQuorumDatabase();
app.RunCatalogCommandIfPresent();
app.RunActivityCommandIfPresent();
app.Run();

public partial class Program;
