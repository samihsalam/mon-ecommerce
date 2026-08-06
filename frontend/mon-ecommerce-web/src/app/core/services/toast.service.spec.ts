import { TestBed } from '@angular/core/testing';
import { LiveAnnouncer } from '@angular/cdk/a11y';

import { ToastService } from './toast.service';

describe('ToastService', () => {
  let liveAnnouncerSpy: jasmine.SpyObj<LiveAnnouncer>;

  beforeEach(() => {
    liveAnnouncerSpy = jasmine.createSpyObj('LiveAnnouncer', ['announce']);

    TestBed.configureTestingModule({
      providers: [{ provide: LiveAnnouncer, useValue: liveAnnouncerSpy }],
    });
  });

  it('should set the message signal', () => {
    const service = TestBed.inject(ToastService);

    service.show('Profil mis à jour');

    expect(service.message()).toBe('Profil mis à jour');
  });

  it('should announce the message via LiveAnnouncer', () => {
    const service = TestBed.inject(ToastService);

    service.show('Profil mis à jour');

    expect(liveAnnouncerSpy.announce).toHaveBeenCalledWith('Profil mis à jour');
  });

  // Story 8.4, AC #7: this is the exact scenario a signal-effect-driven announcement would miss —
  // Angular signals use Object.is equality, so setting the identical string twice would never
  // re-trigger an effect(). Calling announce() imperatively inside show() avoids that entirely.
  it('should announce two consecutive identical messages, not just the first', () => {
    const service = TestBed.inject(ToastService);

    service.show('Une erreur est survenue.');
    service.show('Une erreur est survenue.');

    expect(liveAnnouncerSpy.announce).toHaveBeenCalledTimes(2);
  });

  it('should clear the message after the duration elapses', (done) => {
    const service = TestBed.inject(ToastService);

    service.show('Profil mis à jour', 10);

    setTimeout(() => {
      expect(service.message()).toBeNull();
      done();
    }, 20);
  });
});
