using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using UniversiteSss.Models;
using UniversiteSss.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);
var jwtKey = builder.Configuration["Jwt:Key"] ?? "dev-secret-change-this-very-long";
var issuer = "universite-sss";
var audience = "universite-sss-users";

builder.Services.AddSingleton<DbService>();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ValidIssuer = issuer,
            ValidAudience = audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });
builder.Services.AddAuthorization();

var app = builder.Build();
var dbService = app.Services.GetRequiredService<DbService>();

SeedIfNeeded(dbService);
MigrateLegacyAdmin(dbService);

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();

app.MapPost("/api/auth/register", (RegisterDto body) =>
{
    if (string.IsNullOrWhiteSpace(body.FullName) || string.IsNullOrWhiteSpace(body.Email) || string.IsNullOrWhiteSpace(body.Password))
        return Results.BadRequest(new { message = "Ad soyad, e-posta ve şifre zorunludur" });

    var db = dbService.Read();
    var email = body.Email.Trim().ToLowerInvariant();
    if (email == "danisman@universite.edu.tr")
        return Results.BadRequest(new { message = "Bu e-posta kullanılamaz. Admin girişi için admin@universite.edu.tr adresini kullanın." });
    if (db.Users.Any(x => x.Email == email))
        return Results.Conflict(new { message = "Bu e-posta zaten kayıtlı" });

    var user = new User
    {
        Id = $"usr_{Guid.NewGuid()}",
        FullName = body.FullName.Trim(),
        Email = email,
        PasswordHash = dbService.HashPassword(body.Password),
        Role = "student",
        CreatedAt = DateTime.UtcNow
    };
    db.Users.Add(user);
    dbService.Write(db);
    return Results.Created("/api/me", new { token = CreateToken(user), user = SafeUser(user) });
});

app.MapPost("/api/auth/login", (LoginDto body) =>
{
    var db = dbService.Read();
    var email = (body.Email ?? "").Trim().ToLowerInvariant();
    var user = db.Users.FirstOrDefault(x => x.Email == email);
    if (user is null || !dbService.VerifyPassword(body.Password ?? "", user.PasswordHash))
        return Results.Json(new { message = "E-posta veya şifre hatalı" }, statusCode: StatusCodes.Status401Unauthorized);

    return Results.Ok(new { token = CreateToken(user), user = SafeUser(user) });
});

app.MapGet("/api/me", (ClaimsPrincipal principal) =>
{
    var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
    if (string.IsNullOrEmpty(userId)) return Results.Unauthorized();
    var db = dbService.Read();
    var user = db.Users.FirstOrDefault(x => x.Id == userId);
    return user is null ? Results.NotFound() : Results.Ok(new { user = SafeUser(user) });
}).RequireAuthorization();

app.MapGet("/api/faqs", (string? q, string? topic) =>
{
    var db = dbService.Read();
    var query = (q ?? "").Trim().ToLowerInvariant();
    var topicQuery = (topic ?? "").Trim().ToLowerInvariant();
    var items = db.Faqs.Where(f =>
    {
        var matchQ = string.IsNullOrEmpty(query) ||
                     f.Question.ToLowerInvariant().Contains(query) ||
                     f.Answer.ToLowerInvariant().Contains(query) ||
                     f.Topic.ToLowerInvariant().Contains(query);
        var matchTopic = string.IsNullOrEmpty(topicQuery) || f.Topic.ToLowerInvariant().Contains(topicQuery);
        return matchQ && matchTopic;
    });
    return Results.Ok(new { items });
});

app.MapGet("/api/faqs/{id}/history", (string id) =>
{
    var db = dbService.Read();
    var faq = db.Faqs.FirstOrDefault(x => x.Id == id);
    return faq is null ? Results.NotFound(new { message = "SSS kaydı bulunamadı" }) : Results.Ok(new { history = faq.History });
});

