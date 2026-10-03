using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using ReadMeApp.Data;
using ReadMeApp.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
});

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Auth/Login";
        options.LogoutPath = "/Auth/Logout";
        options.ExpireTimeSpan = TimeSpan.FromDays(14);
        options.SlidingExpiration = true;
        options.Cookie.Name = "Readme_Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    });

var provider = builder.Configuration["DatabaseProvider"] ?? "Sqlite";
var sqlServerConn = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? Environment.GetEnvironmentVariable("SQLCONNSTR_DefaultConnection")
    ?? Environment.GetEnvironmentVariable("SQLAZURECONNSTR_DefaultConnection")
    ?? Environment.GetEnvironmentVariable("CUSTOMCONNSTR_DefaultConnection");
var sqliteConn = builder.Configuration.GetConnectionString("SqliteConnection") ?? "Data Source=readme.db";

builder.Services.AddDbContext<ReadmeDbContext>(options =>
{
    var isSqlServer = provider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase) &&
                      !string.IsNullOrWhiteSpace(sqlServerConn) &&
                      (builder.Environment.IsDevelopment() || !sqlServerConn.Contains("(localdb)", StringComparison.OrdinalIgnoreCase));

    if (isSqlServer)
    {
        options.UseSqlServer(sqlServerConn, sqlOptions =>
        {
            sqlOptions.EnableRetryOnFailure(
                maxRetryCount: 6,
                maxRetryDelay: TimeSpan.FromSeconds(10),
                errorNumbersToAdd: null);
            sqlOptions.CommandTimeout(60);
        });
    }
    else
    {
        options.UseSqlite(sqliteConn);
    }
});

builder.Services.AddHttpClient<IBookSearchService, KakaoBookSearchService>();
builder.Services.AddHttpClient<IKakaoAuthService, KakaoAuthService>();
builder.Services.AddScoped<IReadmeExportService, ReadmeExportService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();
    try
    {
        var context = services.GetRequiredService<ReadmeDbContext>();
        context.Database.EnsureCreated();

        if (context.Database.IsSqlServer())
        {
            context.Database.ExecuteSqlRaw("""
                IF EXISTS (
                    SELECT 1
                    FROM sys.columns c
                    INNER JOIN sys.tables t ON c.object_id = t.object_id
                    WHERE t.name = 'UserBooks'
                      AND c.name = 'Content'
                      AND c.max_length <> -1
                )
                BEGIN
                    ALTER TABLE [UserBooks] ALTER COLUMN [Content] nvarchar(max) NULL;
                END

                IF OBJECT_ID(N'[ReviewComments]', N'U') IS NULL
                BEGIN
                    CREATE TABLE [ReviewComments] (
                        [Id] int IDENTITY(1,1) NOT NULL,
                        [UserBookId] int NOT NULL,
                        [AuthorName] nvarchar(50) NOT NULL,
                        [Content] nvarchar(1000) NOT NULL,
                        [CreatedAt] datetime2 NOT NULL,
                        CONSTRAINT [PK_ReviewComments] PRIMARY KEY ([Id]),
                        CONSTRAINT [FK_ReviewComments_UserBooks_UserBookId]
                            FOREIGN KEY ([UserBookId]) REFERENCES [UserBooks]([Id]) ON DELETE CASCADE
                    );
                    CREATE INDEX [IX_ReviewComments_UserBookId]
                        ON [ReviewComments] ([UserBookId]);
                END
                """);
        }
        else if (context.Database.IsSqlite())
        {
            context.Database.ExecuteSqlRaw("""
                CREATE TABLE IF NOT EXISTS "ReviewComments" (
                    "Id" INTEGER NOT NULL CONSTRAINT "PK_ReviewComments" PRIMARY KEY AUTOINCREMENT,
                    "UserBookId" INTEGER NOT NULL,
                    "AuthorName" TEXT NOT NULL,
                    "Content" TEXT NOT NULL,
                    "CreatedAt" TEXT NOT NULL,
                    CONSTRAINT "FK_ReviewComments_UserBooks_UserBookId"
                        FOREIGN KEY ("UserBookId") REFERENCES "UserBooks" ("Id") ON DELETE CASCADE
                );
                CREATE INDEX IF NOT EXISTS "IX_ReviewComments_UserBookId"
                    ON "ReviewComments" ("UserBookId");
                """);
        }

        logger.LogInformation("데이터베이스 연결 및 초기화 확인 완료.");
    }
    catch (SqlException ex) { logger.LogWarning("MS SQL Server 연결 실패({Message}).", ex.Message); }
    catch (Exception ex) { logger.LogError(ex, "데이터베이스 초기화 중 오류 발생."); }
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(name: "default", pattern: "{controller=Home}/{action=Index}/{id?}");
app.Run();
