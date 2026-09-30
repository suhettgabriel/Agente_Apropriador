import { CommonModule } from '@angular/common';
import { Component, inject } from '@angular/core';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { ApropriacaoResponse, ApropriacaoService } from '../apropriacao.service';

@Component({
  selector: 'app-apropriacao-form',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './apropriacao-form.component.html',
  styleUrl: './apropriacao-form.component.css'
})
export class ApropriacaoFormComponent {
  private readonly fb = inject(FormBuilder);
  private readonly apropriacaoService = inject(ApropriacaoService);
  private readonly maxFileSize = 50 * 1024 * 1024;

  readonly meses = [
    { valor: 1, nome: 'Janeiro' },
    { valor: 2, nome: 'Fevereiro' },
    { valor: 3, nome: 'Março' },
    { valor: 4, nome: 'Abril' },
    { valor: 5, nome: 'Maio' },
    { valor: 6, nome: 'Junho' },
    { valor: 7, nome: 'Julho' },
    { valor: 8, nome: 'Agosto' },
    { valor: 9, nome: 'Setembro' },
    { valor: 10, nome: 'Outubro' },
    { valor: 11, nome: 'Novembro' },
    { valor: 12, nome: 'Dezembro' }
  ];

  readonly anosDisponiveis = this.gerarAnosDisponiveis();

  form: FormGroup = this.fb.group({
    mes: [new Date().getMonth() + 1, Validators.required],
    ano: [new Date().getFullYear(), Validators.required]
  });

  arquivoPlanilha: File | null = null;
  diasSelecionados: string[] = [];
  isSubmitting = false;
  response: ApropriacaoResponse | null = null;
  responseError = '';

  get podeEnviar() {
    return this.form.valid && this.diasSelecionados.length > 0 && !!this.arquivoPlanilha && !this.isSubmitting;
  }

  get nomePlanilha() {
    return this.arquivoPlanilha?.name ?? 'Nenhum Excel selecionado';
  }

  get mesReferencia(): string {
    const mes = Number(this.form.value.mes);
    const ano = Number(this.form.value.ano);
    const nomeMes = new Intl.DateTimeFormat('pt-BR', { month: 'long' }).format(new Date(ano, mes - 1, 1));
    return `${nomeMes}/${ano}`;
  }

  get diasDoMes(): Array<number | null> {
    const mes = Number(this.form.value.mes);
    const ano = Number(this.form.value.ano);
    if (!mes || !ano) return [];
    const primeiroDia = new Date(ano, mes - 1, 1).getDay();
    const quantidadeDias = new Date(ano, mes, 0).getDate();
    return [
      ...Array.from({ length: primeiroDia }, () => null),
      ...Array.from({ length: quantidadeDias }, (_, indice) => indice + 1)
    ];
  }

  onFileChange(event: Event) {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0] ?? null;
    this.response = null;
    this.responseError = '';

    if (!file) {
      this.arquivoPlanilha = null;
      return;
    }

    const extensoesPermitidas = ['.xlsx', '.xls'];
    const fileName = file.name.toLowerCase();
    const extensaoValida = extensoesPermitidas.some(ext => fileName.endsWith(ext));

    if (!extensaoValida) {
      this.responseError = 'Envie uma planilha Excel válida em .xlsx ou .xls.';
      input.value = '';
      this.arquivoPlanilha = null;
      return;
    }

    if (file.size > this.maxFileSize) {
      this.responseError = 'O arquivo excede o limite de 50 MB.';
      input.value = '';
      this.arquivoPlanilha = null;
      return;
    }

    this.arquivoPlanilha = file;
  }

  alterarMesCalendario() {
    this.diasSelecionados = [];
    this.responseError = '';
  }

  alternarDia(dia: number | null) {
    if (dia === null) return;
    const mes = String(this.form.value.mes).padStart(2, '0');
    const ano = this.form.value.ano;
    const data = `${String(dia).padStart(2, '0')}/${mes}/${ano}`;
    this.diasSelecionados = this.diasSelecionados.includes(data)
      ? this.diasSelecionados.filter(item => item !== data)
      : [...this.diasSelecionados, data].sort((a, b) => this.compararDatas(a, b));
    this.responseError = '';
  }

  removerDia(dia: string) {
    this.diasSelecionados = this.diasSelecionados.filter(item => item !== dia);
  }

  submit() {
    this.response = null;
    this.responseError = '';

    if (!this.podeEnviar || !this.arquivoPlanilha) {
      this.form.markAllAsTouched();
      this.responseError = 'Informe o mês, os dias que deseja lançar e selecione a planilha de atividades.';
      return;
    }

    this.isSubmitting = true;

    this.apropriacaoService
      .iniciarAutomacao(this.arquivoPlanilha, this.mesReferencia, this.diasSelecionados.join(', '))
      .subscribe({
        next: (response) => {
          this.isSubmitting = false;
          this.response = response;
          this.responseError = '';
          const mesAtual = new Date().getMonth() + 1;
          const anoAtual = new Date().getFullYear();
          this.form.reset({ mes: mesAtual, ano: anoAtual });
          this.arquivoPlanilha = null;
          this.diasSelecionados = [];
        },
        error: (error: HttpErrorResponse) => {
          this.isSubmitting = false;
          const payload = error.error as Partial<ApropriacaoResponse> | undefined;
          this.response = payload?.mensagem ? payload as ApropriacaoResponse : null;
          this.responseError = payload?.mensagem ?? this.mapHttpError(error);
        }
      });
  }

  estaSelecionado(dia: number | null) {
    if (dia === null) return false;
    const mes = String(this.form.value.mes).padStart(2, '0');
    const ano = this.form.value.ano;
    return this.diasSelecionados.includes(`${String(dia).padStart(2, '0')}/${mes}/${ano}`);
  }

  private compararDatas(a: string, b: string) {
    const [diaA, mesA, anoA] = a.split('/').map(Number);
    const [diaB, mesB, anoB] = b.split('/').map(Number);
    return new Date(anoA, mesA - 1, diaA).getTime() - new Date(anoB, mesB - 1, diaB).getTime();
  }

  private gerarAnosDisponiveis() {
    const anoAtual = new Date().getFullYear();
    return [anoAtual - 1, anoAtual, anoAtual + 1];
  }

  private mapHttpError(error: HttpErrorResponse) {
    if (error.status === 0) {
      return 'Não foi possível conectar à API em http://localhost:5254.';
    }

    if (error.status === 400) {
      return 'A API recusou os dados enviados. Revise os arquivos e o mês de referência.';
    }

    if (error.status >= 500) {
      return 'A API encontrou um erro interno durante o processamento.';
    }

    return 'Erro ao processar a apropriação.';
  }
}
