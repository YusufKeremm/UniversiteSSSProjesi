# Üniversite SSS Platformu

Web tabanlı sıkça sorulan sorular uygulaması. Öğrenciler ve adaylar kayıt olup SSS arayabilir; admin kullanıcılar içerik yönetimi ve soru taleplerini onaylayabilir.

## Gereksinimler

- .NET 8 SDK

## Çalıştırma

```bash
cd UniversiteSssPlatform
dotnet --version
dotnet restore
dotnet run
```

Uygulama açıldıktan sonra terminalde görünen adresi tarayıcıda açın (genellikle `http://localhost:5000`).

## Proje yapısı

- `Program.cs` — API uçları ve kimlik doğrulama
- `Models/` — veri modelleri
- `Services/` — veri erişim katmanı
- `Data/db.json` — kalıcı veri dosyası
- `wwwroot/` — arayüz dosyaları

## Test hesabı (admin)

- E-posta: `admin@universite.edu.tr`
- Şifre: `123456`
