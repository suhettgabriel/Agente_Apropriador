import { Routes } from '@angular/router';
import { ApropriacaoFormComponent } from './apropriacao-form/apropriacao-form.component';

export const routes: Routes = [
	{ path: '', component: ApropriacaoFormComponent },
	{ path: 'apropriacao', component: ApropriacaoFormComponent },
	{ path: '**', redirectTo: '' }
];
