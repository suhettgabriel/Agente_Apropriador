namespace Apropriacao.Domain.ValueObjects
{
    /// <summary>
    /// Value Object que encapsula informações sobre arquivos temporários salvos.
    /// </summary>
    public class ArquivoTemporario
    {
        public string CaminhoCompleto { get; private set; }
        public string NomeOriginal { get; private set; }
        public string TipoArquivo { get; private set; } // "PDF" ou "Excel"
        public long TamanhoBytes { get; private set; }
        public DateTime DataCriacao { get; private set; }
        
        public ArquivoTemporario(string caminhoCompleto, string nomeOriginal, string tipoArquivo, long tamanhoBytes)
        {
            if (string.IsNullOrWhiteSpace(caminhoCompleto))
                throw new ArgumentException("Caminho completo não pode ser vazio.", nameof(caminhoCompleto));
            
            if (string.IsNullOrWhiteSpace(nomeOriginal))
                throw new ArgumentException("Nome original não pode ser vazio.", nameof(nomeOriginal));
            
            if (tipoArquivo != "PDF" && tipoArquivo != "Excel")
                throw new ArgumentException("Tipo de arquivo deve ser 'PDF' ou 'Excel'.", nameof(tipoArquivo));
            
            if (tamanhoBytes <= 0)
                throw new ArgumentException("Tamanho deve ser maior que zero.", nameof(tamanhoBytes));
            
            CaminhoCompleto = caminhoCompleto;
            NomeOriginal = nomeOriginal;
            TipoArquivo = tipoArquivo;
            TamanhoBytes = tamanhoBytes;
            DataCriacao = DateTime.UtcNow;
        }
        
        /// <summary>
        /// Verifica se o arquivo ainda existe no disco
        /// </summary>
        public bool Existe()
        {
            return File.Exists(CaminhoCompleto);
        }
    }
}
