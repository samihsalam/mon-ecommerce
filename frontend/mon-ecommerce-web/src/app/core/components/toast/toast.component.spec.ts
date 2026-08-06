import { TestBed } from '@angular/core/testing';
import { LiveAnnouncer } from '@angular/cdk/a11y';

import { ToastComponent } from './toast.component';
import { ToastService } from '../../services/toast.service';

describe('ToastComponent', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [ToastComponent],
      providers: [{ provide: LiveAnnouncer, useValue: jasmine.createSpyObj('LiveAnnouncer', ['announce']) }],
    });
  });

  it('should render nothing when there is no message', () => {
    const fixture = TestBed.createComponent(ToastComponent);
    fixture.detectChanges();

    expect((fixture.nativeElement as HTMLElement).textContent?.trim()).toBe('');
  });

  it('should render the toast message', () => {
    const fixture = TestBed.createComponent(ToastComponent);
    const toastService = TestBed.inject(ToastService);
    toastService.show('Profil mis à jour');
    fixture.detectChanges();

    expect((fixture.nativeElement as HTMLElement).textContent?.trim()).toBe('Profil mis à jour');
  });
});
