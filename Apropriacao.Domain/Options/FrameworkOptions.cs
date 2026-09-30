namespace Apropriacao.Domain.Options
{
    public class FrameworkOptions
    {
        public const string SectionName = "FrameworkPortal";

        public string UrlApropriacaoNovo { get; set; } = "https://app.frwkapp.com.br/apropriacao/novo";
        public string StorageStatePath { get; set; } = "auth.json";
        public int TimeoutSegundos { get; set; } = 30;
        public int TempoLoginManualSegundos { get; set; } = 180;
    }
}