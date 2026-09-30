namespace Apropriacao.Domain.ValueObjects
{
    /// <summary>
    /// Value Object que encapsula os dados extraídos de um arquivo (PDF ou Excel)
    /// </summary>
    public class DadosExtraidosArquivo
    {
        public string Conteudo { get; private set; }
        public string TipoArquivo { get; private set; }
        public int QuantidadeLinhas { get; private set; }
        public DateTime DataExtracao { get; private set; }
        
        public DadosExtraidosArquivo(string conteudo, string tipoArquivo, int quantidadeLinhas)
        {
            if (string.IsNullOrWhiteSpace(conteudo))
                throw new ArgumentException("Conteúdo não pode ser vazio.", nameof(conteudo));
            
            if (tipoArquivo != "PDF" && tipoArquivo != "Excel")
                throw new ArgumentException("Tipo deve ser 'PDF' ou 'Excel'.", nameof(tipoArquivo));
            
            if (quantidadeLinhas < 0)
                throw new ArgumentException("Quantidade de linhas não pode ser negativa.", nameof(quantidadeLinhas));
            
            Conteudo = conteudo;
            TipoArquivo = tipoArquivo;
            QuantidadeLinhas = quantidadeLinhas;
            DataExtracao = DateTime.UtcNow;
        }
        
        /// <summary>
        /// Trunca o conteúdo a um número máximo de caracteres para não sobrecarregar o LLM
        /// </summary>
        public string ObterConteudoTruncado(int maxCaracteres = 5000)
        {
            if (Conteudo.Length <= maxCaracteres)
                return Conteudo;
            
            return Conteudo.Substring(0, maxCaracteres) + "\n[... conteúdo truncado ...]";
        }
    }
}
