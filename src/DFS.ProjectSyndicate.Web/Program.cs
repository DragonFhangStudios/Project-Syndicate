using DFS.ProjectSyndicate.Gameplay;
using DFS.ProjectSyndicate.Jobs;
using DFS.ProjectSyndicate.Persistence;
using DFS.ProjectSyndicate.Web.Components;
using DFS.ProjectSyndicate.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Saves live outside wwwroot so they are never served to the browser.
string saveDirectory = Path.GetFullPath(Path.Combine(
    builder.Environment.ContentRootPath,
    builder.Configuration["Syndicate:SaveDirectory"] ?? "App_Data/saves"));

builder.Services.AddSingleton<IPlayerRepository>(new JsonFilePlayerRepository(saveDirectory));
builder.Services.AddSingleton<IJobCatalog>(new JobCatalog(JobCatalog.DefaultPath));
builder.Services.AddSingleton<GameService>();
builder.Services.AddScoped<PlayerState>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
