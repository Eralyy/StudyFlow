using Microsoft.EntityFrameworkCore;
using StudyFlow.Web.Components;
using StudyFlow.Web.Data;
using StudyFlow.Web.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
string dataDirectory = Path.Combine(builder.Environment.ContentRootPath, "App_Data");
Directory.CreateDirectory(dataDirectory);
string connectionString = builder.Configuration.GetConnectionString("StudyFlow")
    ?? $"Data Source={Path.Combine(dataDirectory, "studyflow.db")}";
builder.Services.AddDbContextFactory<StudyFlowDbContext>(options => options.UseSqlite(connectionString));
builder.Services.AddScoped<TaskStore>();
builder.Services.AddScoped<AvailabilityStore>();
builder.Services.AddScoped<WorkspaceState>();

var app = builder.Build();
using (var scope = app.Services.CreateScope())
{
    var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<StudyFlowDbContext>>();
    await using var db = await factory.CreateDbContextAsync();
    await db.Database.MigrateAsync();
}
if (!app.Environment.IsDevelopment())
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
app.UseStaticFiles();
app.UseAntiforgery();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
app.Run();
