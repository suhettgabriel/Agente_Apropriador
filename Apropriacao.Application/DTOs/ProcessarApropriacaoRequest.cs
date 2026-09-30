namespace Apropriacao.Application.DTOs
{
    /// <summary>
    /// DTO para requisição de processamento de apropriação de horas.
    /// Contém os dados enviados via multipart/form-data.
    /// </summary>
    public class ProcessarApropriacaoRequest
    {
        /// <summary>
        /// Stream ou bytes do arquivo Excel com atividades
        /// </summary>
        public byte[] ArquivoPlanilhaBytes { get; set; } = Array.Empty<byte>();
        
        /// <summary>
        /// Nome do arquivo Excel original
        /// </summary>
        public string NomeArquivoPlanilha { get; set; } = string.Empty;
        
        /// <summary>
        /// Mês de referência para filtrar as atividades (ex: "Julho/2026")
        /// </summary>
        public string MesReferencia { get; set; } = string.Empty;
    }
}
