import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, catchError, throwError } from 'rxjs';

export interface ApropriacaoResponse {
  sucesso: boolean;
  mensagem: string;
  detalhes: string;
  quantidadeLancamentosRealizados: number;
  dataProcessamento: string;
  erros: string[];
}

@Injectable({ providedIn: 'root' })
export class ApropriacaoService {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = 'http://localhost:5254/api/apropriacao/iniciar';

  iniciarAutomacao(arquivoPlanilha: File, mesReferencia: string, diasSelecionados: string): Observable<ApropriacaoResponse> {
    const formData = new FormData();
    formData.append('arquivoPlanilha', arquivoPlanilha, arquivoPlanilha.name);
    formData.append('mesReferencia', mesReferencia);
    formData.append('diasSelecionados', diasSelecionados);

    return this.http.post<ApropriacaoResponse>(this.apiUrl, formData).pipe(
      catchError((error: HttpErrorResponse) => throwError(() => error))
    );
  }
}
