import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';

import { ProfileComponent } from './profile.component';
import { environment } from '../../../../../environments/environment';

describe('ProfileComponent', () => {
  let httpMock: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ProfileComponent],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    }).compileComponents();

    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  function createAndLoad() {
    const fixture = TestBed.createComponent(ProfileComponent);
    fixture.detectChanges();

    const req = httpMock.expectOne(`${environment.apiUrl}/api/v1/account/profile`);
    req.flush({ name: 'Alice', email: 'alice@example.com', addresses: [] });

    return fixture;
  }

  it('should load and display the profile on init', async () => {
    const fixture = createAndLoad();
    await fixture.whenStable();
    fixture.detectChanges();

    const component = fixture.componentInstance;
    expect(component['form'].controls.name.value).toBe('Alice');
    expect(component['form'].controls.email.value).toBe('alice@example.com');
  });

  it('should not show the current-password field until the email is changed', async () => {
    const fixture = createAndLoad();
    await fixture.whenStable();
    fixture.detectChanges();

    let compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('#currentPassword')).toBeFalsy();

    const component = fixture.componentInstance;
    component['form'].controls.email.setValue('alice-new@example.com');
    fixture.detectChanges();

    compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('#currentPassword')).toBeTruthy();
  });

  it('should submit name/email and navigate the toast on success', async () => {
    const fixture = createAndLoad();
    await fixture.whenStable();
    fixture.detectChanges();

    const component = fixture.componentInstance;
    component['form'].controls.name.setValue('Alice Updated');

    const submitPromise = component['onSubmit']();

    const req = httpMock.expectOne(`${environment.apiUrl}/api/v1/account/profile`);
    expect(req.request.body).toEqual({ name: 'Alice Updated', email: 'alice@example.com', currentPassword: null });
    req.flush({ name: 'Alice Updated', email: 'alice@example.com', addresses: [] });

    await submitPromise;

    expect(component['accountStore'].profile()?.name).toBe('Alice Updated');
  });
});
