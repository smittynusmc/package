// src/app/app.ts
import { ChangeDetectorRef, Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { ApiService } from './api.service';
import { PeopleSelectComponent } from './Components/people-select.component';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule,
    PeopleSelectComponent],
  templateUrl: './app.html',
})
export class App {
  people: any[] = [];
  personResult: any = null;
  dutiesResult: any = null;
  message = '';

  findPersonForm: ReturnType<FormBuilder['group']>;
  addPersonForm: ReturnType<FormBuilder['group']>;
  updateByNameForm: ReturnType<FormBuilder['group']>;
  findDutiesForm: ReturnType<FormBuilder['group']>;
  addDutyForm: ReturnType<FormBuilder['group']>;

  constructor(private fb: FormBuilder, private api: ApiService, private cdr: ChangeDetectorRef) {
    this.findPersonForm = this.fb.group({ name: ['', Validators.required] });
    this.addPersonForm = this.fb.group({ name: ['', Validators.required] });
    this.updateByNameForm = this.fb.group({ currentName: ['', Validators.required], newName: ['', Validators.required] });
    this.findDutiesForm = this.fb.group({ name: ['', Validators.required], personId: [null], });
    this.addDutyForm = this.fb.group({
      personId: [1, Validators.required],
      rank: ['', Validators.required],
      dutyTitle: ['', Validators.required],
      dutyStartDate: ['', Validators.required],
      dutyEndDate: [''],
    });
  }

  loadPeople() {
    this.message = '';
    this.api.getPeople().subscribe({
      next: (res: any) => {
        // handle [], {data:[...]}, {people:[...]}, or anything similar
        const list = Array.isArray(res) ? res
          : Array.isArray(res?.data) ? res.data
            : Array.isArray(res?.people) ? res.people
              : [];
        this.people = list;
        this.cdr.markForCheck();
      },
      error: e => this.message = e.error?.message ?? e.message
    });
  }

  findPerson() {
    this.message = '';
    const n = this.findPersonForm.value.name?.trim(); if (!n) return;
    this.api.getPersonByName(n).subscribe({
      next: res => this.personResult = res.person ?? res,
      error: e => { this.personResult = null; this.message = e.error?.message ?? e.message; }
    });
  }
  addPerson() {
    this.message = '';
    const n = this.addPersonForm.value.name?.trim();
    if (!n) return;

    this.api.createOrUpsertPersonByName(n).subscribe({
      next: res => {
        this.message = res.message ?? 'Saved';
        this.addPersonForm.reset();
        this.loadPeople();
      },
      error: (e) => {
        if (e?.status === 409) {
          const c = this.addPersonForm.get('name');
          const msg = e?.error?.message || 'That name already exists.';

          // DO NOT call updateValueAndValidity after setErrors — it clears custom errors.
          c?.markAsTouched();
          const current = c?.errors ?? {};
          c?.setErrors({ ...current, duplicate: msg });

          // Optional: if you use OnPush, keep this
          this.cdr.markForCheck();

          console.log('ctrl.errors after setErrors:', c?.errors); // should now show { duplicate: '...' }
          return;
        }

        this.message = e?.error?.message ?? e?.message ?? 'Failed to save.';
      }


    });
  }


  ngOnInit(): void {
    this.loadPeople();
    this.setupFindDutiesAutoRun();
    // Clear the custom "duplicate" error as soon as the user edits the field
    this.addPersonForm.get('name')?.valueChanges.subscribe(() => {
      const ctrl = this.addPersonForm.get('name');
      if (ctrl?.errors?.['duplicate']) {
        const { duplicate, ...rest } = ctrl.errors!;
        ctrl.setErrors(Object.keys(rest).length ? rest : null);
      }
    });
  }

  private setupFindDutiesAutoRun() {
    const ctrl = this.findDutiesForm.get('personId');
    if (!ctrl) return;

    ctrl.valueChanges.subscribe((id) => {
      if (id == null) return;

      // Tolerant lookup (handles personId/id casing)
      const picked = this.people.find(p =>
        Number(p?.personId ?? p?.id ?? p?.personID ?? p?.PersonId) === Number(id)
      );

      const name = (picked?.name ?? picked?.fullName ?? picked?.personName ?? picked?.Name ?? '').trim();
      if (!name) return;

      // optionally keep the form’s name field in sync (safe, no loops)
      this.findDutiesForm.patchValue({ name }, { emitEvent: false });

      // 🔔 Call your existing method with the name
      this.findDuties(name);
    });
  }


  updateByName() {
    this.message = '';
    const { currentName, newName } = this.updateByNameForm.value;
    if (!currentName || !newName) return;
    this.api.updatePersonByName(currentName.trim(), newName.trim()).subscribe({
      next: res => { this.message = res.message ?? 'Updated'; this.loadPeople(); },
      error: e => this.message = e.error?.message ?? e.message
    });
  }

  findDuties(nameFromSelect?: string) {
    this.message = '';

    const name = (nameFromSelect ?? this.findDutiesForm.value.name ?? '').trim();
    if (!name) return;

    this.api.getDutiesByName(name).subscribe({
      next: res => this.dutiesResult = res,
      error: e => { this.dutiesResult = null; this.message = e.error?.message ?? e.message; }
    });
  }


  selectedPersonName: string | null = null;

  addDuty() {
    const v = this.addDutyForm.value;

    const picked = this.people.find(p => {
      const id = p?.personId ?? p?.id ?? p?.personID ?? p?.PersonId;
      return Number(id) === Number(v.personId);
    });

    const name = picked?.name ?? picked?.fullName ?? picked?.personName ?? picked?.Name ?? '';

    const payload = {
      personId: Number(v.personId),
      name: (name ?? '').trim(),                 // optional if backend uses personId
      rank: (v.rank ?? '').trim(),
      dutyTitle: (v.dutyTitle ?? '').trim(),
      dutyStartDate: v.dutyStartDate ? new Date(v.dutyStartDate).toISOString() : ''
    };

    this.api.createDuty(payload).subscribe({
      next: res => this.message = res.message ?? 'Created',
      error: e => this.message = e.error?.message ?? e.message
    });
  }
  

}
