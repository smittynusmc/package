import { bootstrapApplication } from '@angular/platform-browser';
import { App } from './app/app';
import { provideHttpClient } from '@angular/common/http';
import { importProvidersFrom } from '@angular/core';
import { ReactiveFormsModule } from '@angular/forms';

bootstrapApplication(App, {
  providers: [
    provideHttpClient(),
    importProvidersFrom(ReactiveFormsModule),
  ],
}).catch(err => console.error(err));
