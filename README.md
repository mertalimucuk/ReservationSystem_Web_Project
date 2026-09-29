# 📅 Reservation System Project

Bu proje, eğitmenlerin akademik dönemler boyunca müsait sınıflar için rezervasyon talebinde bulunabilmesini sağlayan web tabanlı bir sistemdir[cite: 22]. Çankaya Üniversitesi CENG 382 Web Geliştirme dersi kapsamında Mert Ali Mucuk tarafından geliştirilmiştir[cite: 22]. 

🔗 **Proje Tanıtım Videosu:** [YouTube Linki](https://youtu.be/WRQYS4bQueM)[cite: 22]

## 👥 Kullanıcı Rolleri
* **Yönetici (Admin):** Sistem kurulumu, akademik dönem yönetimi, eğitmen hesaplarının yönetilmesi, rezervasyon taleplerinin onaylanması/reddedilmesi, çakışmaların çözümü ve geri bildirimlerin izlenmesinden sorumludur[cite: 22].
* **Eğitmen (Instructor):** Müsait sınıfları görüntüleyebilir, dersleri için rezervasyon talebi gönderebilir, mevcut rezervasyonlarını değiştirebilir/iptal edebilir ve sınıflar hakkında geri bildirim verebilir[cite: 22].
* **Kullanıcı (User):** Eğitmen veya yönetici olmayan, sınıflar hakkında geri bildirim (feedback) verebilen ziyaretçi kullanıcılardır[cite: 22].

## ✨ Temel Özellikler
* **Rol Bazlı Erişim:** Yönetici ve Eğitmenler için şifre korumalı, işlevleri ayrılmış özel paneller[cite: 22].
* **Akademik Dönem Yönetimi:** Yöneticilerin rezervasyon yapılabilecek aktif dönemleri tanımlaması ve yönetmesi[cite: 22].
* **Gelişmiş Rezervasyon & Çakışma Kontrolü:** Eğitmenler tekil veya aktif dönem boyunca tekrarlayan rezervasyonlar yapabilir; sistem aynı sınıf ve saat için çakışan talepleri otomatik olarak tespit edip yöneticiye bildirir[cite: 22].
* **Resmi Tatil Entegrasyonu:** Google Calendar API kullanılarak talep edilen tarihlerin resmi tatillere denk gelip gelmediği kontrol edilir ve uyarısı verilir[cite: 22].
* **Geri Bildirim Sistemi:** Kullanıcıların belirli sınıflar ve ders oturumları için 5 yıldızlı değerlendirme ve yorum yapabilmesini sağlayan entegre sistem[cite: 22].
* **Dinamik Takvim Görünümü:** Tüm rezervasyonları ve durumlarını gösteren, haftalık ve aylık görünüme sahip etkileşimli takvim (Admin ve Eğitmenler için)[cite: 22].
* **Otomatik E-posta Bildirimleri:** Eğitmenlere rezervasyon taleplerinin durumu (onay, ret, tatil uyarıları) hakkında otomatik bilgilendirme e-postaları gönderilir[cite: 22].
* **Güvenlik ve Loglama:** Parolalar hash ve salt yöntemleriyle şifrelenir; sisteme girişler, rezervasyon işlemleri ve sistem hataları denetim için veritabanına kaydedilir[cite: 22].
* **Karanlık/Aydınlık Mod (Dark/Light Mode):** Kullanıcı arayüzü teması tercihe göre anlık olarak değiştirilebilir (Bonus Özellik)[cite: 22].
* **Modern Arayüz:** Bootstrap kullanılarak akademik ortama uygun, profesyonel, hızlı çalışan (modallar kullanılarak sayfa yenilemesi en aza indirilmiş) duyarlı bir tasarım[cite: 22].