app.MapPost("/api/faqs", (ClaimsPrincipal principal, FaqUpsertDto body) =>
{
    if (string.IsNullOrWhiteSpace(body.Question) || string.IsNullOrWhiteSpace(body.Answer) || string.IsNullOrWhiteSpace(body.Topic))
        return Results.BadRequest(new { message = "Soru, cevap ve konu zorunludur" });

    var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier)!;
    var now = DateTime.UtcNow;
    var faq = new Faq
    {
        Id = $"faq_{Guid.NewGuid()}",
        Question = body.Question.Trim(),
        Answer = body.Answer.Trim(),
        Topic = body.Topic.Trim(),
        CreatedBy = userId,
        CreatedAt = now,
        UpdatedAt = now,
        History =
        [
            new FaqHistory
            {
                Question = body.Question.Trim(),
                Answer = body.Answer.Trim(),
                Topic = body.Topic.Trim(),
                UpdatedBy = userId,
                UpdatedAt = now
            }
        ]
    };

    var db = dbService.Read();
    db.Faqs.Insert(0, faq);
    dbService.Write(db);
    return Results.Created($"/api/faqs/{faq.Id}", new { item = faq });
}).RequireAuthorization(policy => policy.RequireRole("admin", "staff"));

app.MapPut("/api/faqs/{id}", (ClaimsPrincipal principal, string id, FaqUpsertDto body) =>
{
    var db = dbService.Read();
    var faq = db.Faqs.FirstOrDefault(x => x.Id == id);
    if (faq is null) return Results.NotFound(new { message = "SSS kaydı bulunamadı" });

    var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier)!;
    faq.Question = string.IsNullOrWhiteSpace(body.Question) ? faq.Question : body.Question.Trim();
    faq.Answer = string.IsNullOrWhiteSpace(body.Answer) ? faq.Answer : body.Answer.Trim();
    faq.Topic = string.IsNullOrWhiteSpace(body.Topic) ? faq.Topic : body.Topic.Trim();
    faq.UpdatedAt = DateTime.UtcNow;
    faq.History.Add(new FaqHistory
    {
        Question = faq.Question,
        Answer = faq.Answer,
        Topic = faq.Topic,
        UpdatedBy = userId,
        UpdatedAt = faq.UpdatedAt
    });
    dbService.Write(db);
    return Results.Ok(new { item = faq });
}).RequireAuthorization(policy => policy.RequireRole("admin", "staff"));

app.MapDelete("/api/faqs/{id}", (string id) =>
{
    var db = dbService.Read();
    var target = db.Faqs.FirstOrDefault(x => x.Id == id);
    if (target is null) return Results.NotFound(new { message = "SSS kaydı bulunamadı" });
    db.Faqs.Remove(target);
    dbService.Write(db);
    return Results.Ok(new { message = "Kayıt silindi" });
}).RequireAuthorization(policy => policy.RequireRole("admin", "staff"));

app.MapPost("/api/requests", (ClaimsPrincipal principal, CreateRequestDto body) =>
{
    if (string.IsNullOrWhiteSpace(body.Question) || string.IsNullOrWhiteSpace(body.Topic))
        return Results.BadRequest(new { message = "Soru ve konu zorunludur" });

    var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier)!;
    var now = DateTime.UtcNow;
    var item = new QuestionRequest
    {
        Id = $"req_{Guid.NewGuid()}",
        Question = body.Question.Trim(),
        Details = (body.Details ?? "").Trim(),
        Topic = body.Topic.Trim(),
        RequestedBy = userId,
        Status = "pending",
        CreatedAt = now,
        UpdatedAt = now,
        History =
        [
            new RequestHistory { Action = "created", By = userId, At = now, Note = "Soru talebi oluşturuldu" }
        ]
    };

    var db = dbService.Read();
    db.QuestionRequests.Insert(0, item);
    dbService.Write(db);
    return Results.Created($"/api/requests/{item.Id}", new { item });
}).RequireAuthorization();

app.MapGet("/api/questions/history", (ClaimsPrincipal principal) =>
{
    var db = dbService.Read();
    var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier)!;
    var role = principal.FindFirstValue(ClaimTypes.Role);
    var items = IsAdminRole(role) ? db.QuestionRequests : db.QuestionRequests.Where(x => x.RequestedBy == userId);
    return Results.Ok(new { items });
}).RequireAuthorization();

