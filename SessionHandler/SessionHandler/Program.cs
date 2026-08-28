using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using SessionHandler.Data;
using SessionHandler.Exceptions;
using SessionHandler.Interfaces;
using SessionHandler.Repositories;
using SessionHandler.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    // Serialize enums (e.g. SessionEvent.Type) as their names, not integers.
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// Translate session domain exceptions into RFC 7807 responses.
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<SessionExceptionHandler>();

// Session persistence (EF Core + SQLite).
builder.Services.AddDbContext<SessionDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("SessionDb")));
builder.Services.AddScoped<ISessionRepository, SessionRepository>();
builder.Services.AddScoped<ISessionService, SessionService>();
builder.Services.AddScoped<ISessionEventRepository, SessionEventRepository>();
builder.Services.AddScoped<ISessionEventService, SessionEventService>();

var app = builder.Build();

// Bring the database schema up to date on startup so a fresh checkout runs with
// no manual `dotnet ef database update` step. Fine for this single-instance app;
// a distributed deployment would run migrations as a separate step instead.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<SessionDbContext>();
    db.Database.Migrate();
}

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "SessionHandler v1");
        options.RoutePrefix = "api";
    });
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();