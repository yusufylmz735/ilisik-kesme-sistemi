# 🏛️ Dijital İlişik Kesme Sistemi

Bu proje, öğrenciler için dijital **ilişik kesme işlemlerini** yöneten bir web uygulamasıdır.  
ASP.NET Core 8.0 ile geliştirilmiş olup **fakülte bazlı onay zinciri, PDF çıktısı ve çoklu dil desteği** sunmaktadır.

---

## 🌟 Özellikler

- **Kullanıcı Tipleri**:
  - **Öğrenci**: Sadece bir dilekçe oluşturabilir.
  - **Yetkili**: Fakülte bazlı dilekçeleri onaylar veya reddeder.
  - **Süperadmin**: Tüm süreçleri yönetir, kullanıcı ve panel kontrollerini yapar.

- **Fakülte Bazlı Panel**: Öğrenciler okul numarasıyla giriş yapar ve kendi fakültelerine özel panel açılır.

- **Onay Zinciri**: Dilekçeler yetkili sırasına göre ilerler.

- **PDF Çıktısı**: Onaylanan dilekçeler PDF olarak alınabilir.

- **Çoklu Dil Desteği**: Sistem birden fazla dilde kullanılabilir.

- **Dinamik Veri Modeli**: Merkezi yönetim ve fakülteye özel tasarımlar desteklenir.

---

## 🛠️ Teknolojiler

- ASP.NET Core 8.0  
- Entity Framework Core  
- SQL Server  
- Bootstrap / Tailwind (UI için)  
- PDFSharp veya iTextSharp (PDF oluşturmak için)  

---

## 🚀 Kurulum

1. Depoyu klonlayın:
```bash
git clone https://github.com/yusufylmz735/IlisikKesmeSistemi.git