app.MapPatch("/api/requests/{id}/moderate", (ClaimsPrincipal principal, string id, ModerateDto body) =>
{
    if (body.Decision is not ("approved" or "rejected"))
        return Results.BadRequest(new { message = "Karar onay (approved) veya red (rejected) olmalıdır" });

    var db = dbService.Read();
    var reqItem = db.QuestionRequests.FirstOrDefault(x => x.Id == id);
    if (reqItem is null) return Results.NotFound(new { message = "Talep bulunamadı" });
    if (reqItem.Status != "pending") return Results.BadRequest(new { message = "Bu talep daha önce işlenmiş" });

    var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier)!;
    var now = DateTime.UtcNow;
    reqItem.Status = body.Decision;
    reqItem.ReviewNote = body.ReviewNote ?? "";
    reqItem.ModeratedBy = userId;
    reqItem.UpdatedAt = now;

    if (body.Decision == "approved")
    {
        if (string.IsNullOrWhiteSpace(body.Answer)) return Results.BadRequest(new { message = "Onaylanan talep için cevap zorunludur" });
        var faq = new Faq
        {
            Id = $"faq_{Guid.NewGuid()}",
            Question = reqItem.Question,
            Answer = body.Answer.Trim(),
            Topic = reqItem.Topic,
            CreatedBy = userId,
            CreatedAt = now,
            UpdatedAt = now,
            History =
            [
                new FaqHistory
                {
                    Question = reqItem.Question,
                    Answer = body.Answer.Trim(),
                    Topic = reqItem.Topic,
                    UpdatedBy = userId,
                    UpdatedAt = now
                }
            ]
        };
        db.Faqs.Insert(0, faq);
        reqItem.PublishedFaqId = faq.Id;
    }

    reqItem.History.Add(new RequestHistory { Action = body.Decision, By = userId, At = now, Note = reqItem.ReviewNote });
    dbService.Write(db);
    return Results.Ok(new { item = reqItem });
}).RequireAuthorization(policy => policy.RequireRole("admin", "staff"));

app.MapFallbackToFile("index.html");
app.Run();

string CreateToken(User user)
{
    var claims = new[]
    {
        new Claim(ClaimTypes.NameIdentifier, user.Id),
        new Claim(ClaimTypes.Email, user.Email),
        new Claim(ClaimTypes.Name, user.FullName),
        new Claim(ClaimTypes.Role, user.Role)
    };
    var credentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)), SecurityAlgorithms.HmacSha256);
    var token = new JwtSecurityToken(issuer, audience, claims, expires: DateTime.UtcNow.AddDays(7), signingCredentials: credentials);
    return new JwtSecurityTokenHandler().WriteToken(token);
}

object SafeUser(User user) => new
{
    user.Id,
    user.FullName,
    user.Email,
    user.Role,
    user.CreatedAt
};

void SeedIfNeeded(DbService db)
{
    var data = db.Read();
    if (data.Users.Count > 0) return;
    var now = DateTime.UtcNow;
    var admin = new User
    {
        Id = $"usr_{Guid.NewGuid()}",
        FullName = "Sistem Admin",
        Email = "admin@universite.edu.tr",
        PasswordHash = db.HashPassword("123456"),
        Role = "admin",
        CreatedAt = now
    };

    data.Users.Add(admin);
    data.Faqs.Add(new Faq
    {
        Id = $"faq_{Guid.NewGuid()}",
        Question = "Üniversite kaydı nasıl yapılır?",
        Answer = "Kayıt işlemleri öğrenci işleri portalından e-Devlet doğrulama adımlarıyla tamamlanır.",
        Topic = "Kayıt",
        CreatedBy = admin.Id,
        CreatedAt = now,
        UpdatedAt = now,
        History =
        [
            new FaqHistory
            {
                Question = "Üniversite kaydı nasıl yapılır?",
                Answer = "Kayıt işlemleri öğrenci işleri portalından e-Devlet doğrulama adımlarıyla tamamlanır.",
                Topic = "Kayıt",
                UpdatedBy = admin.Id,
                UpdatedAt = now
            }
        ]
    });
    db.Write(data);
}

void MigrateLegacyAdmin(DbService db)
{
    var data = db.Read();
    var changed = false;
    foreach (var user in data.Users)
    {
        if (user.Role == "staff")
        {
            user.Role = "admin";
            changed = true;
        }
        if (user.Email.Equals("danisman@universite.edu.tr", StringComparison.OrdinalIgnoreCase))
        {
            user.Email = "admin@universite.edu.tr";
            user.FullName = "Sistem Admin";
            changed = true;
        }
    }
    if (changed) db.Write(data);
}

static bool IsAdminRole(string? role) => role is "admin" or "staff";

public record RegisterDto(string FullName, string Email, string Password);
public record LoginDto(string Email, string Password);
public record FaqUpsertDto(string Question, string Answer, string Topic);
public record CreateRequestDto(string Question, string Details, string Topic);
public record ModerateDto(string Decision, string Answer, string ReviewNote);
