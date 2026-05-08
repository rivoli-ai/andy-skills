import { CommonModule } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  HostListener,
  computed,
  inject,
  input,
  output,
  signal,
} from '@angular/core';

export interface CustomSelectOption {
  value: string;
  label: string;
}

@Component({
  selector: 'app-custom-select',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './custom-select.component.html',
  styleUrl: './custom-select.component.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CustomSelectComponent {
  private readonly host = inject(ElementRef<HTMLElement>);

  /** Options shown in the panel (pre-rendered labels). */
  readonly options = input<CustomSelectOption[]>([]);

  readonly value = input<string>('');

  readonly valueChange = output<string>();

  readonly disabled = input(false);

  readonly placeholder = input('Select…');

  /** Optional id of element that labels this control (accessibility). */
  readonly ariaLabelledBy = input<string | undefined>(undefined);

  /** Stable id for listbox when using aria-controls from trigger. */
  readonly listboxId = input<string>('custom-select-listbox');

  protected readonly open = signal(false);

  protected readonly displayLabel = computed(() => {
    const opts = this.options();
    const v = this.value();
    const ph = this.placeholder();
    const opt = opts.find((o) => o.value === v);
    return opt?.label ?? ph;
  });

  protected toggle(ev: Event): void {
    ev.stopPropagation();
    if (this.disabled()) {
      return;
    }
    this.open.update((o) => !o);
  }

  protected pick(val: string, ev: Event): void {
    ev.stopPropagation();
    this.valueChange.emit(val);
    this.open.set(false);
  }

  @HostListener('document:click', ['$event'])
  protected onDocumentClick(ev: MouseEvent): void {
    if (!this.open()) {
      return;
    }
    const root = this.host.nativeElement;
    if (!root.contains(ev.target as Node)) {
      this.open.set(false);
    }
  }

  @HostListener('document:keydown', ['$event'])
  protected onDocumentKeydown(ev: KeyboardEvent): void {
    if (ev.key === 'Escape' && this.open()) {
      this.open.set(false);
    }
  }
}
