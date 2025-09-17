namespace HayataAtilmaFormu.Models.ViewModels
{
    public class IstatistikViewModel
    {
        public int ToplamBasvuru { get; set; }
        public int BekleyenBasvuru { get; set; }
        public int OnaylananBasvuru { get; set; }
        public int ReddettigimBasvuru { get; set; }
        public double OrtalamaSure { get; set; }
        public double OnayOrani { get; set; }
        public double RedOrani { get; set; }
        public int BuAyToplam { get; set; }
        public int BuAyOnaylanan { get; set; }
        public int BuAyReddedilen { get; set; }
        public int BuAyIslenen { get; set; }
        public List<BasvuruTurIstatistik> BasvuruTurleri { get; set; } = new List<BasvuruTurIstatistik>();
        public List<SonIslem> SonIslemler { get; set; } = new List<SonIslem>();
    }

    public class BasvuruTurIstatistik
    {
        public string Tur { get; set; } = string.Empty;
        public int Adet { get; set; }
    }

    public class SonIslem
    {
        public string OgrenciAdi { get; set; } = string.Empty;
        public string OgrenciNo { get; set; } = string.Empty;
        public string BolumAdi { get; set; } = string.Empty;
        public string BasvuruTuru { get; set; } = string.Empty;
        public string Durum { get; set; } = string.Empty;
        public DateTime IslemTarihi { get; set; }
    }
}