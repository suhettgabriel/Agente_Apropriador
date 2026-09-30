namespace Apropriacao.Domain.Options
{
    public class TangerinoOptions
    {
        public const string SectionName = "TangerinoCredentials";

        public string CodigoEmpregador { get; set; } = string.Empty;
        public string Pin { get; set; } = string.Empty;
        public string UrlBase { get; set; } = "https://app.tangerino.com.br/Tangerino/";
        public string UrlLoginFuncionario { get; set; } = "https://app.tangerino.com.br/Tangerino/?wicket:interface=wicket-1:0:body:loginForm:loginFuncionario::ILinkListener::";
        public string UrlEspelhoPonto { get; set; } = "https://app.tangerino.com.br/Tangerino/pages/apropriacao-horas?funcionalidade=5";
        public string UrlApropriacaoNovo { get; set; } = "https://app.tangerino.com.br/Tangerino/";
        public int TimeoutSegundos { get; set; } = 30;
    }
}