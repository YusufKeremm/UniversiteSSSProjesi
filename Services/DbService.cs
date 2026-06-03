using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using UniversiteSss.Models;

namespace UniversiteSss.Services;

public class DbService
{
    private readonly string _dbPath;
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };

    public DbService(IWebHostEnvironment env)
    {
        _dbPath = Path.Combine(env.ContentRootPath, "Data", "db.json");
        EnsureDb();
    }

    public DbModel Read()
    {
        lock (this)
        {
            EnsureDb();
            var raw = File.ReadAllText(_dbPath);
            return JsonSerializer.Deserialize<DbModel>(raw, _jsonOptions) ?? new DbModel();
        }
    }

    public void Write(DbModel db)
    {
        lock (this)
        {
            var json = JsonSerializer.Serialize(db, _jsonOptions);
            File.WriteAllText(_dbPath, json);
        }
    }

    public string HashPassword(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        using var derive = new Rfc2898DeriveBytes(password, salt, 100_000, HashAlgorithmName.SHA256);
        var key = derive.GetBytes(32);
        return $"{Convert.ToBase64String(salt)}.{Convert.ToBase64String(key)}";
    }

    public bool VerifyPassword(string password, string hash)
    {
        var parts = hash.Split('.');
        if (parts.Length != 2) return false;
        var salt = Convert.FromBase64String(parts[0]);
        var expected = Convert.FromBase64String(parts[1]);
        using var derive = new Rfc2898DeriveBytes(password, salt, 100_000, HashAlgorithmName.SHA256);
        var actual = derive.GetBytes(32);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }

    private void EnsureDb()
    {
        var dir = Path.GetDirectoryName(_dbPath)!;
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
        if (!File.Exists(_dbPath))
        {
            var json = JsonSerializer.Serialize(new DbModel(), _jsonOptions);
            File.WriteAllText(_dbPath, json, Encoding.UTF8);
        }
    }
}
