namespace HayataAtilmaFormu.Models
{
    public class NotificationStats
    {
        public int TotalSent { get; set; }
        public int EmailSent { get; set; }
        public int SmsSent { get; set; }
        public int SuccessfulSent { get; set; }
        public int FailedSent { get; set; }
        public double EmailSuccessRate { get; set; }
        public double SmsSuccessRate { get; set; }
    }
}