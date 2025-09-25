// people-select.component.ts
import { ChangeDetectionStrategy, ChangeDetectorRef, Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ControlValueAccessor, NG_VALUE_ACCESSOR } from '@angular/forms';

@Component({
    selector: 'people-select',
    standalone: true,
    imports: [CommonModule],
    template: `
    <select [disabled]="disabled"
            [value]="value ?? ''"
            (change)="onSelect($event)">
      <option value="">{{ placeholder }}</option>

      <ng-container *ngIf="people?.length; else none">
        <option *ngFor="let p of people; trackBy: trackAny"
                [value]="getId(p)">
          {{ getName(p) }}
        </option>
      </ng-container>

      <ng-template #none>
        <option value="" disabled>No people found</option>
      </ng-template>
    </select>
  `,
    changeDetection: ChangeDetectionStrategy.OnPush,
    providers: [{ provide: NG_VALUE_ACCESSOR, useExisting: PeopleSelectComponent, multi: true }],
})
export class PeopleSelectComponent implements ControlValueAccessor {
    @Output() selectionChange = new EventEmitter<any>();
    @Input() placeholder = 'Select a person…';
    @Input() set people(val: any[] | null | undefined) {
        this._people = Array.isArray(val) ? val : [];
        this.cdr.markForCheck();
    }
    get people(): any[] { return this._people; }
    private _people: any[] = [];

    value: number | null = null;
    disabled = false;

    constructor(private cdr: ChangeDetectorRef) { }

    // tolerate different shapes: personId/id, name/fullName/etc.
    getId = (p: any): number | '' => {
        const id = p?.personId ?? p?.id ?? p?.personID ?? p?.PersonId ?? null;
        return typeof id === 'number' ? id : (typeof id === 'string' ? Number(id) : '');
    };

    getName = (p: any): string =>
        p?.name ?? p?.fullName ?? p?.personName ?? p?.Name ?? '';

    trackAny = (_: number, p: any) => this.getId(p) ?? p; // stabilize DOM if ID exists

    // CVA
    private onChange: (v: number | null) => void = () => { };
    private onTouched: () => void = () => { };
    writeValue(v: number | null): void { this.value = v ?? null; this.cdr.markForCheck(); }
    registerOnChange(fn: (v: number | null) => void): void { this.onChange = fn; }
    registerOnTouched(fn: () => void): void { this.onTouched = fn; }
    setDisabledState(isDisabled: boolean): void { this.disabled = isDisabled; this.cdr.markForCheck(); }

    onSelect(event: Event) {
        const raw = (event.target as HTMLSelectElement).value;
        const out = raw === '' ? null : Number(raw);
        this.value = out;
        this.onChange(out);
        this.onTouched();

        // emit the full person so parent can grab the name
        const picked = this.people.find(p => {
            const id = p?.personId ?? p?.id ?? p?.personID ?? p?.PersonId;
            return Number(id) === out;
        }) ?? null;
        this.selectionChange.emit(picked);
    }
}
