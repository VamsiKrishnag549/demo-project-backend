using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("Default")));

builder.Services.AddCors(options =>
    options.AddPolicy("react", policy =>
        policy.WithOrigins("http://localhost:5173")
              .AllowAnyHeader()
              .AllowAnyMethod()));

var app = builder.Build();
app.UseCors("react");

// ---- Create database + table, and seed once ----
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();   // fine for learning; use migrations in real projects

    if (!db.Users.Any())
    {
        db.Database.ExecuteSqlRaw(@"
            ;WITH N AS (
              SELECT TOP (100000) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS i
              FROM sys.all_objects a CROSS JOIN sys.all_objects b
            )
            INSERT INTO Users (Name, Email)
            SELECT
              CONCAT(
                CHOOSE((i - 1) % 10 + 1, 'Ravi','Anita','Kiran','Sneha','Arjun','Priya','Vikram','Meera','Rahul','Divya'),
                ' ',
                CHOOSE(((i - 1) / 10) % 10 + 1, 'Sharma','Reddy','Nair','Gupta','Iyer','Rao','Patel','Singh','Das','Kumar')
              ),
              CONCAT('user', i, '@example.com')
            FROM N
            ORDER BY i;");
    }
}

// ---- GET /api/users?cursor=0&limit=50&search=ravi ----
app.MapGet("/api/users", async (AppDbContext db, int cursor = 0, int limit = 50, string? search = null) =>
{
    limit = Math.Clamp(limit, 1, 100);
    cursor = Math.Max(cursor, 0);

    var query = db.Users.AsNoTracking();

    if (!string.IsNullOrWhiteSpace(search))
    {
        var s = search.Trim();
        query = query.Where(u => u.Name.Contains(s) || u.Email.Contains(s));
    }

    var total = await query.CountAsync();
    var items = await query.OrderBy(u => u.Id).Skip(cursor).Take(limit).ToListAsync();
    var next = cursor + limit;

    return Results.Ok(new
    {
        items,
        total,
        nextCursor = next < total ? (int?)next : null
    });
});

app.Run();

// ---- Entity + DbContext ----
public class User
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
}

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
    public DbSet<User> Users => Set<User>();
}