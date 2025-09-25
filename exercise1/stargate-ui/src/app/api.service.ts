import { Injectable } from '@angular/core';
import { HttpClient, HttpErrorResponse, HttpHeaders } from '@angular/common/http';
import { catchError, throwError } from 'rxjs';

@Injectable({ providedIn: 'root' })
export class ApiService {
  private json = new HttpHeaders({ 'Content-Type': 'application/json' });
  constructor(private http: HttpClient) {}

  // People
  getPeople() { return this.http.get<any>('/Person'); }
  getPersonByName(name: string) { return this.http.get<any>(`/Person/${encodeURIComponent(name)}`); }
  // POST /Person expects a JSON string literal body
  createOrUpsertPersonByName(name: string) {
    return this.http.post<any>('/Person', JSON.stringify(name), { headers: this.json });
  }
  updatePersonByName(currentName: string, newName: string) {
    return this.http.put<any>('/Person/by-name', { currentName, newName }, { headers: this.json });
  }

  // Astronaut Duty
  getDutiesByName(name: string) { return this.http.get<any>(`/AstronautDuty/${encodeURIComponent(name)}`); }
  createDuty(duty: { personId: number; name: string; rank: string; dutyTitle: string; dutyStartDate: string; dutyEndDate?: string | null; }) {
    return this.http.post<any>('/AstronautDuty', duty, { headers: this.json });
  }

  createPerson(name: string) {
  return this.http.post<any>('Person', JSON.stringify(name), {
    headers: { 'Content-Type': 'application/json' }
  }).pipe(
    catchError((err: HttpErrorResponse) => {
      if (err.status === 409) {
        return throwError(() => ({ duplicate: true, message: err.error?.message ?? 'That name already exists.' }));
      }
      return throwError(() => err);
    })
  );
}
}
